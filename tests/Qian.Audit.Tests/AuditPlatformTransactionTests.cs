using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Audit.Tests;

// S1-AUD-009：平台链的同事务入口。生产方（受限业务登录，只获 platform_tip / platform_append 的执行权）把平台记录挂进自己的 SERIALIZABLE 事务，
// 函数以独立 NOLOGIN 属主追加；RLS 的平台登录分支不变，属主分支只在无租户 GUC、调用方不是平台登录且属主无成员关系时放行平台行。
public sealed partial class AuditStoreTests
{
    private const string PlatformCallerRefused = "platform audit caller refused";
    private const string PlatformContextRefused = "platform audit requires a serializable transaction without tenant";
    private const string PlatformFunctions = "plugin_qian_audit.platform_tip(uuid), plugin_qian_audit.platform_append(text,text,text)";

    [Fact]
    public async Task 平台记录经同事务入口与业务写入同提交同回滚_与独立追加共用一条平台链_幂等重试不重写_不同内容与损坏链头拒绝()
    {
        await using var platform = await StartPlatformTransaction();
        var audit = platform.Producer.Audit();
        var first = await audit.AppendPlatformAsync(platform.Producer, PlatformRecord(), Ct);
        var committed = PlatformRecord(); var kept = Guid.CreateVersion7();
        await using (var connection = await platform.OpenProducer())
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await Operation(connection, transaction, kept);
            var entry = await audit.AppendPlatformEnlistedAsync(platform.Producer, committed, transaction, Ct);
            Assert.Equal((2L, first.ContentHash, AuditCanonicalizer.Hash(committed, 2, first.ContentHash)), (entry.Sequence, entry.PreviousHash, entry.ContentHash));
            // 外层提交之前，平台登录读不到这一条。
            Assert.Single(await audit.ReadPlatformAsync(platform.Producer, 0, 10, cancellationToken: Ct));
            await transaction.CommitAsync(Ct);
        }
        await using (var connection = await platform.OpenProducer())
        {
            // 显式回滚：业务行与平台条目都不留。
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await Operation(connection, transaction, Guid.CreateVersion7());
            Assert.Equal(3, (await audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct)).Sequence);
            await transaction.RollbackAsync(Ct);
        }
        await Admin("CREATE FUNCTION plugin_qian_audit.reject_platform() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.tenant_id IS NULL THEN RAISE EXCEPTION 'test platform failure'; END IF; RETURN NEW; END $$; " +
            "CREATE TRIGGER reject_platform BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_platform();");
        await using (var connection = await platform.OpenProducer())
        {
            // 平台条目写不进去：已写入的业务行随外层事务一起回滚。
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await Operation(connection, transaction, Guid.CreateVersion7());
            Assert.Equal(PostgresErrorCodes.RaiseException, (await Assert.ThrowsAsync<PostgresException>(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct))).SqlState);
            await transaction.RollbackAsync(Ct);
        }
        await Admin("DROP TRIGGER reject_platform ON plugin_qian_audit.audit_entries;");
        Assert.Equal([kept], await Operations());
        Assert.Equal([first.Record.Id, committed.Id], (await audit.ReadPlatformAsync(platform.Producer, 0, 10, cancellationToken: Ct)).Select(entry => entry.Record.Id));
        // 独立追加接在同一条链上，序号不跳。
        Assert.Equal(3, (await audit.AppendPlatformAsync(platform.Producer, PlatformRecord(), Ct)).Sequence);
        await using (var connection = await platform.OpenProducer())
        {
            // 同一事件 ID、同一内容重试：返回原节点，不重写；同一 ID 换内容：CONFLICT。
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            var replay = await audit.AppendPlatformEnlistedAsync(platform.Producer, committed, transaction, Ct);
            Assert.Equal((2L, first.ContentHash, AuditCanonicalizer.Hash(committed, 2, first.ContentHash)), (replay.Sequence, replay.PreviousHash, replay.ContentHash));
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(committed.Id, "{\"changed\":true}"), transaction, Ct))).Error.Code.Code);
            await transaction.RollbackAsync(Ct);
        }
        Assert.Equal(new AuditChainVerification(true, 3, null, null), await audit.VerifyPlatformAsync(platform.Producer, Ct));
        // 链头被改到不存在的序号：函数核对链头不一致，写入器转成 CONFLICT。
        await Admin("UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = 8 WHERE chain_key = 'platform';");
        await using (var connection = await platform.OpenProducer())
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct))).Error.Code.Code);
            await transaction.RollbackAsync(Ct);
        }
    }

    [Fact]
    public async Task 平台链同事务入口沿用平台生产方检查_拒绝租户上下文租户记录与非可串行化事务_连接带租户切换角色或缺直接执行权时拒绝()
    {
        await using var platform = await StartPlatformTransaction();
        var audit = platform.Producer.Audit();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using (var connection = await platform.OpenProducer())
        {
            await using (var weak = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, Ct))
            {
                Assert.Equal("INPUT_INVALID", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), weak, Ct))).Error.Code.Code);
                await weak.RollbackAsync(Ct);
            }
            var tenantConsumer = new AuditConsumer();
            await platform.Runtime.MountAsync([new PluginDefinition(new PluginEntry("tenant-producer", "sample.tenant-producer", "1.0.0",
                new ScopeKey("tenant", A), 0, JsonSerializer.SerializeToElement(new { })), tenantConsumer, [])], Ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            // 生产方检查与 AppendPlatformAsync 同一份：tenant 生产方即使重新 Scoped 到 global、global 生产方 Scoped 到租户都拒绝。
            foreach (var producer in new[] { tenantConsumer.Context!.Scoped(ScopeKey.Global), platform.Producer.Scoped(new ScopeKey("tenant", A)) })
                await AssertDenied(() => audit.AppendPlatformEnlistedAsync(producer, PlatformRecord(), transaction, Ct));
            // 存在租户上下文、记录带租户、连接带租户 GUC：拒绝。
            using (platform.Tenant.BeginScope(A)) await AssertDenied(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct));
            await AssertDenied(() => audit.AppendPlatformEnlistedAsync(platform.Producer, CanonicalizerTests.Record(), transaction, Ct));
            await SetTenant(connection, transaction, A);
            await AssertDenied(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct));
            await transaction.RollbackAsync(Ct);
        }
        // 连接切换了角色（current_user 不是登录本身）：即使切换到的角色也有执行权，入口照样拒绝。
        var switched = "qian_platform_switch_" + suffix;
        await Admin($"CREATE ROLE \"{switched}\" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS; GRANT \"{switched}\" TO \"{platform.ProducerRole}\"; " +
            $"GRANT USAGE ON SCHEMA plugin_qian_audit TO \"{switched}\"; GRANT EXECUTE ON FUNCTION {PlatformFunctions} TO \"{switched}\";");
        await using (var connection = await platform.OpenProducer())
        {
            await Execute(connection, $"SET ROLE \"{switched}\"");
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await AssertDenied(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct));
            await transaction.RollbackAsync(Ct);
        }
        // 执行权须直接授予受限登录本身：借成员关系继承生产方执行权的登录、没有执行权的登录（宿主漏了三参迁移）、被误授执行权的平台登录、
        // 被直接授予执行权的 BYPASSRLS 登录都在调用函数之前拒绝。
        var member = "qian_platform_member_" + suffix;
        var memberConnection = await CreateLogin(member, "INHERIT NOSUPERUSER NOBYPASSRLS");
        var bypass = "qian_platform_bypass_" + suffix; var bypassConnection = await CreateLogin(bypass, "NOINHERIT NOSUPERUSER BYPASSRLS");
        await Admin($"GRANT \"{platform.ProducerRole}\" TO \"{member}\"; GRANT EXECUTE ON FUNCTION {PlatformFunctions} TO \"{platform.PlatformRole}\"; " +
            $"GRANT USAGE ON SCHEMA plugin_qian_audit TO \"{bypass}\"; GRANT EXECUTE ON FUNCTION {PlatformFunctions} TO \"{bypass}\";");
        foreach (var login in new[] { memberConnection, postgres.AppConnectionString, platform.PlatformConnection, bypassConnection })
        {
            await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(login) { Pooling = false }.ConnectionString); await connection.OpenAsync(Ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await AssertDenied(() => audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct));
            await transaction.RollbackAsync(Ct);
        }
        await Admin($"REVOKE EXECUTE ON FUNCTION {PlatformFunctions} FROM \"{platform.PlatformRole}\";");
        // 审计条目没有 chain.platformRole：入口拒绝。
        await platform.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        await platform.Runtime.MountAsync([Define("audit", new AuditPlugin(), [AuditDefinition.Key])], Ct);
        var ordinary = platform.Runtime.GetContext("consumer", ScopeKey.Global);
        await using (var connection = await platform.OpenProducer())
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await AssertDenied(() => ordinary.Audit().AppendPlatformEnlistedAsync(ordinary, PlatformRecord(), transaction, Ct));
            await transaction.RollbackAsync(Ct);
        }
        Assert.Equal(0L, Convert.ToInt64(await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_entries"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task 平台链函数只认被直接授予执行权的受限登录_平台登录成员登录与BYPASSRLS登录被拒_非可串行化带租户与租户正文均拒绝_两个函数都先取平台链锁()
    {
        await using var platform = await StartPlatformTransaction();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        // 平台登录没有执行权：数据库权限检查直接拒绝。
        await DirectCall(platform.PlatformConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, null);
        // 被误授执行权的平台登录、借成员关系继承执行权的登录、被直接授予执行权的 BYPASSRLS 登录：函数拒绝调用方。
        await Admin($"GRANT EXECUTE ON FUNCTION {PlatformFunctions} TO \"{platform.PlatformRole}\";");
        await DirectCall(platform.PlatformConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformCallerRefused);
        var member = "qian_platform_member_" + suffix; var memberConnection = await CreateLogin(member, "INHERIT NOSUPERUSER NOBYPASSRLS");
        await Admin($"GRANT \"{platform.ProducerRole}\" TO \"{member}\";");
        await DirectCall(memberConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformCallerRefused);
        var bypass = "qian_platform_bypass_" + suffix; var bypassConnection = await CreateLogin(bypass, "NOINHERIT NOSUPERUSER BYPASSRLS");
        await Admin($"GRANT USAGE ON SCHEMA plugin_qian_audit TO \"{bypass}\"; GRANT EXECUTE ON FUNCTION {PlatformFunctions} TO \"{bypass}\";");
        await DirectCall(bypassConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformCallerRefused);
        // 生产方本身：非可串行化事务、带租户 GUC、正文带租户都拒绝；摘要不符、序号或前序哈希不接链头同租户链函数。
        await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformContextRefused, IsolationLevel.ReadCommitted);
        await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformContextRefused, tenant: A);
        await DirectCall(platform.ProducerConnection, CanonicalizerTests.Record(), PostgresErrorCodes.InsufficientPrivilege, "audit tenant boundary mismatch");
        await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.CheckViolation, "invalid audit digest", forgeHash: true);
        await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.SerializationFailure, "audit chain changed", skipSequence: true);
        await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.SerializationFailure, "audit chain changed", forgePrevious: true);
        // 两个函数都先取 qian.audit:platform 事务锁（独立追加的 session 锁用同一个键）、再读写链头行，与独立追加同一锁序，锁持有到事务结束：
        // 单独调用 platform_tip 或 platform_append 之后，另一会话都取不到这把锁，回滚后即可取到。此时平台链为空，下一个序号是 1、没有前序。
        await using (var connection = await OpenLogin(platform.ProducerConnection))
        {
            var record = PlatformRecord();
            await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
            {
                await Scalar(connection, $"SELECT count(*) FROM plugin_qian_audit.platform_tip('{record.Id}')", transaction);
                Assert.False(await PlatformLockFree());
                await transaction.RollbackAsync(Ct);
            }
            Assert.True(await PlatformLockFree());
            await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
            {
                await using (var append = new NpgsqlCommand("SELECT plugin_qian_audit.platform_append(@body, NULL, @hash)", connection, transaction))
                {
                    append.Parameters.AddWithValue("body", AuditCanonicalizer.Content(record, 1));
                    append.Parameters.AddWithValue("hash", AuditCanonicalizer.Hash(record, 1, null));
                    await append.ExecuteNonQueryAsync(Ct);
                }
                Assert.False(await PlatformLockFree());
                await transaction.RollbackAsync(Ct);
            }
            Assert.True(await PlatformLockFree());
        }
        // 函数属主被授予成员关系：函数拒绝所有调用方（属主分支同时关闭）。
        await Admin($"GRANT \"{platform.Writer}\" TO qian_app;");
        try { await DirectCall(platform.ProducerConnection, PlatformRecord(), PostgresErrorCodes.InsufficientPrivilege, PlatformCallerRefused); }
        finally { await Admin($"REVOKE \"{platform.Writer}\" FROM qian_app;"); }
        Assert.Equal(new AuditChainVerification(true, 0, null, null), await platform.Producer.Audit().VerifyPlatformAsync(platform.Producer, Ct));
        // 被直接授予执行权的生产方在可串行化、无租户的事务里可以直接调用函数追加平台记录。
        await DirectCall(platform.ProducerConnection, PlatformRecord(), null, null);
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await platform.Producer.Audit().VerifyPlatformAsync(platform.Producer, Ct));
    }

    [Fact]
    public async Task 应用角色不能直接读写平台记录_借成员关系切换为函数属主也不能_属主分支只在无租户GUC且调用方不是平台登录时放行平台行_平台登录只读追加平台行_迁移拒绝角色复用()
    {
        await using var platform = await StartPlatformTransaction();
        var audit = platform.Producer.Audit();
        await audit.AppendPlatformAsync(platform.Producer, PlatformRecord(), Ct);
        await using (var connection = await platform.OpenProducer())
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal(2, (await audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), transaction, Ct)).Sequence);
            await transaction.CommitAsync(Ct);
        }
        using (platform.Tenant.BeginScope(A)) await audit.AppendAsync(CanonicalizerTests.Record(), Ct);
        // qian_app 持有审计表的 DML 授权（租户审计凭据），但 RLS 不放行平台行：读不到、插不进、改不了也删不掉。
        await using (var app = await OpenLogin(postgres.AppConnectionString))
        {
            Assert.Equal(0L, await Scalar(app, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
            Assert.Equal(0L, await Scalar(app, "SELECT count(*) FROM plugin_qian_audit.audit_chain_state"));
            await AssertRowSecurity(() => Execute(app, ForgedPlatformEntry));
            Assert.Equal(0, await Execute(app, "UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = 0 WHERE chain_key = 'platform'"));
            Assert.Equal(0, await Execute(app, "DELETE FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NULL"));
        }
        // 生产方只有函数执行权，没有审计表权限。
        await using (var producer = await platform.OpenProducer())
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => Scalar(producer, "SELECT count(*) FROM plugin_qian_audit.audit_entries"))).SqlState);
        // 属主分支逐条核对：另建一个属主拥有的 SECURITY DEFINER 探针，绕开平台函数自己的调用方检查，直接看属主身份下 RLS 放行哪些行。
        // 生产方调用且无租户 GUC：只见平台行，见不到租户行；带租户 GUC：属主分支关闭，只剩租户分支放行的该租户行；平台登录调用（session_user 是平台登录）：一行都见不到。
        await Admin("CREATE FUNCTION qian_platform_ops.owner_view(OUT platform_rows bigint, OUT tenant_rows bigint) LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog " +
            "AS 'SELECT count(*) FILTER (WHERE tenant_id IS NULL), count(*) FILTER (WHERE tenant_id IS NOT NULL) FROM plugin_qian_audit.audit_entries'; " +
            $"ALTER FUNCTION qian_platform_ops.owner_view() OWNER TO \"{platform.Writer}\"; REVOKE ALL ON FUNCTION qian_platform_ops.owner_view() FROM PUBLIC; " +
            $"GRANT USAGE ON SCHEMA qian_platform_ops TO \"{platform.PlatformRole}\"; GRANT EXECUTE ON FUNCTION qian_platform_ops.owner_view() TO \"{platform.ProducerRole}\", \"{platform.PlatformRole}\";");
        await using (var producer = await platform.OpenProducer())
        {
            Assert.Equal((2L, 0L), await OwnerView(producer));
            await Execute(producer, $"SELECT set_config('app.current_tenant_id', '{A}', false)");
            Assert.Equal((0L, 1L), await OwnerView(producer));
        }
        await using (var login = await OpenLogin(platform.PlatformConnection)) Assert.Equal((0L, 0L), await OwnerView(login));
        // 把函数属主授给 qian_app：SET ROLE 成属主后属主分支随之关闭，读不到、写不进、改不了平台行；属主有成员关系时迁移也拒绝重跑。
        await Admin($"GRANT \"{platform.Writer}\" TO qian_app;");
        try
        {
            await using var app = await OpenLogin(postgres.AppConnectionString);
            await Execute(app, $"SET ROLE \"{platform.Writer}\"");
            Assert.Equal(0L, await Scalar(app, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NULL"));
            await AssertRowSecurity(() => Execute(app, ForgedPlatformEntry));
            Assert.Equal(0, await Execute(app, "UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = 0 WHERE chain_key = 'platform'"));
            await Assert.ThrowsAsync<PostgresException>(() => Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(platform.PlatformRole, platform.Writer, platform.ProducerRole) + "COMMIT;"));
        }
        finally { await Admin($"REVOKE \"{platform.Writer}\" FROM qian_app;"); }
        // 平台登录：读、验平台链照常（只见平台行，租户行不可见），不能改写或删除历史，设置租户 GUC 后读不到平台行。
        Assert.Equal(new AuditChainVerification(true, 2, null, null), await audit.VerifyPlatformAsync(platform.Producer, Ct));
        await using (var login = await OpenLogin(platform.PlatformConnection))
        {
            Assert.Equal(2L, await Scalar(login, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => Execute(login, "UPDATE plugin_qian_audit.audit_entries SET result = 'forged'"))).SqlState);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => Execute(login, "DELETE FROM plugin_qian_audit.audit_entries"))).SqlState);
            await Execute(login, $"SELECT set_config('app.current_tenant_id', '{A}', false)");
            Assert.Equal(0L, await Scalar(login, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
        }
        // 迁移参数：三个角色两两分离，函数属主须是 NOLOGIN。
        foreach (var (platformRole, writer, producer) in new[] { ("role_a", "role_a", "role_b"), ("role_a", "role_b", "role_a"), ("role_a", "role_b", "role_b") })
            Assert.Throws<ArgumentException>(() => AuditPlatformAccess.CreateMigrationSql(platformRole, writer, producer));
        Assert.Throws<QianErrorException>(() => AuditPlatformAccess.CreateMigrationSql("role_a", "Bad", "role_c"));
        await Assert.ThrowsAsync<PostgresException>(() => Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(platform.PlatformRole, platform.ProducerRole, "qian_app") + "COMMIT;"));
    }

    [Fact]
    public async Task 两个同事务追加持旧快照争用平台链头_后提交者可串行化失败_换新事务重做后链连续()
    {
        await using var platform = await StartPlatformTransaction();
        var audit = platform.Producer.Audit();
        await audit.AppendPlatformAsync(platform.Producer, PlatformRecord(), Ct);
        await using var first = await platform.OpenProducer(); await using var second = await platform.OpenProducer();
        // 两个事务先各自取得快照，再先后追加：后到者的快照早于前者推进的链头，只能整笔重做。
        await using var early = await first.BeginTransactionAsync(IsolationLevel.Serializable, Ct); await Scalar(first, "SELECT 1", early);
        await using var late = await second.BeginTransactionAsync(IsolationLevel.Serializable, Ct); await Scalar(second, "SELECT 1", late);
        Assert.Equal(2, (await audit.AppendPlatformEnlistedAsync(platform.Producer, PlatformRecord(), early, Ct)).Sequence);
        await early.CommitAsync(Ct);
        var record = PlatformRecord();
        Assert.Equal(PostgresErrorCodes.SerializationFailure, (await Assert.ThrowsAsync<PostgresException>(() => audit.AppendPlatformEnlistedAsync(platform.Producer, record, late, Ct))).SqlState);
        await late.RollbackAsync(Ct);
        await using (var retry = await second.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            Assert.Equal(3, (await audit.AppendPlatformEnlistedAsync(platform.Producer, record, retry, Ct)).Sequence);
            await retry.CommitAsync(Ct);
        }
        Assert.Equal(new AuditChainVerification(true, 3, null, null), await audit.VerifyPlatformAsync(platform.Producer, Ct));
    }

    // 平台链同事务入口的宿主：审计租户凭据 qian_app、独立平台审计登录、生产方（受限 NOINHERIT 登录，只获两条链同事务函数的执行权与一张平台业务表的写权）、
    // 独立 NOLOGIN 函数属主；迁移按宿主约定在同一事务里应用租户链函数与平台链三参迁移，global 审计条目配置 chain.platformRole。
    private async Task<PlatformTransactionHost> StartPlatformTransaction()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var platformRole = "qian_audit_platform_" + suffix; var platformConnection = await CreatePlatformRole(platformRole);
        var producerRole = "qian_platform_producer_" + suffix; var producerConnection = await CreatePlatformRole(producerRole);
        var writer = "qian_audit_owner_" + suffix;
        var tenant = new AsyncLocalTenantContext(); var host = PlatformHost(tenant, platformConnection);
        try
        {
            await host.StartAsync(Ct); await Prepare(host);
            await Admin($"CREATE ROLE \"{writer}\" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION; " +
                "DROP SCHEMA IF EXISTS qian_platform_ops CASCADE; CREATE SCHEMA qian_platform_ops; CREATE TABLE qian_platform_ops.operations(id uuid PRIMARY KEY); " +
                $"GRANT USAGE ON SCHEMA qian_platform_ops TO \"{producerRole}\"; GRANT SELECT, INSERT ON qian_platform_ops.operations TO \"{producerRole}\";");
            await Admin("BEGIN;" + AuditTransactionAccess.CreateMigrationSql(writer, producerRole) + AuditPlatformAccess.CreateMigrationSql(platformRole, writer, producerRole) + "COMMIT;");
            var runtime = new PluginLoader(host.Services); var consumer = new AuditConsumer();
            await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]),
                Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg", platformRole } }), Define("consumer", consumer, [])], Ct);
            return new PlatformTransactionHost(host, runtime, tenant, consumer, platformRole, NoPooling(platformConnection), writer, producerRole, NoPooling(producerConnection));
        }
        catch { await host.StopAsync(CancellationToken.None); host.Dispose(); throw; }
    }

    private sealed class PlatformTransactionHost(IHost host, PluginLoader runtime, AsyncLocalTenantContext tenant, AuditConsumer consumer,
        string platformRole, string platformConnection, string writer, string producerRole, string producerConnection) : IAsyncDisposable
    {
        internal PluginLoader Runtime => runtime;
        internal AsyncLocalTenantContext Tenant => tenant;
        internal IPluginContext Producer => consumer.Context!;
        internal string PlatformRole => platformRole;
        internal string PlatformConnection => platformConnection;
        internal string Writer => writer;
        internal string ProducerRole => producerRole;
        internal string ProducerConnection => producerConnection;
        internal Task<NpgsqlConnection> OpenProducer() => OpenLogin(producerConnection);
        public async ValueTask DisposeAsync()
        {
            await runtime.DisposeAsync();
            await host.StopAsync(CancellationToken.None); host.Dispose();
        }
    }

    // 绕过写入器直接调用平台链函数：先以 platform_tip 取下一个序号与链头，再以合法正文、前序与摘要调用 platform_append（可伪造摘要、跳号，或伪造前序哈希并据此算出自洽的摘要）；
    // expected 为 null 时须成功并提交，否则断言 SQLSTATE 与（非 null 时的）错误文本。
    private static async Task DirectCall(string login, AuditRecord record, string? state, string? message, IsolationLevel isolation = IsolationLevel.Serializable,
        string? tenant = null, bool forgeHash = false, bool skipSequence = false, bool forgePrevious = false)
    {
        await using var connection = await OpenLogin(login);
        await using var transaction = await connection.BeginTransactionAsync(isolation, Ct);
        if (tenant is not null) await SetTenant(connection, transaction, tenant);
        async Task Call()
        {
            long next; string? previous;
            await using (var tip = new NpgsqlCommand("SELECT next_seq, previous_hash FROM plugin_qian_audit.platform_tip(@id)", connection, transaction))
            {
                tip.Parameters.AddWithValue("id", record.Id);
                await using var reader = await tip.ExecuteReaderAsync(Ct);
                Assert.True(await reader.ReadAsync(Ct));
                next = reader.GetInt64(0); previous = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
            var sequence = skipSequence ? next + 1 : next;
            if (forgePrevious) previous = new string('f', 64);
            await using var append = new NpgsqlCommand("SELECT plugin_qian_audit.platform_append(@body, @previous, @hash)", connection, transaction);
            append.Parameters.AddWithValue("body", AuditCanonicalizer.Content(record, sequence));
            append.Parameters.AddWithValue("previous", NpgsqlDbType.Text, (object?)previous ?? DBNull.Value);
            append.Parameters.AddWithValue("hash", forgeHash ? new string('0', 64) : AuditCanonicalizer.Hash(record, sequence, previous));
            await append.ExecuteNonQueryAsync(Ct);
        }
        if (state is null) { await Call(); await transaction.CommitAsync(Ct); return; }
        var error = await Assert.ThrowsAsync<PostgresException>(Call);
        Assert.Equal(state, error.SqlState);
        if (message is not null) Assert.Equal(message, error.MessageText);
        await transaction.RollbackAsync(Ct);
    }

    // 测试专用探针：以平台函数的属主身份统计审计表里 RLS 放行的平台行与租户行（属主分支的 RLS 用例在 qian_platform_ops 里建，随该 schema 每例重建）。
    private static async Task<(long Platform, long Tenant)> OwnerView(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT platform_rows, tenant_rows FROM qian_platform_ops.owner_view()", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private const string ForgedPlatformEntry = """
        INSERT INTO plugin_qian_audit.audit_entries
          (id, tenant_id, chain_key, seq, prev_hash, content_hash, actor_type, actor_id, on_behalf_of, capability_id, contract_version, input_hash, channel, resource, result, trace_id, occurred_at, payload)
        VALUES (pg_catalog.gen_random_uuid(), NULL, 'platform', 99, NULL, md5(random()::text) || md5(random()::text), 'service', 'forged', NULL, 'forged.operation', '1.0.0',
          repeat('1', 64), 'api', NULL, 'success', repeat('2', 32), now(), '{}'::jsonb)
        """;

    private static AuditRecord PlatformRecord(Guid id, string payload) => new(id, null, "service", "platform-audit-test", null,
        "platform.operation", "1.0.0", new string('1', 64), "api", null, "success", new string('2', 32), DateTimeOffset.UtcNow, CanonicalizerTests.Json(payload));

    private static async Task AssertDenied(Func<Task> call) =>
        Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(call)).Error.Code.Code);

    private static async Task AssertRowSecurity(Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(call);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        Assert.Contains("row-level security", error.MessageText, StringComparison.Ordinal);
    }

    private async Task<string> CreateLogin(string role, string attributes)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"CREATE ROLE \"{role}\" LOGIN {attributes} NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
        return NoPooling(new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString);
    }

    private static string NoPooling(string connection) => new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString;

    private static async Task<NpgsqlConnection> OpenLogin(string connection)
    {
        var opened = new NpgsqlConnection(NoPooling(connection)); await opened.OpenAsync(Ct); return opened;
    }

    private static async Task Operation(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id)
    {
        await using var insert = new NpgsqlCommand("INSERT INTO qian_platform_ops.operations(id) VALUES (@id)", connection, transaction);
        insert.Parameters.AddWithValue("id", id); await insert.ExecuteNonQueryAsync(Ct);
    }

    private async Task<Guid[]> Operations()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT id FROM qian_platform_ops.operations ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var ids = new List<Guid>(); while (await reader.ReadAsync(Ct)) ids.Add(reader.GetGuid(0));
        return [.. ids];
    }

    // 另开管理会话试取 qian.audit:platform 锁键（独立追加的 session 锁用同一个键），取到即刻释放；返回是否取到。
    private async Task<bool> PlatformLockFree() => await AdminScalar(
        "SELECT CASE WHEN pg_try_advisory_lock(hashtextextended('qian.audit:platform', 0)) THEN pg_advisory_unlock(hashtextextended('qian.audit:platform', 0)) ELSE false END") is true;

    private async Task<object?> AdminScalar(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteScalarAsync(Ct);
    }

    private static async Task<object?> Scalar(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction); return await command.ExecuteScalarAsync(Ct);
    }

    private static async Task<int> Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteNonQueryAsync(Ct);
    }
}
