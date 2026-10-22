using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

// S1-CFG-003 验收 3 后半句的快速回归（协议替身 + 记录型审计服务）：接受与拒绝各记一条、拒绝在重试周期里不重复、
// 审计缺席时暂存并在就绪后按序补写；记录只含 namespace、键数与原因码。真实 Apollo + 平台链见 RealApolloReleaseAuditTests。
public sealed class ReleaseAuditTests(ApolloFixture apollo) : IClassFixture<ApolloFixture>
{
    private const string Original = "secretRef://platform/service/original";
    private const string Rotated = "secretRef://platform/service/rotated";
    private const string Literal = "synthetic-rejected-literal";
    private static readonly string[] s_application = ["application"];

    [Fact]
    public async Task 快照接受与拒绝写平台审计_拒绝不随重试重复_审计就绪前暂存并按序补写_记录不含配置值()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-release-" + Guid.NewGuid().ToString("N"))).FullName;
        var appId = "qian-release-" + Guid.NewGuid().ToString("N");
        using var schema = JsonDocument.Parse("""{"properties":{"Service":{"properties":{"credential":{"x-qian-sensitive":true}}}}}""");
        var retries = new RetryLog();
        try
        {
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "10", ["Service:credential"] = Original }, cancellationToken: ct);
            var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
            {
                BasePath = directory, ApolloConfigurationSchema = schema.RootElement, Logger = retries,
                Arguments = ["--Apollo:AppId=" + appId, "--Apollo:Env=UAT", "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri.AbsoluteUri]
            }).Build();
            using var owner = (IDisposable)root;
            await using var provider = new ServiceCollection().AddQianConfiguration(root).BuildServiceProvider();
            await using var runtime = new PluginLoader(provider);
            await runtime.MountAsync([new PluginDefinition(new PluginEntry("config", "qian.config.apollo", "1.0.0", ScopeKey.Global, 0,
                JsonSerializer.SerializeToElement(new { appId, env = "UAT", meta = apollo.ConfigServiceUri.AbsoluteUri, namespaces = s_application })),
                new ApolloConfigurationPlugin(), [ConfigDefinition.Key])], ct);

            // 审计缺席时发布一次：记录暂存，审计挂载后与启动快照一起按序补写。
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "25", ["Service:credential"] = Original }, cancellationToken: ct);
            await WaitAsync(() => root["Sample:PageSize"] == "25", ct);
            var audit = new RecordingAudit();
            await runtime.MountAsync([new PluginDefinition(new PluginEntry("audit", "test.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                new AuditProvider(audit), [AuditDefinition.Key])], ct);
            var initial = await audit.NextAsync(ct);
            var updated = await audit.NextAsync(ct);
            AssertRecord(initial, appId, "applied", 2);
            AssertRecord(updated, appId, "applied", 1);
            Assert.NotEqual(initial.InputHash, updated.InputHash);

            // 敏感字段明文：拒绝并保留旧值；客户端每个重试周期重拉同一候选，只记一次。
            var before = retries.Count;
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "25", ["Service:credential"] = Literal }, cancellationToken: ct);
            var rejected = await audit.NextAsync(ct);
            AssertRecord(rejected, appId, "rejected", 1);
            Assert.Equal(["CONFIG_SECRET_EXPOSED"], rejected.Payload.GetProperty("reasons").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(Original, root["Service:credential"]);
            await WaitAsync(() => retries.Count >= before + 2, ct, TimeSpan.FromSeconds(30));
            Assert.False(audit.Records.Reader.TryRead(out _));

            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Sample:PageSize"] = "25", ["Service:credential"] = Rotated }, cancellationToken: ct);
            var recovered = await audit.NextAsync(ct);
            AssertRecord(recovered, appId, "applied", 1);
            Assert.Equal(Rotated, root["Service:credential"]);
            // 资源与主体是 AppId 等标识；配置值只可能出现在补充数据里。
            foreach (var record in new[] { initial, updated, rejected, recovered })
            {
                var text = record.Payload.GetRawText() + record;
                foreach (var value in new[] { Literal, Original, Rotated, "PageSize", "credential" }) Assert.DoesNotContain(value, text, StringComparison.Ordinal);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void AssertRecord(AuditRecord record, string appId, string result, int changedKeys)
    {
        Assert.Equal((null, "service", appId, "event", "1.0.0", result), (record.TenantId, record.ActorType, record.ActorId, record.Channel, record.ContractVersion, record.Result));
        Assert.Equal(result == "applied" ? ConfigEvents.ReleaseAppliedCapabilityId : ConfigEvents.ReleaseRejectedCapabilityId, record.CapabilityId);
        Assert.Equal($"apollo://{appId}/UAT/default/application", record.Resource);
        Assert.Equal("application", record.Payload.GetProperty("namespace").GetString());
        Assert.Equal(changedKeys, record.Payload.GetProperty("changedKeys").GetInt32());
        Assert.Matches("^[0-9a-f]{64}$", record.InputHash);
    }

    private static async Task WaitAsync(Func<bool> condition, CancellationToken ct, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(50, ct);
        Assert.True(condition());
    }

    internal sealed class RecordingAudit : IAuditService
    {
        private long _sequence;
        public Channel<AuditRecord> Records { get; } = Channel.CreateUnbounded<AuditRecord>();
        public async Task<AuditRecord> NextAsync(CancellationToken ct) => await Records.Reader.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(30), ct);
        public Task<AuditChainEntry> AppendPlatformAsync(IPluginContext producer, AuditRecord record, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ScopeKey.Global, producer.Scope);
            Records.Writer.TryWrite(record);
            return Task.FromResult(new AuditChainEntry(Interlocked.Increment(ref _sequence), null, new string('0', 64), record));
        }
        public Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditChainEntry>> ReadPlatformAsync(IPluginContext producer, long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainVerification> VerifyPlatformAsync(IPluginContext producer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class AuditProvider(RecordingAudit audit) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<IAuditService>(AuditDefinition.Key, audit);
            return ValueTask.CompletedTask;
        }
    }

    // Kun 客户端每次快照校验失败都记一条重试日志；用它确认被拒候选已经被重拉过。
    private sealed class RetryLog : ILogger
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).Contains("快照校验失败", StringComparison.Ordinal)) Interlocked.Increment(ref _count);
        }
    }
}
