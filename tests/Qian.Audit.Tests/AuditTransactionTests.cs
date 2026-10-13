using System.Data;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Sample.Crm;

namespace Qian.Audit.Tests;

public sealed partial class AuditStoreTests
{
    [Fact]
    public async Task 受限CRM上下文和审计共用事务_提交同时可见_回滚不留客户记录和链头()
    {
        var tenant = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var host = BusinessHost(tenant, connection); await host.StartAsync(Ct); await PrepareBusiness(host, role);
        await using var runtime = await MountAudit(host); var audit = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        using var scope = tenant.BeginScope(A); var data = host.Services.GetRequiredService<IDataService>();
        var committed = Guid.CreateVersion7(); var rolledBack = Guid.CreateVersion7(); var record = CanonicalizerTests.Record();
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            session.Context.Customers.Add(new Customer(committed, "saved")); await session.SaveChangesAsync(Ct);
            Assert.Equal(1, (await audit.AppendEnlistedAsync(record, transaction.GetDbTransaction(), Ct)).Sequence);
            Assert.Empty(await audit.ReadAsync(0, 10, cancellationToken: Ct));
            await transaction.CommitAsync(Ct);
        }
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            session.Context.Customers.Add(new Customer(rolledBack, "rollback")); await session.SaveChangesAsync(Ct);
            Assert.Equal(2, (await audit.AppendEnlistedAsync(CanonicalizerTests.Record(), transaction.GetDbTransaction(), Ct)).Sequence);
            await transaction.RollbackAsync(Ct);
        }
        await using var verify = await data.OpenAsync<CrmDbContext>(Ct);
        Assert.Equal(committed, Assert.Single(await verify.Context.Customers.ToListAsync(Ct)).Id);
        Assert.Equal(record.Id, Assert.Single(await audit.ReadAsync(0, 10, cancellationToken: Ct)).Record.Id);
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await audit.VerifyAsync(Ct));
        Assert.Equal(2, (await audit.AppendAsync(CanonicalizerTests.Record(), Ct)).Sequence);
    }

    [Fact]
    public async Task 审计插入失败会使已写入的真实CRM事务整体回滚()
    {
        var tenant = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var host = BusinessHost(tenant, connection); await host.StartAsync(Ct); await PrepareBusiness(host, role);
        await using var runtime = await MountAudit(host); var audit = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        await Admin("CREATE FUNCTION plugin_qian_audit.reject_transaction() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test transaction failure'; END $$; CREATE TRIGGER reject_transaction BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_transaction();");
        using var scope = tenant.BeginScope(A); var data = host.Services.GetRequiredService<IDataService>();
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            session.Context.Customers.Add(new Customer(Guid.CreateVersion7(), "must disappear")); await session.SaveChangesAsync(Ct);
            Assert.Equal(PostgresErrorCodes.RaiseException, (await Assert.ThrowsAsync<PostgresException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(), transaction.GetDbTransaction(), Ct))).SqlState);
            await transaction.RollbackAsync(Ct);
        }
        await using var verify = await data.OpenAsync<CrmDbContext>(Ct);
        Assert.Empty(await verify.Context.Customers.ToListAsync(Ct)); Assert.Empty(await audit.ReadAsync(0, 10, cancellationToken: Ct));
        Assert.Equal(new AuditChainVerification(true, 0, null, null), await audit.VerifyAsync(Ct));
        await Admin("DROP TRIGGER reject_transaction ON plugin_qian_audit.audit_entries;");
        Assert.Equal(1, (await audit.AppendAsync(CanonicalizerTests.Record(), Ct)).Sequence);
    }

    [Fact]
    public async Task 业务登录只能调用审计函数_无表权限_错误事务和租户均拒绝()
    {
        var tenant = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var host = BusinessHost(tenant, connection); await host.StartAsync(Ct); await PrepareBusiness(host, role);
        await using var runtime = await MountAudit(host); var audit = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        await using var app = new NpgsqlConnection(connection); await app.OpenAsync(Ct);
        foreach (var sql in new[] { "SELECT * FROM plugin_qian_audit.audit_entries", "UPDATE plugin_qian_audit.audit_entries SET result = 'forged'", "DELETE FROM plugin_qian_audit.audit_entries", "TRUNCATE plugin_qian_audit.audit_entries", "INSERT INTO plugin_qian_audit.audit_chain_state(chain_key, tenant_id, tip_seq) VALUES ('tenant:x', 'x', 0)" })
        {
            await using var direct = new NpgsqlCommand(sql, app);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => direct.ExecuteNonQueryAsync(Ct))).SqlState);
        }
        using var scope = tenant.BeginScope(A);
        await using (var weak = await app.BeginTransactionAsync(IsolationLevel.ReadCommitted, Ct))
        {
            Assert.Equal("INPUT_INVALID", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(), weak, Ct))).Error.Code.Code);
            await SetTenant(app, weak, A);
            await using var raw = new NpgsqlCommand("SELECT * FROM plugin_qian_audit.transaction_tip(@id)", app, weak);
            raw.Parameters.AddWithValue("id", Guid.CreateVersion7());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => raw.ExecuteNonQueryAsync(Ct))).SqlState);
            await weak.RollbackAsync(Ct);
        }
        await using (var transaction = await app.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(), transaction, Ct))).Error.Code.Code);
            await SetTenant(app, transaction, B);
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(), transaction, Ct))).Error.Code.Code);
            await SetTenant(app, transaction, A);
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(tenant: B), transaction, Ct))).Error.Code.Code);
            await transaction.RollbackAsync(Ct);
        }
        await using (var transaction = await app.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            await SetTenant(app, transaction, A);
            await using var malformed = new NpgsqlCommand("SELECT plugin_qian_audit.transaction_append('{}', NULL, @hash)", app, transaction);
            malformed.Parameters.AddWithValue("hash", new string('a', 64));
            await Assert.ThrowsAsync<PostgresException>(() => malformed.ExecuteNonQueryAsync(Ct));
            await transaction.RollbackAsync(Ct);
        }
        Assert.Equal(new AuditChainVerification(true, 0, null, null), await audit.VerifyAsync(Ct));
    }

    [Fact]
    public async Task 数据库函数拒绝损坏摘要和错误序号_未授权登录无法调用()
    {
        var tenant = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var host = BusinessHost(tenant, connection); await host.StartAsync(Ct); await PrepareBusiness(host, role);
        await using var app = new NpgsqlConnection(connection); await app.OpenAsync(Ct);
        foreach (var wrongHash in new[] { true, false })
        {
            await using var transaction = await app.BeginTransactionAsync(IsolationLevel.Serializable, Ct); await SetTenant(app, transaction, A);
            var record = CanonicalizerTests.Record(); var sequence = wrongHash ? 1 : 2;
            await using var command = new NpgsqlCommand("SELECT plugin_qian_audit.transaction_append(@body, NULL, @hash)", app, transaction);
            command.Parameters.AddWithValue("body", AuditCanonicalizer.Content(record, sequence));
            command.Parameters.AddWithValue("hash", wrongHash ? new string('0', 64) : AuditCanonicalizer.Hash(record, sequence, null));
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
            Assert.Equal(wrongHash ? PostgresErrorCodes.CheckViolation : PostgresErrorCodes.SerializationFailure, error.SqlState);
            await transaction.RollbackAsync(Ct);
        }
        await using var untrusted = new NpgsqlConnection(postgres.AppConnectionString); await untrusted.OpenAsync(Ct);
        await using var denied = new NpgsqlCommand("SELECT * FROM plugin_qian_audit.transaction_tip(@id)", untrusted);
        denied.Parameters.AddWithValue("id", Guid.CreateVersion7());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteNonQueryAsync(Ct))).SqlState);
    }

    [Fact]
    public async Task 事务追加与独立追加使用同一链_幂等重试不重写_损坏链头拒绝()
    {
        var tenant = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var host = BusinessHost(tenant, connection); await host.StartAsync(Ct); await PrepareBusiness(host, role);
        await using var runtime = await MountAudit(host); var audit = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        using var scope = tenant.BeginScope(A); var data = host.Services.GetRequiredService<IDataService>();
        var first = await audit.AppendAsync(CanonicalizerTests.Record(payload: "{\"long\":9007199254740993}"), Ct);
        var record = CanonicalizerTests.Record(payload: "{\"unicode\":\"客户\",\"precise\":1.25}");
        for (var i = 0; i < 2; i++)
        {
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            var second = await audit.AppendEnlistedAsync(record, transaction.GetDbTransaction(), Ct);
            Assert.Equal(2, second.Sequence); Assert.Equal(first.ContentHash, second.PreviousHash);
            Assert.Equal(AuditCanonicalizer.Hash(record, 2, first.ContentHash), second.ContentHash);
            await transaction.CommitAsync(Ct);
        }
        Assert.Equal(new AuditChainVerification(true, 2, null, null), await audit.VerifyAsync(Ct));
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(id: record.Id), transaction.GetDbTransaction(), Ct))).Error.Code.Code);
            await transaction.RollbackAsync(Ct);
        }
        await Admin("UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = 8;");
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.Equal("CONFLICT", (await Assert.ThrowsAsync<QianErrorException>(() => audit.AppendEnlistedAsync(CanonicalizerTests.Record(), transaction.GetDbTransaction(), Ct))).Error.Code.Code);
            await transaction.RollbackAsync(Ct);
        }
    }

    [Fact]
    public async Task 两宿主业务事务争用链头_序列化失败重试整笔业务_客户与审计一一对应()
    {
        var tenantA = new AsyncLocalTenantContext(); var tenantB = new AsyncLocalTenantContext(); var (connection, role) = await CreateBusinessRole();
        using var hostA = BusinessHost(tenantA, connection); using var hostB = BusinessHost(tenantB, connection);
        await hostA.StartAsync(Ct); await hostB.StartAsync(Ct); await PrepareBusiness(hostA, role);
        await using var runtimeA = await MountAudit(hostA); await using var runtimeB = await MountAudit(hostB);
        var auditA = runtimeA.GetContext("consumer", ScopeKey.Global).Audit(); var auditB = runtimeB.GetContext("consumer", ScopeKey.Global).Audit();
        using (tenantA.BeginScope(A)) await auditA.AppendAsync(CanonicalizerTests.Record(), Ct);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrivals = 0; var conflicts = 0;
        async Task Write(IHost host, AsyncLocalTenantContext tenant, IAuditService audit)
        {
            using var scope = tenant.BeginScope(A); var data = host.Services.GetRequiredService<IDataService>();
            for (var index = 0; index < 8; index++)
            {
                var id = Guid.CreateVersion7(); var record = CanonicalizerTests.Record(id: id);
                for (var attempt = 0; ; attempt++)
                {
                    await using var session = await data.OpenAsync<CrmDbContext>(Ct);
                    await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
                    try
                    {
                        session.Context.Customers.Add(new Customer(id, "concurrent")); await session.SaveChangesAsync(Ct);
                        if (index == 0 && attempt == 0)
                        {
                            if (Interlocked.Increment(ref arrivals) == 2) ready.SetResult();
                            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                        }
                        await audit.AppendEnlistedAsync(record, transaction.GetDbTransaction(), Ct);
                        await transaction.CommitAsync(Ct); break;
                    }
                    catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.SerializationFailure && attempt < 12)
                    {
                        Interlocked.Increment(ref conflicts); await transaction.RollbackAsync(Ct);
                        await Task.Delay(5 * (attempt + 1), Ct);
                    }
                }
            }
        }
        await Task.WhenAll(Write(hostA, tenantA, auditA), Write(hostB, tenantB, auditB));
        Assert.True(conflicts > 0, "必须实际产生旧快照冲突，证明重试的是包含业务写入的整笔事务。");
        using var verifyScope = tenantA.BeginScope(A); await using var verify = await hostA.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
        var customers = await verify.Context.Customers.Select(row => row.Id).ToArrayAsync(Ct); Assert.Equal(16, customers.Length);
        var records = await auditA.ReadAsync(0, 100, cancellationToken: Ct);
        Assert.Equal(customers.Order(), records.Skip(1).Select(row => row.Record.Id).Order());
        Assert.Equal(new AuditChainVerification(true, 17, null, null), await auditA.VerifyAsync(Ct));
    }

    private async Task<(string Connection, string Role)> CreateBusinessRole()
    {
        var role = "qian_crm_" + Guid.NewGuid().ToString("N")[..8]; return (await CreatePlatformRole(role), role);
    }
    private IHost BusinessHost(AsyncLocalTenantContext tenant, string connection)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        builder.Services.AddQianData<CrmDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(CrmDbContext.Schema));
        return builder.Build();
    }
    private async Task PrepareBusiness(IHost host, string role)
    {
        await Prepare(host);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        var owner = "qian_audit_owner_" + Guid.NewGuid().ToString("N")[..8];
        await Admin($"DROP SCHEMA IF EXISTS plugin_qian_sample_crm CASCADE;\n{sql}\nGRANT USAGE ON SCHEMA plugin_qian_sample_crm TO \"{role}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_sample_crm TO \"{role}\"; CREATE ROLE \"{owner}\" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;");
        await Admin("BEGIN;" + AuditTransactionAccess.CreateMigrationSql(owner, role) + "COMMIT;");
    }
    private static async Task<PluginLoader> MountAudit(IHost host)
    {
        var runtime = new PluginLoader(host.Services);
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key]), Define("consumer", new AuditConsumer(), [])], Ct);
        return runtime;
    }
    private static async Task SetTenant(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenant)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, true)", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant); await command.ExecuteNonQueryAsync(Ct);
    }
}
