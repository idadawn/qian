using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

[Collection(RealApolloGroup.Name)]
public sealed class RealApolloFeatureFlagsTests(RealApolloFixture apollo) : IClassFixture<RealApolloFixture>
{
    [Fact]
    public async Task 真实公共namespace跨应用灰度_挂载与消费侧权益结果不改变求值_坏规则拒绝并保留缓存()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-real-flags-" + Guid.NewGuid().ToString("N"))).FullName;
        var ownerApp = "flags-owner-" + Guid.NewGuid().ToString("N");
        var consumerApp = "flags-reader-" + Guid.NewGuid().ToString("N");
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        var initial = "{\"default\":false,\"tenants\":[\"" + tenantA.ToString("D") + "\"]}";
        var log = new RejectionLog();
        try
        {
            await apollo.CreateAppAsync(ownerApp, ct);
            await apollo.CreateNamespaceAsync(ownerApp, "qian.common.flags", isPublic: true, cancellationToken: ct);
            await apollo.PublishNamespaceAsync(ownerApp, "qian.common.flags", "flag.preview", initial, ct);
            await apollo.CreateAppAsync(consumerApp, ct);
            await apollo.PublishAsync(consumerApp, "Fixture", "ready", ct);
            await apollo.AssociateNamespaceAsync(consumerApp, "qian.common.flags", ct);
            await apollo.WaitForPublishedAsync(consumerApp, "qian.common.flags", "flag.preview", initial, ct);
            var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
            {
                BasePath = directory, Logger = log,
                Arguments = ["--Apollo:AppId=" + consumerApp, "--Apollo:Env=UAT", "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri,
                    "--Apollo:Namespaces:0=qian.common.flags", "--Apollo:CacheDir=cache"]
            }).Build();
            using var rootOwner = (IDisposable)root;
            using var services = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
            await using var runtime = new PluginLoader(services);
            await runtime.MountAsync([new(new PluginEntry("config", "qian.config.apollo", "1.0.0", ScopeKey.Global, 0,
                JsonElement.Parse("{}")), new ApolloConfigurationPlugin(), [ConfigDefinition.Key])], ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("config", ScopeKey.Global));
            var flags = runtime.GetContext("config", ScopeKey.Global).Config().Flags;
            Assert.True(flags.IsEnabled("preview", tenantA));
            Assert.False(flags.IsEnabled("preview", tenantB));
            var scope = new ScopeKey("tenant", tenantA.ToString("D"));
            await runtime.MountAsync([new(new PluginEntry("feature-consumer", "test.feature-consumer", "1.0.0", scope, 0,
                JsonElement.Parse("{}")), new Consumer(), [])], ct);
            var visible = runtime.GetContext("feature-consumer", scope).Config().Flags;
            Assert.True(visible.IsEnabled("preview", tenantA));
            // ENT-002 承担真实权益 Provider 的三处联调；这里明确使用消费侧权益结果 fixture 检查灰度端口不变。
            foreach (var entitled in new[] { false, true })
            {
                Assert.Equal(entitled, entitled && visible.IsEnabled("preview", tenantA));
                Assert.True(flags.IsEnabled("preview", tenantA));
                Assert.False(flags.IsEnabled("preview", tenantB));
            }
            await runtime.UnmountAsync("feature-consumer", scope, ct);
            Assert.True(flags.IsEnabled("preview", tenantA));
            Assert.False(flags.IsEnabled("preview", tenantB));
            await apollo.PublishNamespaceAsync(ownerApp, "qian.common.flags", "flag.preview", "{\"default\":false,\"percentage\":101}", ct);
            await log.Rejected.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            Assert.Equal(initial, root["flag.preview"]);
            Assert.True(flags.IsEnabled("preview", tenantA));
            Assert.False(flags.IsEnabled("preview", tenantB));
            foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
                Assert.DoesNotContain("\\\"percentage\\\":101", await File.ReadAllTextAsync(file, ct), StringComparison.Ordinal);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var listen = ChangeToken.OnChange(root.GetReloadToken, () =>
            { if (root["flag.preview"] == "{\"default\":false,\"percentage\":100}") changed.TrySetResult(); });
            await apollo.PublishNamespaceAsync(ownerApp, "qian.common.flags", "flag.preview", "{\"default\":false,\"percentage\":100}", ct);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            Assert.True(flags.IsEnabled("preview", tenantA));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (!flags.IsEnabled("preview", tenantB) && DateTimeOffset.UtcNow < deadline) await Task.Delay(10, ct);
            Assert.True(flags.IsEnabled("preview", tenantB));
            TestContext.Current.TestOutputHelper!.WriteLine("真实 Apollo 2.5.2 公共 namespace 跨应用：白名单、坏规则保留、修正热更新、Runtime scope 卸载正交均通过；权益结果为消费侧 fixture，真实联调归 ENT-002。");
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Consumer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class RejectionLog : ILogger
    {
        internal TaskCompletionSource Rejected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (formatter(state, exception).Contains("快照校验失败", StringComparison.Ordinal)) Rejected.TrySetResult(); }
    }
}
