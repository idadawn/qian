using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Tests;

public sealed class FeatureFlagsTests
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void 白名单默认与未定义开关_仅依赖名称租户与规则()
    {
        using var root = Build("""{"default":false,"tenants":["00000000-0000-0000-0000-000000000001"]}""");
        using var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        var flags = services.GetRequiredService<IConfigService>().Flags;
        Assert.Same(flags, services.GetRequiredService<IFeatureFlags>());
        Assert.True(flags.IsEnabled("preview", TenantA));
        Assert.False(flags.IsEnabled("preview", TenantB));
        Assert.False(flags.IsEnabled("missing", TenantA));
        root["flag.preview"] = """{"default":true}"""; root.Reload();
        Assert.True(flags.IsEnabled("preview", TenantA));
        Assert.True(flags.IsEnabled("preview", TenantB));
        root["flag.preview"] = null; root.Reload();
        // null 不是合法规则删除；整份候选被拒绝，最后有效值保留。
        Assert.True(flags.IsEnabled("preview", TenantB));
    }

    [Fact]
    public void 百分比固定向量_多实例与重载稳定_增量放量集合单调()
    {
        using var root = Build("""{"default":false,"percentage":22}""");
        using var first = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        using var second = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        var flags = first.GetRequiredService<IFeatureFlags>();
        var other = second.GetRequiredService<IFeatureFlags>();
        // SHA-256(UTF8("preview\n" + Guid:D)) 前 8 字节大端数 % 10000：A=2108，B=9106。
        for (var iteration = 0; iteration < 100; iteration++)
        {
            Assert.True(flags.IsEnabled("preview", TenantA));
            Assert.False(flags.IsEnabled("preview", TenantB));
            Assert.Equal(flags.IsEnabled("preview", TenantA), other.IsEnabled("preview", TenantA));
            root.Reload();
        }
        var tenants = Enumerable.Range(1, 1000).Select(index => new Guid(index, 0, 0, new byte[8])).ToArray();
        var previous = new HashSet<Guid>();
        foreach (var percentage in new[] { 0, 1, 25, 50, 99, 100 })
        {
            root["flag.preview"] = "{\"default\":false,\"percentage\":" + percentage + "}"; root.Reload();
            var next = tenants.Where(tenant => flags.IsEnabled("preview", tenant)).ToHashSet();
            Assert.True(previous.IsSubsetOf(next));
            if (percentage == 0) Assert.Empty(next);
            if (percentage == 100) Assert.Equal(tenants.Length, next.Count);
            previous = next;
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"default\":\"false\"}")]
    [InlineData("{\"default\":false,\"default\":true}")]
    [InlineData("{\"default\":false,\"percentage\":-1}")]
    [InlineData("{\"default\":false,\"percentage\":101}")]
    [InlineData("{\"default\":false,\"percentage\":1.5}")]
    [InlineData("{\"default\":false,\"tenants\":[\"not-a-tenant\"]}")]
    [InlineData("{\"default\":false,\"tenants\":[\"00000000-0000-0000-0000-000000000000\"]}")]
    [InlineData("{\"default\":false,\"tenants\":[\"00000000-0000-0000-0000-000000000001\",\"00000000-0000-0000-0000-000000000001\"]}")]
    [InlineData("{\"default\":false,\"private\":\"literal-that-must-not-appear\"}")]
    [InlineData("{\"default\":false,\"percentage\":null}")]
    public void 非法初值拒绝启动_异常不含原始规则(string rule)
    {
        using var root = Build(rule);
        using var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        var error = Assert.Throws<QianErrorException>(() => services.GetRequiredService<IFeatureFlags>());
        Assert.Equal("CONFIG_RELOAD_REJECTED", error.Error.Code.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(rule, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("literal-that-must-not-appear", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")][InlineData("Preview")][InlineData("path/name")][InlineData("name:child")][InlineData("white space")]
    public void 非规范名称拒绝_不因配置键大小写规则产生别名(string name)
    {
        using var root = Build("""{"default":true}""");
        using var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        var flags = services.GetRequiredService<IFeatureFlags>();
        Assert.Throws<ArgumentException>(() => flags.IsEnabled(name, TenantA));
        Assert.Throws<ArgumentException>(() => flags.IsEnabled("preview", Guid.Empty));
    }

    [Fact]
    public async Task 重载原子拒绝并告警_修正恢复_卸载撤销监听且宿主根仍存活()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = Build("""{"default":true}""");
        using var log = new CapturedLogs();
        using var services = new ServiceCollection().AddQianConfiguration(root).AddSingleton<ILoggerFactory>(log).BuildServiceProvider();
        using var meter = new MeterListener();
        long rejected = 0;
        meter.InstrumentPublished = (instrument, listener) =>
        { if (instrument.Meter.Name == "Qian.Config" && instrument.Name == "qian.config.reload.failed") listener.EnableMeasurementEvents(instrument); };
        meter.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        { foreach (var tag in tags) if (tag.Key == "options.type" && Equals(tag.Value, typeof(IFeatureFlags).FullName)) Interlocked.Add(ref rejected, value); });
        meter.Start();
        await using var runtime = new PluginLoader(services);
        var observer = new Observer();
        await runtime.MountAsync([
            new(new PluginEntry("config", "qian.config", "1.0.0", ScopeKey.Global, 0, JsonElement.Parse("{}")), new ConfigurationPlugin(), [ConfigDefinition.Key]),
            new(new PluginEntry("observer", "test.flags-observer", "1.0.0", ScopeKey.Global, 0, JsonElement.Parse("{}")), observer, [])], ct);
        var flags = runtime.GetContext("config", ScopeKey.Global).Config().Flags;
        Assert.True(flags.IsEnabled("preview", TenantA));
        root["flag.preview"] = """{"default":false}""";
        root["flag.other"] = "literal-that-must-not-appear"; root.Reload();
        var notice = await observer.Notices.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ct);
        Assert.Equal("CONFIG_RELOAD_REJECTED", notice.Code);
        Assert.Equal("qian.common.flags", notice.Section);
        Assert.True(flags.IsEnabled("preview", TenantA));
        Assert.False(flags.IsEnabled("other", TenantA));
        Assert.Equal(1, Interlocked.Read(ref rejected));
        Assert.DoesNotContain("literal-that-must-not-appear", string.Join('\n', log.Messages), StringComparison.Ordinal);
        root["flag.other"] = """{"default":true}"""; root.Reload();
        Assert.False(flags.IsEnabled("preview", TenantA));
        Assert.True(flags.IsEnabled("other", TenantA));
        await runtime.UnmountAsync("observer", ScopeKey.Global, ct);
        await runtime.UnmountAsync("config", ScopeKey.Global, ct);
        Assert.Throws<ObjectDisposedException>(() => flags.IsEnabled("preview", TenantA));
        root["flag.preview"] = "invalid-after-unload"; root.Reload();
        Assert.Equal(1, Interlocked.Read(ref rejected));
        Assert.Equal("invalid-after-unload", root["flag.preview"]);
    }

    private static ConfigurationRoot Build(string rule) => new([new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider(
        new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = new Dictionary<string, string?> { ["flag.preview"] = rule } })]);

    private sealed class Observer : IQianPlugin
    {
        public Channel<ConfigReloadFailed> Notices { get; } = Channel.CreateUnbounded<ConfigReloadFailed>();
        public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.On(ConfigRuntimeEvents.ReloadFailed, DispatchMode.Emit, (Func<ConfigReloadFailed, ValueTask>)(notice =>
            { Notices.Writer.TryWrite(notice); return ValueTask.CompletedTask; }));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturedLogs : ILoggerFactory, ILogger
    {
        public List<string> Messages { get; } = [];
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
