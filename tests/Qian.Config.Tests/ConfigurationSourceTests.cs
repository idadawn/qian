using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

public sealed class ConfigurationSourceTests(ApolloFixture apollo) : IClassFixture<ApolloFixture>
{
    private static readonly string[] s_namespaces = ["application", "qian.plugin.sample", "qian.common.test"];
    private static readonly string[] s_application = ["application"];

    [Fact]
    public async Task 仅环境引导可启动_AccessKey每次解析_鉴权失败不回退有效缓存()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-env-apollo-" + Guid.NewGuid().ToString("N"))).FullName;
        var prefix = "QIAN_APOLLO_" + Guid.NewGuid().ToString("N") + "_";
        var appId = "qian-env-" + Guid.NewGuid().ToString("N");
        var secret = Guid.NewGuid().ToString("N");
        const string reference = "secretRef://platform/apollo/access-key";
        var variables = new Dictionary<string, string>
        {
            ["Apollo__AppId"] = appId, ["Apollo__Env"] = "PRO", ["Apollo__Cluster"] = "default",
            ["Apollo__ConfigServer__0"] = apollo.ConfigServiceUri.AbsoluteUri, ["Apollo__Label"] = "acceptance",
            ["Apollo__CacheDir"] = "cache", ["Apollo__AccessKey"] = reference
        };
        try
        {
            foreach (var pair in variables) Environment.SetEnvironmentVariable(prefix + pair.Key, pair.Value);
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "18" }, cancellationToken: ct);
            await apollo.SetAccessKeyAsync(appId, secret, ct);
            var resolutions = 0;
            var root = Build(_ => { Interlocked.Increment(ref resolutions); return secret; });
            using (var owner = (IDisposable)root)
            using (var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider())
            {
                Assert.Equal("18", root["Sample:PageSize"]);
                Assert.Equal(reference, root["Apollo:AccessKey"]);
                Assert.True(resolutions > 0);
                await using var runtime = new PluginLoader(services);
                await runtime.MountAsync([new PluginDefinition(new PluginEntry("config", "qian.config.apollo", "1.0.0", ScopeKey.Global, 0,
                    JsonSerializer.SerializeToElement(new { appId, env = "PRO", meta = apollo.ConfigServiceUri.AbsoluteUri, namespaces = s_application })),
                    new ApolloConfigurationPlugin(), [ConfigDefinition.Key])], ct);
                Assert.Equal(PluginState.Ready, runtime.GetState("config", ScopeKey.Global));
                Assert.Equal("18", runtime.GetContext("config", ScopeKey.Global).Config().GetRequiredValue("Sample:PageSize"));
            }
            var cache = Assert.Single(Directory.GetFiles(Path.Combine(directory, "cache", "PRO"), "*.json"));
            Assert.DoesNotContain(secret, await File.ReadAllTextAsync(cache, ct), StringComparison.Ordinal);
            Assert.Throws<UnauthorizedAccessException>(() => Build(_ => "invalid-test-key"));
        }
        finally
        {
            foreach (var name in variables.Keys) Environment.SetEnvironmentVariable(prefix + name, null);
            Directory.Delete(directory, true);
        }
        IConfigurationRoot Build(Func<string, string?> resolver) => new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
        {
            BasePath = directory, EnvironmentPrefix = prefix,
            ResolveAccessKey = name => { Assert.Equal(reference, name); return resolver(name); }
        }).Build();
    }
    [Fact]
    public async Task 五层配置顺序和namespace顺序固定_引导只从宿主读取()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "qian-cfg-" + Guid.NewGuid().ToString("N"));
        var prefix = "QIAN_CFG_" + Guid.NewGuid().ToString("N") + "_";
        Directory.CreateDirectory(directory);
        var secrets = Directory.CreateDirectory(Path.Combine(directory, "secrets")).FullName;
        try
        {
            await apollo.SetHoldAsync(TimeSpan.FromSeconds(1), ct);
            await apollo.PublishAsync("qian-test", "qian.common.test", new Dictionary<string, string>
                { ["Layer:Value"] = "common", ["Tier:Value"] = "common", ["CommonOnly"] = "yes" }, cancellationToken: ct);
            await apollo.PublishAsync("qian-test", "qian.plugin.sample", new Dictionary<string, string>
                { ["Layer:Value"] = "plugin", ["Tier:Value"] = "plugin" }, cancellationToken: ct);
            await apollo.PublishAsync("qian-test", "application", new Dictionary<string, string>
                { ["Layer:Value"] = "application" }, cancellationToken: ct);
            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), JsonSerializer.Serialize(new
            {
                Apollo = new { AppId = "qian-test", Env = "UAT", ConfigServer = new[] { apollo.ConfigServiceUri.AbsoluteUri },
                    Namespaces = s_namespaces },
                Layer = new { Value = "json" }, JsonOnly = "yes"
            }), ct);
            await File.WriteAllTextAsync(Path.Combine(secrets, "Layer__Value"), "secret", ct);
            Environment.SetEnvironmentVariable(prefix + "Layer__Value", "env");
            Check("cli", secrets, ["--Layer:Value=cli"]);
            Check("env", secrets, []);
            Environment.SetEnvironmentVariable(prefix + "Layer__Value", null);
            Check("secret", secrets, []);
            Check("application", null, []);
            using var local = (IDisposable)new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
                { BasePath = directory, EnvironmentPrefix = prefix, UseApollo = false }).Build();
            Assert.Equal("json", ((IConfiguration)local)["Layer:Value"]);
        }
        finally { Environment.SetEnvironmentVariable(prefix + "Layer__Value", null); Directory.Delete(directory, true); }

        void Check(string expected, string? secretDirectory, string[] arguments)
        {
            var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
                { BasePath = directory, SecretDirectory = secretDirectory, EnvironmentPrefix = prefix, Arguments = arguments }).Build();
            using var owner = (IDisposable)root;
            Assert.Equal(expected, root["Layer:Value"]);
            Assert.Equal("plugin", root["Tier:Value"]);
            Assert.Equal("yes", root["CommonOnly"]);
            Assert.Equal("yes", root["JsonOnly"]);
        }
    }

    [Fact]
    public async Task 标准Config可独立消费_插件卸载不销毁借用的宿主根()
    {
        using var root = new ConfigurationRoot([new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider(
            new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = new Dictionary<string, string?> { ["Feature:Mode"] = "safe" } })]);
        root.Reload();
        using var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
        var standalone = services.GetRequiredService<IConfigService>();
        Assert.Equal("safe", standalone.GetRequiredValue("Feature:Mode"));
        var error = Assert.Throws<QianErrorException>(() => standalone.GetRequiredValue("Missing"));
        Assert.Equal("CONFIG_REQUIRED_MISSING", error.Error.Code.Code);
        await using var runtime = new PluginLoader(services);
        await runtime.MountAsync([new PluginDefinition(new PluginEntry("config", "qian.config", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new ConfigurationPlugin(), [ConfigDefinition.Key])], TestContext.Current.CancellationToken);
        var context = runtime.GetContext("config", ScopeKey.Global);
        Assert.Equal("safe", context.Config().ReadSection("Feature")["Mode"]);
        await runtime.UnmountAsync("config", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => context.Config());
        Assert.Equal("safe", root["Feature:Mode"]);
    }

    [Theory]
    [InlineData("Apollo:Env", "DEV")]
    [InlineData("Apollo:ConfigServer:0", "http://user:password@localhost/")]
    [InlineData("Apollo:AccessKey", "plaintext-is-not-a-reference")]
    [InlineData("Apollo:Namespaces:0", "tenant.settings")]
    public void 不安全或无效引导在联网前拒绝(string key, string value)
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-cfg-invalid-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var args = new Dictionary<string, string> { ["Apollo:AppId"] = "test", ["Apollo:Env"] = "UAT", ["Apollo:ConfigServer:0"] = "http://localhost:1/" };
            args[key] = value;
            var error = Assert.Throws<QianErrorException>(() => new ConfigurationBuilder().AddQianConfigurationSources(
                new QianConfigurationOptions { BasePath = directory, Arguments = args.Select(pair => "--" + pair.Key + "=" + pair.Value).ToArray() }));
            Assert.Equal("CONFIG_BOOTSTRAP_INVALID", error.Error.Code.Code);
        }
        finally { Directory.Delete(directory, true); }
    }
}
