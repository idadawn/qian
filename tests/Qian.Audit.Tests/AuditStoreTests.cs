using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Audit.Tests;

public sealed partial class AuditStoreTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string A = "01900000-0000-7000-8000-000000000001";
    private const string B = "01900000-0000-7000-8000-000000000002";
    private static PostgresAuditStore Store(IHost host, AsyncLocalTenantContext tenant) => new(host.Services.GetRequiredService<IDataService>(), tenant);

    [Fact]
    public async Task 两个真实宿主并发追加一千条_无断号无分叉_重试同一事件幂等()
    {
        var a = new AsyncLocalTenantContext(); var b = new AsyncLocalTenantContext();
        using var first = Host(a); using var second = Host(b); await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        async Task Write(IHost host, AsyncLocalTenantContext tenant)
        {
            using var scope = tenant.BeginScope(A); var store = Store(host, tenant);
            for (var i = 0; i < 500; i++) await store.AppendAsync(CanonicalizerTests.Record(), Ct);
        }
        await Task.WhenAll(Write(first, a), Write(second, b));
        using var scope = a.BeginScope(A); var service = Store(first, a);
        var rows = await service.ReadAsync(0, 1000, null, Ct); Assert.Equal(1000, rows.Count);
        Assert.Equal(Enumerable.Range(1, 1000).Select(i => (long)i), rows.Select(x => x.Sequence));
        Assert.Null(rows[0].PreviousHash); for (var i = 1; i < rows.Count; i++) Assert.Equal(rows[i - 1].ContentHash, rows[i].PreviousHash);
        Assert.Equal(new AuditChainVerification(true, 1000, null, null), await service.VerifyAsync(Ct));
        Assert.Equal(rows[0].ContentHash, (await service.AppendAsync(rows[0].Record, Ct)).ContentHash);
        Assert.Equal(new AuditChainVerification(true, 1000, null, null), await service.VerifyAsync(Ct));
    }

    [Fact]
    public async Task Agent委托字段可分页查询_原生SQL隔离租户_缺租户不能进入平台链()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host); var store = Store(host, tenant);
        Assert.Equal("UNAUTHENTICATED", (await Assert.ThrowsAsync<QianErrorException>(() => store.AppendAsync(CanonicalizerTests.Record(), Ct))).Error.Code.Code);
        using (tenant.BeginScope(A))
        {
            await store.AppendAsync(CanonicalizerTests.Record(actorType: "agent", onBehalfOf: "person-a"), Ct);
            await store.AppendAsync(CanonicalizerTests.Record(onBehalfOf: "person-b"), Ct);
            var entry = Assert.Single(await store.ReadAsync(0, 10, "person-a", Ct)); Assert.Equal("agent", entry.Record.ActorType);
            Assert.Equal("crm.customer.create", entry.Record.CapabilityId); Assert.Equal("1.0.0", entry.Record.ContractVersion);
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => store.AppendAsync(CanonicalizerTests.Record(tenant: B), Ct))).Error.Code.Code);
        }
        using (tenant.BeginScope(B)) { Assert.Empty(await store.ReadAsync(0, 10, null, Ct)); await store.AppendAsync(CanonicalizerTests.Record(tenant: B), Ct); }
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        await using (var empty = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", app)) Assert.Equal(0L, await empty.ExecuteScalarAsync(Ct));
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", app)) { set.Parameters.AddWithValue("tenant", A); await set.ExecuteNonQueryAsync(Ct); }
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", app)) Assert.Equal(2L, await count.ExecuteScalarAsync(Ct));
        await using var cross = new NpgsqlCommand("UPDATE plugin_qian_audit.audit_entries SET tenant_id = @tenant", app); cross.Parameters.AddWithValue("tenant", B);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync(Ct))).SqlState);
    }

    [Fact]
    public async Task 插入失败回滚记录和链头_下一条不跳号_不同内容不能复用事件ID()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host); var store = Store(host, tenant);
        using var scope = tenant.BeginScope(A); var first = await store.AppendAsync(CanonicalizerTests.Record(), Ct);
        await Admin("CREATE FUNCTION plugin_qian_audit.reject_append() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test rollback'; END $$; CREATE TRIGGER reject_append BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_append();");
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AppendAsync(CanonicalizerTests.Record(), Ct));
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await store.VerifyAsync(Ct));
        await Admin("DROP TRIGGER reject_append ON plugin_qian_audit.audit_entries;");
        Assert.Equal(2, (await store.AppendAsync(CanonicalizerTests.Record(), Ct)).Sequence);
        Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => store.AppendAsync(CanonicalizerTests.Record(id: first.Record.Id, payload: "{\"changed\":true}"), Ct))).Error.Code.Code);
    }

    [Fact]
    public async Task 数据库JSONB重排不破坏哈希_篡改历史内容精确定位()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host); var store = Store(host, tenant);
        using var scope = tenant.BeginScope(A);
        for (var i = 0; i < 4; i++) await store.AppendAsync(CanonicalizerTests.Record(payload: "{\"z\":1e3,\"huge\":9007199254740993,\"a\":1.00}"), Ct);
        Assert.Equal(new AuditChainVerification(true, 4, null, null), await store.VerifyAsync(Ct));
        await Admin("UPDATE plugin_qian_audit.audit_entries SET payload = '{\"tampered\":true}'::jsonb WHERE seq = 2;");
        Assert.Equal(new AuditChainVerification(false, 1, 2, "content_mismatch"), await store.VerifyAsync(Ct));
    }
    [Fact]
    public async Task 等待链锁时取消不会留下记录或泄漏锁_损坏链头拒绝继续追加()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var scope = tenant.BeginScope(A); var store = Store(host, tenant);
        await using var holder = new NpgsqlConnection(postgres.ConnectionString); await holder.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", holder))
        { acquire.Parameters.AddWithValue("key", "qian.audit:tenant:" + A); await acquire.ExecuteNonQueryAsync(Ct); }
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var pending = store.AppendAsync(CanonicalizerTests.Record(), canceled.Token);
        try
        {
            var observed = false; var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && !observed)
            {
                await using var inspect = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted)", holder);
                observed = (bool)(await inspect.ExecuteScalarAsync(Ct))!;
                if (!observed) await Task.Delay(20, Ct);
            }
            Assert.True(observed, "必须看到数据库中真实等待 advisory lock 的追加请求后才取消。");
            canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            canceled.Cancel();
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", holder); await release.ExecuteNonQueryAsync(CancellationToken.None);
            try { await pending; } catch (OperationCanceledException) { }
        }
        Assert.Equal(new AuditChainVerification(true, 0, null, null), await store.VerifyAsync(Ct));
        Assert.Equal(1, (await store.AppendAsync(CanonicalizerTests.Record(), Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct)).Sequence);
        await Admin("UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = 9;");
        Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => store.AppendAsync(CanonicalizerTests.Record(), Ct))).Error.Code.Code);
        Assert.Equal("tip_mismatch", (await store.VerifyAsync(Ct)).Reason);
    }

    [Fact]
    public async Task 脱敏策略产生的输入哈希真实落库_原输入没有进入审计记录()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var scope = tenant.BeginScope(A);
        var policy = new AuditInputPolicy(CanonicalizerTests.Json("""{"type":"object","properties":{"name":{"type":"string"},"password":{"type":"string","sensitive":true}}}"""), true);
        var input = CanonicalizerTests.Json("""{"name":"customer","password":"audit-only-private-sentinel"}""");
        var hash = policy.InputHash(input)!;
        var entry = await Store(host, tenant).AppendAsync(CanonicalizerTests.Record(inputHash: hash, payload: "{\"source\":\"capability-metadata\"}"), Ct);
        Assert.Equal(hash, entry.Record.InputHash); Assert.Equal(new AuditChainVerification(true, 1, null, null), await Store(host, tenant).VerifyAsync(Ct));
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<AuditDbContext>(Ct);
        var persisted = await session.Context.Database.SqlQueryRaw<string>("SELECT to_jsonb(e)::text AS \"Value\" FROM plugin_qian_audit.audit_entries e").SingleAsync(Ct);
        Assert.DoesNotContain("audit-only-private-sentinel", persisted); Assert.DoesNotContain("password", persisted);
        Assert.Contains(hash, persisted);
    }

    private async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }
    private IHost Host(Kun.Core.MultiTenancy.ITenantContext? tenant = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<Kun.Core.MultiTenancy.ITenantContext>(tenant ?? new AsyncLocalTenantContext()); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        return builder.Build();
    }
    private async Task Prepare(IHost host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;\n" + sql + "\nGRANT USAGE ON SCHEMA plugin_qian_audit TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_audit TO qian_app;", admin);
        await create.ExecuteNonQueryAsync(Ct);
    }
}
