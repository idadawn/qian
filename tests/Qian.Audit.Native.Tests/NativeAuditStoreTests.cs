using System.Data;
using System.Data.Common;
using System.Text.Json;
using Npgsql;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;
using Qian.Testing.NativeAudit;

namespace Qian.Audit.Native.Tests;

public sealed partial class NativeAuditStoreTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string A = "01900000-0000-7000-8000-000000000001";
    private const string B = "01900000-0000-7000-8000-000000000002";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static AuditRecord Record(string? tenant = A, Guid? id = null, string payload = "{\"z\":1e3,\"huge\":9007199254740993,\"a\":1.00}") => new(
        id ?? Guid.CreateVersion7(), tenant, "agent", "native-audit-agent", "person-a", "crm.customer.create", "1.0.0", new string('1', 64),
        "api", "customer:1", "success", new string('2', 32), new DateTimeOffset(2026, 10, 23, 1, 2, 3, TimeSpan.FromHours(8)), Json(payload));
    private static JsonElement Json(string value) { using var doc = JsonDocument.Parse(value); return doc.RootElement.Clone(); }

    [Fact]
    public async Task 两真实宿主并发追加_分页验链_同ID幂等冲突_历史篡改定位()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct);
        await using var first = await NativeAuditTestHost.Create(db, Ct);
        await using var second = await NativeAuditTestHost.Create(db, Ct);
        Assert.NotSame(first.Host, second.Host); Assert.NotSame(first.Data, second.Data);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrivals = 0;
        async Task Append(NativeAuditTestHost host)
        {
            using var tenant = host.Tenant.BeginScope(A);
            if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await Parallel.ForEachAsync(Enumerable.Range(0, 80), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = Ct },
                async (_, ct) => await host.Audit.AppendAsync(Record(), ct));
        }
        await Task.WhenAll(Append(first), Append(second));
        using var scope = first.Tenant.BeginScope(A);
        var page1 = await first.Audit.ReadAsync(0, 73, "person-a", Ct);
        var page2 = await first.Audit.ReadAsync(page1[^1].Sequence, 100, cancellationToken: Ct);
        var rows = page1.Concat(page2).ToArray(); Assert.Equal(160, rows.Length);
        Assert.Equal(Enumerable.Range(1, 160).Select(i => (long)i), rows.Select(e => e.Sequence));
        for (var index = 0; index < rows.Length; index++)
        { Assert.Equal(index == 0 ? null : rows[index - 1].ContentHash, rows[index].PreviousHash); Assert.Equal(AuditCanonicalizer.Hash(rows[index].Record, rows[index].Sequence, rows[index].PreviousHash), rows[index].ContentHash); }
        Assert.Equal(new AuditChainVerification(true, 160, null, null), await first.Audit.VerifyAsync(Ct));
        Assert.Equal(rows[0].ContentHash, (await first.Audit.AppendAsync(rows[0].Record, Ct)).ContentHash);
        Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => first.Audit.AppendAsync(Record(id: rows[0].Record.Id, payload: "{\"changed\":true}"), Ct))).Error.Code.Code);
        await Admin("UPDATE plugin_qian_audit.audit_entries SET payload='{\"tampered\":true}' WHERE seq=130");
        Assert.Equal(new AuditChainVerification(false, 129, 130, "content_mismatch"), await first.Audit.VerifyAsync(Ct));
    }

    [Fact]
    public async Task 插入失败原子回滚链头_有界重试只覆盖自有事务()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct, maxAttempts: 3);
        using var tenant = host.Tenant.BeginScope(A); await host.Audit.AppendAsync(Record(), Ct);
        await Admin("""
            CREATE SEQUENCE plugin_qian_audit.attempts;
            CREATE FUNCTION plugin_qian_audit.reject_append() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM nextval('plugin_qian_audit.attempts'); RAISE EXCEPTION 'forced serialization conflict' USING ERRCODE='40001'; END $$;
            CREATE TRIGGER reject_append BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_append();
            """ + $"GRANT USAGE ON SEQUENCE plugin_qian_audit.attempts TO {db.Roles.FunctionOwner};");
        var failed = await Assert.ThrowsAsync<PostgresException>(() => host.Audit.AppendAsync(Record(), Ct)); Assert.Equal("40001", failed.SqlState);
        Assert.Equal(3L, await AdminScalar("SELECT last_value FROM plugin_qian_audit.attempts"));
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.Audit.VerifyAsync(Ct));
        await using (var session = await host.Data.OpenTenantAsync("audit_tenant", Ct))
        {
            var tx = await session.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal("40001", (await Assert.ThrowsAsync<PostgresException>(() => host.Audit.AppendEnlistedAsync(Record(), tx, Ct))).SqlState);
            Assert.Equal(4L, await AdminScalar("SELECT last_value FROM plugin_qian_audit.attempts"));
            await session.RollbackAsync(Ct);
        }
        await Admin("DROP TRIGGER reject_append ON plugin_qian_audit.audit_entries");
        Assert.Equal(2, (await host.Audit.AppendAsync(Record(), Ct)).Sequence);
        await Admin("UPDATE plugin_qian_audit.audit_chain_state SET tip_seq=50");
        Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendAsync(Record(), Ct))).Error.Code.Code);
        Assert.Equal("tip_mismatch", (await host.Audit.VerifyAsync(Ct)).Reason);
    }

    [Fact]
    public async Task 原生租户和平台只追加_原始SQL改删回拨与跨租户全部拒绝()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct);
        await host.Audit.AppendPlatformAsync(host.Caller, Record(null), Ct);
        using (host.Tenant.BeginScope(A))
        { await host.Audit.AppendAsync(Record(), Ct); await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendAsync(Record(B), Ct));
          await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendPlatformAsync(host.Caller, Record(null), Ct)); }
        using (host.Tenant.BeginScope(B)) { Assert.Empty(await host.Audit.ReadAsync(0, 10, cancellationToken: Ct)); await host.Audit.AppendAsync(Record(B), Ct); }
        Assert.Single(await host.Audit.ReadPlatformAsync(host.Caller, 0, 10, cancellationToken: Ct));
        Assert.True((await host.Audit.VerifyPlatformAsync(host.Caller, Ct)).Valid);
        foreach (var connection in new[] { db.TenantConnection, db.PlatformConnection })
        {
            await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync(Ct);
            var platform = connection == db.PlatformConnection;
            if (!platform) await Execute(sql, $"SELECT set_config('app.current_tenant_id','{A}',false)");
            Assert.Equal(1L, await Scalar(sql, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
            Assert.Equal(0L, await Scalar(sql, $"SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id='{B}'"));
            foreach (var statement in new[] {
                "UPDATE plugin_qian_audit.audit_entries SET result='forged'", "DELETE FROM plugin_qian_audit.audit_entries",
                "TRUNCATE plugin_qian_audit.audit_entries", "UPDATE plugin_qian_audit.audit_chain_state SET tip_seq=0,tip_hash=NULL",
                "DELETE FROM plugin_qian_audit.audit_chain_state", "INSERT INTO plugin_qian_audit.audit_chain_state VALUES ('platform',NULL,0,NULL)",
                $"SET ROLE {db.Roles.FunctionOwner}", $"SET ROLE {(platform ? db.Roles.TenantLogin : db.Roles.PlatformLogin)}",
                "SELECT plugin_qian_audit.native_context()" })
                Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => Execute(sql, statement))).SqlState);
            // 即便函数可调用，也要求真正的 SERIALIZABLE 事务以及租户匹配。
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => Execute(sql, "SELECT * FROM plugin_qian_audit.native_tip(gen_random_uuid())"))).SqlState);
            if (platform)
            {
                await Execute(sql, $"SELECT set_config('app.current_tenant_id','{A}',false)");
                Assert.Equal(0L, await Scalar(sql, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
                await using var tx = await sql.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
                Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => Execute(sql, "SELECT * FROM plugin_qian_audit.native_tip(gen_random_uuid())"))).SqlState);
            }
            else
            {
                await using var tx = await sql.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
                var wrong = Record(B);
                await using var call = NativeAuditStore.Command(sql, tx, "SELECT plugin_qian_audit.native_append(@body,NULL,@hash)", ("body", AuditProtocol.Content(wrong, 1)), ("hash", AuditProtocol.Hash(wrong, 1, null)));
                Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => call.ExecuteNonQueryAsync(Ct))).SqlState);
            }
        }
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("membership-nologin")]
    [InlineData("extra-update")]
    [InlineData("owner-create")]
    [InlineData("public-execute")]
    [InlineData("strict-function")]
    [InlineData("body-function")]
    [InlineData("guard")]
    [InlineData("no-force")]
    public async Task 在线检查精确拒绝角色策略与函数漂移(string violation)
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct);
        var sql = violation switch
        {
            "membership" => $"GRANT {db.Roles.PlatformLogin} TO qian_app",
            "membership-nologin" => $"CREATE ROLE {db.Roles.FunctionOwner}_member NOLOGIN NOINHERIT; GRANT {db.Roles.PlatformLogin} TO {db.Roles.FunctionOwner}_member; GRANT {db.Roles.FunctionOwner}_member TO qian_app",
            "extra-update" => $"GRANT UPDATE(tenant_id) ON plugin_qian_audit.audit_entries TO {db.Roles.TenantLogin}",
            "owner-create" => $"GRANT CREATE ON SCHEMA plugin_qian_audit TO {db.Roles.FunctionOwner}",
            "public-execute" => "GRANT EXECUTE ON FUNCTION plugin_qian_audit.native_append(text,text,text) TO PUBLIC",
            "strict-function" => "ALTER FUNCTION plugin_qian_audit.native_append(text,text,text) STRICT",
            "body-function" => "CREATE OR REPLACE FUNCTION plugin_qian_audit.native_append(p_body text,p_previous text,p_hash text) RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$ BEGIN RETURN; END $$",
            "guard" => "ALTER POLICY qian_audit_native_guard ON plugin_qian_audit.audit_entries USING(true) WITH CHECK(true)",
            "no-force" => "ALTER TABLE plugin_qian_audit.audit_entries NO FORCE ROW LEVEL SECURITY",
            _ => throw new ArgumentException("未知反例", nameof(violation))
        };
        await Admin(sql);
        using (host.Tenant.BeginScope(A)) await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendAsync(Record(), Ct));
        await host.Loader.UnmountAsync("audit", ScopeKey.Global, Ct); await host.MountAudit(Ct);
        Assert.Equal(PluginState.Failed, host.Loader.GetState("audit", ScopeKey.Global));
        if (violation is "membership" or "membership-nologin")
        {
            await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
            await Execute(app, $"SET ROLE {db.Roles.PlatformLogin}");
            Assert.Equal(0L, await Scalar(app, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
            await using var tx = await app.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => Execute(app, "SELECT * FROM plugin_qian_audit.native_tip(gen_random_uuid())"))).SqlState);
        }
    }

    [Fact]
    public async Task 表达式扩展的唯一索引不能冒充链序列唯一约束()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct);
        await Admin("DROP INDEX plugin_qian_audit.ix_audit_entries_chain_key_seq; CREATE UNIQUE INDEX ix_audit_entries_chain_key_seq ON plugin_qian_audit.audit_entries(chain_key,seq,(id::text))");
        await host.Loader.UnmountAsync("audit", ScopeKey.Global, Ct); await host.MountAudit(Ct);
        Assert.Equal(PluginState.Failed, host.Loader.GetState("audit", ScopeKey.Global));
    }

    [Fact]
    public async Task global缺平台绑定拒绝_tenant只绑定租户凭据独立安装_卸载确实撤销策略()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct);
        await using (var rejected = await NativeAuditTestHost.Create(db, Ct, platformBinding: false, ready: false))
            Assert.Equal(PluginState.Failed, rejected.Loader.GetState("audit", ScopeKey.Global));
        await using var host = await NativeAuditTestHost.Create(db, Ct, scopedTenant: A, platformBinding: false);
        using var tenant = host.Tenant.BeginScope(A); var service = host.Audit;
        await service.AppendAsync(Record(), Ct); Assert.True((await service.VerifyAsync(Ct)).Valid);
        await Assert.ThrowsAsync<QianErrorException>(() => service.AppendPlatformAsync(host.Caller.Scoped(ScopeKey.Global), Record(null), Ct));
        var observer = host.Loader.GetContext("data", ScopeKey.Global).Scoped(new ScopeKey("tenant", A));
        var sameNameNewDefinition = QianEvent.Define<string, object?>(CommandEvents.Dispatching.Name, DispatchMode.Waterfall);
        Assert.ThrowsAny<Exception>(() => observer.On(sameNameNewDefinition, DispatchMode.Waterfall,
            (Func<string, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next())));
        await host.Loader.UnmountAsync("audit", new ScopeKey("tenant", A), Ct);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.ReadAsync(0, 10, cancellationToken: Ct));
        // Runtime 检查真实 listener 集合，不以旧上下文不可见代替撤销。
        using var registration = observer.On(sameNameNewDefinition, DispatchMode.Waterfall,
            (Func<string, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next()));
        Assert.Empty(observer.List(CommandPolicies.AuditKey));
    }

    [Fact]
    public async Task 卸载取消真实advisory锁等待_排空后旧句柄不可用_重新挂载可写()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct);
        using var tenant = host.Tenant.BeginScope(A); var service = host.Audit;
        await using var locker = new NpgsqlConnection(postgres.ConnectionString); await locker.OpenAsync(Ct);
        await Execute(locker, $"SELECT pg_advisory_lock(hashtextextended('qian.audit:tenant:{A}',0))");
        var pending = service.AppendAsync(Record(), Ct);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10); var observed = false;
            while (!observed && DateTime.UtcNow < deadline)
            { observed = (bool)(await Scalar(locker, "SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted)"))!; if (!observed) await Task.Delay(10, Ct); }
            Assert.True(observed); await host.Loader.UnmountAsync("audit", ScopeKey.Global, Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => service.AppendAsync(Record(), Ct));
        }
        finally { await Execute(locker, "SELECT pg_advisory_unlock_all()"); }
        // global 的启动校验在无租户作用域内执行。
        using (host.Tenant.BeginScope(null)) await host.MountAudit(Ct);
        Assert.Equal(PluginState.Ready, host.Loader.GetState("audit", ScopeKey.Global));
        Assert.Equal(1, (await host.Audit.AppendAsync(Record(), Ct)).Sequence);
    }
    private async Task Admin(string sql) { await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct); await Execute(db, sql); }
    private async Task<object?> AdminScalar(string sql) { await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct); return await Scalar(db, sql); }
    private static async Task<int> Execute(DbConnection db, string sql) { await using var command = db.CreateCommand(); command.CommandText = sql; return await command.ExecuteNonQueryAsync(Ct); }
    private static async Task<object?> Scalar(DbConnection db, string sql) { await using var command = db.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(Ct); }
}
