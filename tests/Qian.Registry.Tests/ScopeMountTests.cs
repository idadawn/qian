using System.Net;
using System.Text.Json;
using Json.Schema;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Data;
using Qian.Events;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

// S1-RT-005 验收 4：公开挂载 / 卸载写 scope_patches，并在同一事务把 registry.scope.*.v1 信封放进 Outbox；卸载不改任何开关字段，
// 业务表面经真实 SDK 中间件在 5 秒内恢复拦截。网关与外壳两处拦截的接线归 S1-ENT-002。
public sealed partial class PatchPersistenceTests
{
    private static readonly EventOrigin Operator = new("qian.lifecycle", EventActor.User("operator-person")) { CorrelationId = Guid.CreateVersion7() };

    [Fact]
    public async Task 公开挂载写入scope_patches并同事务发出mounted_v1信封_卸载只留墓碑并发出unmounted_v1_五秒内SDK拦截恢复且B不受影响()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, events: true);
        await using var http = await StartEndpointHost(running, tenant, true);
        var scopeA = new ScopeKey("tenant", HttpTenantA);
        using (var unmounted = await http.Get("/private", HttpTenantA)) Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
        Assert.Equal(0, http.Calls);

        ScopePatchSnapshot mounted;
        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
            mounted = await running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{"label":"A"}}"""), 0, "operator-a", Ct);
        Assert.Equal((1, "operator-a"), (mounted.Revision, mounted.UpdatedBy));
        await WithinFiveSecondsAsync(async () => { using var response = await http.Get("/private", HttpTenantA); return response.StatusCode == HttpStatusCode.OK; });
        using (var other = await http.Get("/private", HttpTenantB)) Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        // 数据库里只有 scope_patches 一行与同一事务写入的一条 Outbox 信封，载荷不含配置原文。
        Assert.Equal(("tenant:" + HttpTenantA, "test.private", "1.0.0", 1, false), await PatchRow(HttpTenantA, "private"));
        var first = Assert.Single(await Outbox(running.Host.Services, tenant, HttpTenantA));
        Assert.Equal(RegistryEvents.ScopeMounted, first.Type);
        var envelope = AssertEnvelope(first.Payload, HttpTenantA);
        Assert.Equal("registry.scope.mounted", envelope.Subject);
        Assert.Equal(Operator.CorrelationId, envelope.CorrelationId);
        Assert.Equal("""{"scope":"tenant:01900000-0000-7000-8000-000000000001","entryId":"private","plugin":"test.private","version":"1.0.0","revision":1}""", envelope.Payload.GetRawText());

        // 同一内容重复挂载是幂等的：不增加修订，也不再发事件。
        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(1, (await running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{"label":"A"}}"""), 1, "operator-a", Ct)).Revision);
        Assert.Single(await Outbox(running.Host.Services, tenant, HttpTenantA));

        int revision;
        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
            revision = await running.Registry.UnmountAsync(scopeA, "private", 1, "operator-a", Ct);
        Assert.Equal(2, revision);
        await WithinFiveSecondsAsync(async () => { using var response = await http.Get("/private", HttpTenantA); return response.StatusCode == HttpStatusCode.NotFound; });
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Scope == scopeA);
        // 卸载只把同一行改为墓碑（修订继续增长、配置清空），不新增任何开关列或开关行。
        Assert.Equal(("tenant:" + HttpTenantA, (string?)null, (string?)null, 2, true), await PatchRow(HttpTenantA, "private"));
        var events = await Outbox(running.Host.Services, tenant, HttpTenantA);
        Assert.Equal([RegistryEvents.ScopeMounted, RegistryEvents.ScopeUnmounted], events.Select(item => item.Type));
        var removed = AssertEnvelope(events[1].Payload, HttpTenantA);
        Assert.Equal("""{"scope":"tenant:01900000-0000-7000-8000-000000000001","entryId":"private","revision":2}""", removed.Payload.GetRawText());
        Assert.Empty(await Outbox(running.Host.Services, tenant, HttpTenantB));
    }

    [Fact]
    public async Task 卸载平台条目写入禁用patch_再挂载只替换配置_两次都随事务发事件且只影响本租户()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, events: true);
        await using var http = await StartEndpointHost(running, tenant, true);
        var scopeA = new ScopeKey("tenant", HttpTenantA);
        using (var initial = await http.Get("/surface", HttpTenantA)) Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(1, await running.Registry.UnmountAsync(scopeA, "shared", 0, "operator-a", Ct));
        await WithinFiveSecondsAsync(async () => { using var response = await http.Get("/surface", HttpTenantA); return response.StatusCode == HttpStatusCode.NotFound; });
        using (var other = await http.Get("/surface", HttpTenantB)) Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(("tenant:" + HttpTenantA, (string?)null, (string?)null, 1, false), await PatchRow(HttpTenantA, "shared"));
        using (tenant.BeginScope(HttpTenantA))
        {
            var disabled = await running.Registry.GetScopePatchAsync(scopeA, "shared", Ct);
            Assert.True(disabled!.Patch.GetProperty("disabled").GetBoolean());
            // 已卸载的平台条目在本 scope 与不存在相同，再次卸载得到 NOT_FOUND。
            using (EventEnvelopeScope.Begin(Operator))
                await AssertCode(PlatformErrors.NotFound, () => running.Registry.UnmountAsync(scopeA, "shared", 1, "operator-a", Ct).AsTask());
        }

        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(2, (await running.Registry.MountAsync(scopeA, Patch("""{"id":"shared","config":{"label":"A"}}"""), 1, "operator-a", Ct)).Revision);
        await WithinFiveSecondsAsync(async () => { using var response = await http.Get("/surface", HttpTenantA); return response.StatusCode == HttpStatusCode.OK; });
        Assert.Equal("A", running.Label("shared", scopeA));
        var events = await Outbox(running.Host.Services, tenant, HttpTenantA);
        Assert.Equal([RegistryEvents.ScopeUnmounted, RegistryEvents.ScopeMounted], events.Select(item => item.Type));
        Assert.Equal("""{"scope":"tenant:01900000-0000-7000-8000-000000000001","entryId":"shared","plugin":null,"version":null,"revision":2}""",
            AssertEnvelope(events[1].Payload, HttpTenantA).Payload.GetRawText());
    }

    [Fact]
    public async Task 公开写入拒绝跨租户_禁用挂载_未预装或未写版本的组件与旧修订_拒绝时不写patch也不发事件()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, events: true);
        var scopeA = new ScopeKey("tenant", HttpTenantA); var scopeB = new ScopeKey("tenant", HttpTenantB);
        var entry = Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{}}""");
        await AssertCode(PlatformErrors.Unauthenticated, () => running.Registry.MountAsync(scopeA, entry, 0, "operator", Ct).AsTask());
        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
        {
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.MountAsync(scopeB, entry, 0, "operator", Ct).AsTask());
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.MountAsync(new ScopeKey("app", "unproven"), entry, 0, "operator", Ct).AsTask());
            await AssertCode(PlatformErrors.InputInvalid, () => running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","disabled":true}"""), 0, "operator", Ct).AsTask());
            await AssertCode(PlatformErrors.InputInvalid, () => running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator", Ct).AsTask());
            await AssertCode(PlatformErrors.InputInvalid, () => running.Registry.MountAsync(scopeA, Patch("""{"id":"unbound","plugin":"test.unbound@1.0.0","config":{}}"""), 0, "operator", Ct).AsTask());
            // 配置不符合组件 config.schema 由共用装配校验拒绝。
            await AssertCode(PlatformErrors.InputInvalid, () => running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{"label":42}}"""), 0, "operator", Ct).AsTask());
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.UnmountAsync(scopeA, "private", 0, "operator", Ct).AsTask());
            Assert.Equal(1, (await running.Registry.MountAsync(scopeA, entry, 0, "operator", Ct)).Revision);
            await AssertCode(RegistryErrors.RevisionConflict, () => running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{"label":"new"}}"""), 0, "operator", Ct).AsTask());
            await AssertCode(RegistryErrors.RevisionConflict, () => running.Registry.UnmountAsync(scopeA, "private", 7, "operator", Ct).AsTask());
        }
        Assert.Single(await Outbox(running.Host.Services, tenant, HttpTenantA));
        Assert.Equal(1, (await PatchRow(HttpTenantA, "private")).Revision);
        Assert.Empty(await Outbox(running.Host.Services, tenant, HttpTenantB));
    }

    [Fact]
    public async Task 宿主漏装注册表事件处理器时公开写入在提交前失败_不留patch也不留Outbox()
    {
        var tenant = new AsyncLocalTenantContext();
        // 装了 Mediator 与信封写入端，却没有登记 AddQianRegistryEvents。
        await using var running = await CreateRunningRegistry(tenant, services: services =>
        {
            services.AddKunMediator(_ => { }); services.AddQianEvents();
        });
        var scopeA = new ScopeKey("tenant", HttpTenantA);
        using (tenant.BeginScope(HttpTenantA))
        using (EventEnvelopeScope.Begin(Operator))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                running.Registry.MountAsync(scopeA, Patch("""{"id":"private","plugin":"test.private@1.0.0","config":{}}"""), 0, "operator", Ct).AsTask());
            Assert.Contains("AddQianRegistryEvents", error.Message, StringComparison.Ordinal);
            Assert.Null(await running.Registry.GetScopePatchAsync(scopeA, "private", Ct));
        }
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using (var patches = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_registry.scope_patches", admin)) Assert.Equal(0L, await patches.ExecuteScalarAsync(Ct));
        await using var outbox = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_registry.outbox_messages", admin);
        Assert.Equal(0L, await outbox.ExecuteScalarAsync(Ct));
    }

    private async Task<(string Scope, string? Plugin, string? Version, int Revision, bool Deleted)> PatchRow(string tenantId, string entryId)
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT scope, plugin, plugin_version, revision, is_deleted FROM plugin_qian_registry.scope_patches WHERE tenant_id = @tenant AND entry_id = @entry", admin);
        command.Parameters.AddWithValue("tenant", tenantId); command.Parameters.AddWithValue("entry", entryId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4));
    }

    // Outbox 是服务级传输表（不按租户过滤），按行的路由租户筛出某租户的事件。
    private static async Task<IReadOnlyList<(string Type, string Payload)>> Outbox(IServiceProvider services, AsyncLocalTenantContext tenant, string tenantId)
    {
        using var current = tenant.BeginScope(tenantId);
        await using var session = await services.GetRequiredService<IDataService>().OpenAsync<RegistryDbContext>(Ct);
        return (await session.Context.OutboxMessages.AsNoTracking().Where(row => row.TenantId == tenantId).OrderBy(row => row.OccurredAt).ToListAsync(Ct))
            .Select(row => (row.EventTypeId, row.Payload)).ToArray();
    }

    private static QianEventEnvelope AssertEnvelope(string payload, string tenantId)
    {
        var envelope = QianEventEnvelope.Parse(payload);
        Assert.Equal((Guid.Parse(tenantId), "qian.registry"), (envelope.TenantId, envelope.Producer));
        Assert.Equal((EventActorKind.User, "operator-person"), (envelope.Actor.Kind, envelope.Actor.Id));
        using var document = JsonDocument.Parse(payload);
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/s2s-claims.schema.json")), options);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/event-envelope.schema.json")), options);
        Assert.True(schema.Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid);
        return envelope;
    }

    private static async Task WithinFiveSecondsAsync(Func<Task<bool>> ready)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!await ready()) await Task.Delay(20, deadline.Token);
        Assert.True(elapsed.Elapsed <= TimeSpan.FromSeconds(5), $"生效耗时 {elapsed.Elapsed.TotalMilliseconds:F0} ms，超过 5 秒。");
    }
}
