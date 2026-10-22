using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

// S1-CFG-003 验收 1：强类型配置热更新先校验，失败保留旧值、计数并发告警，修正后恢复。
public sealed partial class OptionsReloadTests(ApolloFixture apollo) : IClassFixture<ApolloFixture>
{
    private static readonly string[] s_application = ["application"];

    [Fact]
    public async Task Apollo发布越界值_宿主保留旧值_指标加一_告警事件发出_修正后恢复()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-reload-" + Guid.NewGuid().ToString("N"))).FullName;
        var appId = "qian-reload-" + Guid.NewGuid().ToString("N");
        using var metrics = new FailureMeter(typeof(SampleOptions).FullName!);
        try
        {
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "10" }, cancellationToken: ct);
            var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
            {
                BasePath = directory,
                Arguments = ["--Apollo:AppId=" + appId, "--Apollo:Env=UAT", "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri.AbsoluteUri]
            }).Build();
            using var owner = (IDisposable)root;
            var services = new ServiceCollection().AddQianConfiguration(root);
            services.AddQianOptions<SampleOptions>("Sample", Bind, plugin: "qian.sample", schema: SampleJson.Default.SampleOptions);
            services.AddSingleton<IValidateOptions<SampleOptions>, SampleOptionsValidator>();
            await using var provider = services.BuildServiceProvider();
            var monitor = provider.GetRequiredService<IOptionsMonitor<SampleOptions>>();
            Assert.Equal(10, monitor.CurrentValue.PageSize);

            await using var runtime = new PluginLoader(provider);
            var observer = new ReloadObserver();
            await runtime.MountAsync([
                new PluginDefinition(new PluginEntry("config", "qian.config.apollo", "1.0.0", ScopeKey.Global, 0,
                    JsonSerializer.SerializeToElement(new { appId, env = "UAT", meta = apollo.ConfigServiceUri.AbsoluteUri, namespaces = s_application })),
                    new ApolloConfigurationPlugin(), [ConfigDefinition.Key]),
                new PluginDefinition(new PluginEntry("observer", "test.reload-observer", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                    observer, [])], ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("observer", ScopeKey.Global));

            var pushed = Channel.CreateUnbounded<int>();
            using var listener = monitor.OnChange(options => pushed.Writer.TryWrite(options.PageSize));
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "1000" }, cancellationToken: ct);
            var alert = await observer.Alerts.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct);
            // 原始配置根已是新值，强类型配置仍是上一份有效值，监听者没有收到越界值。
            Assert.Equal("1000", root["Sample:PageSize"]);
            Assert.Equal(10, monitor.CurrentValue.PageSize);
            Assert.False(pushed.Reader.TryRead(out _));
            Assert.Equal((typeof(SampleOptions).FullName, "", "Sample", 1, "CONFIG_RELOAD_REJECTED", "qian.sample"),
                (alert.OptionsType, alert.OptionsName, alert.Section, alert.Failures, alert.Code, alert.Plugin));
            Assert.DoesNotContain("1000", JsonSerializer.Serialize(alert, SampleJson.Default.ConfigReloadFailed), StringComparison.Ordinal);
            Assert.Equal(1, metrics.Count);
            Assert.Equal(["qian.sample"], metrics.Plugins);

            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "25" }, cancellationToken: ct);
            Assert.Equal(25, await pushed.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct));
            Assert.Equal(25, monitor.CurrentValue.PageSize);
            Assert.Equal(1, metrics.Count);
            Assert.False(observer.Alerts.Reader.TryRead(out _));

            // 卸载配置组件后告警不再分发，保留旧值与指标照常。
            await runtime.UnmountAsync("observer", ScopeKey.Global, ct);
            await runtime.UnmountAsync("config", ScopeKey.Global, ct);
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "0" }, cancellationToken: ct);
            await metrics.WaitAsync(2, ct);
            Assert.Equal(25, monitor.CurrentValue.PageSize);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task 关键配置启动即校验_可选配置首次读取才校验_绑定失败同样保留旧值()
    {
        var ct = TestContext.Current.CancellationToken;
        using var metrics = new FailureMeter(typeof(BindOnlyOptions).FullName!);
        var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sample:PageSize"] = "500", ["Other:PageSize"] = "7" }).Build();

        var critical = new ServiceCollection().AddQianConfiguration(root);
        critical.AddQianOptions<SampleOptions>("Sample", Bind);
        critical.AddSingleton<IValidateOptions<SampleOptions>, SampleOptionsValidator>();
        await using (var provider = critical.BuildServiceProvider())
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        var optional = new ServiceCollection().AddQianConfiguration(root);
        optional.AddQianOptions<SampleOptions>("Sample", Bind, critical: false);
        optional.AddSingleton<IValidateOptions<SampleOptions>, SampleOptionsValidator>();
        await using (var provider = optional.BuildServiceProvider())
        {
            Assert.Null(provider.GetService<IStartupValidator>());
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptionsMonitor<SampleOptions>>().CurrentValue);
            Assert.Throws<InvalidOperationException>(() => optional.AddQianOptions<SampleOptions>("Sample", Bind));
        }

        var binding = new ServiceCollection().AddQianConfiguration(root);
        binding.AddQianOptions<BindOnlyOptions>("Other", (section, options) => options.PageSize = int.Parse(section["PageSize"]!, CultureInfo.InvariantCulture));
        await using (var provider = binding.BuildServiceProvider())
        {
            var monitor = provider.GetRequiredService<IOptionsMonitor<BindOnlyOptions>>();
            Assert.Equal(7, monitor.CurrentValue.PageSize);
            root["Other:PageSize"] = "not-a-number"; ((IConfigurationRoot)root).Reload();
            await metrics.WaitAsync(1, ct);
            Assert.Equal(7, monitor.CurrentValue.PageSize);
            root["Other:PageSize"] = "8"; ((IConfigurationRoot)root).Reload();
            Assert.Equal(8, monitor.CurrentValue.PageSize);
        }
    }

    [Fact]
    public void 登记Schema时导出不含配置值的JSON_Schema_2020_12()
    {
        var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sample:PageSize"] = "42" }).Build();
        var services = new ServiceCollection().AddQianConfiguration(root);
        services.AddQianOptions<SampleOptions>("Sample", Bind, schema: SampleJson.Default.SampleOptions);
        using var provider = services.BuildServiceProvider();
        var schema = Assert.Single(provider.GetServices<QianOptionsSchema>());
        Assert.Equal(("Sample", typeof(SampleOptions).FullName), (schema.Section, schema.OptionsType));
        var exported = schema.Export();
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", exported["$schema"]!.GetValue<string>());
        Assert.Equal("integer", exported["properties"]!["pageSize"]!["type"]!.GetValue<string>());
        Assert.DoesNotContain("42", exported.ToJsonString(), StringComparison.Ordinal);
        exported["mutated"] = true;
        Assert.Null(schema.Export()["mutated"]);
    }

    private static void Bind(IConfiguration section, SampleOptions options)
    {
        if (section["PageSize"] is { } value) options.PageSize = int.Parse(value, CultureInfo.InvariantCulture);
    }

    public sealed class SampleOptions
    {
        [Range(1, 100)] public int PageSize { get; set; }
    }
    public sealed class BindOnlyOptions { public int PageSize { get; set; } }

    // DataAnnotations 走 Options 源生成校验器，不用反射；与 AddQianOptions 一起在 AOT profile 可用。
    [OptionsValidator]
    public sealed partial class SampleOptionsValidator : IValidateOptions<SampleOptions>;

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SampleOptions))][JsonSerializable(typeof(ConfigReloadFailed))]
    internal sealed partial class SampleJson : JsonSerializerContext;

    private sealed class ReloadObserver : IQianPlugin
    {
        public Channel<ConfigReloadFailed> Alerts { get; } = Channel.CreateUnbounded<ConfigReloadFailed>();
        public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.On(ConfigRuntimeEvents.ReloadFailed, DispatchMode.Emit, (Func<ConfigReloadFailed, ValueTask>)(failure =>
            { Alerts.Writer.TryWrite(failure); return ValueTask.CompletedTask; }));
            return ValueTask.CompletedTask;
        }
    }

    // MeterListener 是进程级的：只统计本测试的 options 类型，避免并行测试串数。
    private sealed class FailureMeter : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly string _type;
        private readonly List<string?> _plugins = [];
        private long _count;
        public FailureMeter(string type)
        {
            _type = type;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Qian.Config" && instrument.Name == "qian.config.reload.failed") listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                string? type = null, plugin = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "options.type") type = tag.Value as string;
                    if (tag.Key == "plugin") plugin = tag.Value as string;
                }
                if (type != _type) return;
                lock (_plugins) _plugins.Add(plugin);
                Interlocked.Add(ref _count, value);
            });
            _listener.Start();
        }
        public long Count => Interlocked.Read(ref _count);
        public IReadOnlyList<string?> Plugins { get { lock (_plugins) return [.. _plugins]; } }
        public async Task WaitAsync(long expected, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (Count < expected && DateTime.UtcNow < deadline) await Task.Delay(50, ct);
            Assert.Equal(expected, Count);
        }
        public void Dispose() => _listener.Dispose();
    }
}
