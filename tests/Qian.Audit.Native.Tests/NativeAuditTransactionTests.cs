using System.Data;
using System.Security.Cryptography;
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
using Qian.Testing.NativeAudit;

namespace Qian.Audit.Native.Tests;

public sealed partial class NativeAuditStoreTests
{
    [Fact]
    public async Task EF真实CRM外部事务_提交同见_外层回滚同时撤销客户审计和链头()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var native = await NativeAuditTestHost.Create(db, Ct);
        using var business = BusinessHost(native.Tenant, db.TenantConnection); await business.StartAsync(Ct);
        await PrepareBusiness(business, db.Roles.TenantLogin);
        using var scope = native.Tenant.BeginScope(A); var data = business.Services.GetRequiredService<IDataService>();
        var committed = Guid.CreateVersion7(); var rolledBack = Guid.CreateVersion7(); var record = Record();
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var tx = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            Assert.IsType<NpgsqlTransaction>(tx.GetDbTransaction());
            session.Context.Customers.Add(new Customer(committed, "committed")); await session.SaveChangesAsync(Ct);
            Assert.Equal(1, (await native.Audit.AppendEnlistedAsync(record, tx.GetDbTransaction(), Ct)).Sequence);
            Assert.NotNull(tx.GetDbTransaction().Connection); Assert.Empty(await native.Audit.ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal(1, (await native.Audit.AppendEnlistedAsync(record, tx.GetDbTransaction(), Ct)).Sequence);
            await tx.CommitAsync(Ct);
        }
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var tx = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            session.Context.Customers.Add(new Customer(rolledBack, "rolled back")); await session.SaveChangesAsync(Ct);
            Assert.Equal(2, (await native.Audit.AppendEnlistedAsync(Record(), tx.GetDbTransaction(), Ct)).Sequence);
            await tx.RollbackAsync(Ct);
        }
        await using (var verify = await data.OpenAsync<CrmDbContext>(Ct)) Assert.Equal(committed, Assert.Single(await verify.Context.Customers.ToListAsync(Ct)).Id);
        Assert.Equal(record.Id, Assert.Single(await native.Audit.ReadAsync(0, 10, cancellationToken: Ct)).Record.Id);
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await native.Audit.VerifyAsync(Ct));
        await Admin("CREATE FUNCTION plugin_qian_audit.fail_business() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced append failure'; END $$; CREATE TRIGGER fail_business BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.fail_business()");
        await using (var session = await data.OpenAsync<CrmDbContext>(Ct))
        {
            await using var tx = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            session.Context.Customers.Add(new Customer(Guid.CreateVersion7(), "must disappear")); await session.SaveChangesAsync(Ct);
            Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => native.Audit.AppendEnlistedAsync(Record(), tx.GetDbTransaction(), Ct))).SqlState);
            await tx.RollbackAsync(Ct);
        }
        await using (var verify = await data.OpenAsync<CrmDbContext>(Ct)) Assert.Single(await verify.Context.Customers.ToListAsync(Ct));
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await native.Audit.VerifyAsync(Ct));
    }

    [Fact]
    public async Task 外部事务拒绝弱隔离错租户特权与SET_ROLE冒充_不接管调用方事务()
    {
        var db = await NativeAuditDatabase.Create(postgres, Ct); await using var host = await NativeAuditTestHost.Create(db, Ct);
        using var tenant = host.Tenant.BeginScope(A);
        await using var external = new NpgsqlConnection(db.TenantConnection); await external.OpenAsync(Ct);
        await Execute(external, $"SELECT set_config('app.current_tenant_id','{A}',false)");
        await using (var weak = await external.BeginTransactionAsync(IsolationLevel.ReadCommitted, Ct))
        { await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendEnlistedAsync(Record(), weak, Ct)); Assert.NotNull(weak.Connection); await weak.RollbackAsync(Ct); }
        await using (var tx = await external.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            await Execute(external, $"SELECT set_config('app.current_tenant_id','{B}',true)");
            await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendEnlistedAsync(Record(), tx, Ct));
            await Execute(external, $"SELECT set_config('app.current_tenant_id','{A}',true)");
            await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendEnlistedAsync(Record(B), tx, Ct));
            Assert.Equal(1, (await host.Audit.AppendEnlistedAsync(Record(), tx, Ct)).Sequence); await tx.RollbackAsync(Ct);
        }
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(Ct); await Execute(admin, $"SELECT set_config('app.current_tenant_id','{A}',false)");
            await using var tx = await admin.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendEnlistedAsync(Record(), tx, Ct));
        }
        await Admin($"GRANT {db.Roles.TenantLogin} TO qian_app");
        await using var impersonator = new NpgsqlConnection(postgres.AppConnectionString); await impersonator.OpenAsync(Ct);
        await Execute(impersonator, $"SET ROLE {db.Roles.TenantLogin}; SELECT set_config('app.current_tenant_id','{A}',false)");
        await using var forged = await impersonator.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
        await Assert.ThrowsAsync<QianErrorException>(() => host.Audit.AppendEnlistedAsync(Record(), forged, Ct));
        Assert.Equal(0L, await Scalar(impersonator, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
        Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => Execute(impersonator, "SELECT * FROM plugin_qian_audit.native_tip(gen_random_uuid())"))).SqlState);
    }

    [Fact]
    public async Task 既有EF模型和历史链_原生迁移保持旧角色权限与平台边界_新旧双向读写验链()
    {
        var role = "audit_legacy_" + Guid.NewGuid().ToString("N")[..10]; var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"CREATE ROLE {role} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'");
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = role, Password = password }.ConnectionString;
        var tenant = new AsyncLocalTenantContext(); var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(AuditDbContext.Schema));
        using var oldHost = builder.Build(); await oldHost.StartAsync(Ct);
        using (var scope = oldHost.Services.CreateScope())
        {
            var sql = Migration(scope.ServiceProvider.GetRequiredService<AuditDbContext>());
            await Admin("DROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;" + sql + "GRANT USAGE ON SCHEMA plugin_qian_audit TO qian_app; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA plugin_qian_audit TO qian_app");
        }
        await Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        await using var loader = new PluginLoader(oldHost.Services);
        await loader.MountAsync([
            NativeAuditTestHost.Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key], ScopeKey.Global, Json("{}")),
            NativeAuditTestHost.Define("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key], ScopeKey.Global, Json($"{{\"chain\":{{\"store\":\"pg\",\"platformRole\":\"{role}\"}}}}"))], Ct);
        Assert.True(loader.GetState("audit", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("audit", ScopeKey.Global)?.ToString());
        var caller = loader.GetContext("audit", ScopeKey.Global); var old = caller.Audit();
        using (tenant.BeginScope(A)) await old.AppendAsync(Record(), Ct);
        await old.AppendPlatformAsync(caller, Record(null), Ct);
        var aclSql = $"SELECT string_agg(c.relname||':'||r.rolname||':'||a.privilege_type||':'||a.is_grantable,'|' ORDER BY c.relname,r.rolname,a.privilege_type) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace CROSS JOIN LATERAL aclexplode(c.relacl) a JOIN pg_roles r ON r.oid=a.grantee WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND r.rolname IN ('qian_app','{role}')";
        var before = (string)(await AdminScalar(aclSql))!;
        var db = await NativeAuditDatabase.Create(postgres, Ct, reset: false);
        await using var native = await NativeAuditTestHost.Create(db, Ct);
        using (var migration = new NpgsqlConnection(postgres.ConnectionString)) { await migration.OpenAsync(Ct); await NativeAuditStorage.MigrateAsync(migration, db.Roles, Ct); }
        var after = (string)(await AdminScalar(aclSql))!;
        Assert.Equal(before, after);
        using (tenant.BeginScope(A)) using (native.Tenant.BeginScope(A))
        {
            Assert.Single(await native.Audit.ReadAsync(0, 10, cancellationToken: Ct));
            await native.Audit.AppendAsync(Record(), Ct); Assert.Equal(2, (await old.VerifyAsync(Ct)).CheckedEntries);
            await old.AppendAsync(Record(), Ct); Assert.Equal(3, (await native.Audit.VerifyAsync(Ct)).CheckedEntries);
            var left = await old.ReadAsync(0, 10, cancellationToken: Ct); var right = await native.Audit.ReadAsync(0, 10, cancellationToken: Ct);
            Assert.Equal(left.Select(e => e.ContentHash), right.Select(e => e.ContentHash));
            Assert.All(right, row => Assert.Equal(AuditProtocol.Content(left.Single(e => e.Record.Id == row.Record.Id).Record, row.Sequence), AuditProtocol.Content(row.Record, row.Sequence)));
        }
        await native.Audit.AppendPlatformAsync(native.Caller, Record(null), Ct);
        Assert.Equal(2, (await old.VerifyPlatformAsync(caller, Ct)).CheckedEntries);
        await old.AppendPlatformAsync(caller, Record(null), Ct);
        Assert.Equal(3, (await native.Audit.VerifyPlatformAsync(native.Caller, Ct)).CheckedEntries);
        await using var oldPlatform = new NpgsqlConnection(connection); await oldPlatform.OpenAsync(Ct);
        await Execute(oldPlatform, $"SELECT set_config('app.current_tenant_id','{A}',false)");
        Assert.Equal(0L, await Scalar(oldPlatform, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
        await using var oldTenant = new NpgsqlConnection(postgres.AppConnectionString); await oldTenant.OpenAsync(Ct);
        Assert.Equal(0L, await Scalar(oldTenant, "SELECT count(*) FROM plugin_qian_audit.audit_entries"));
        // 原 EF 租户权限保持原样，不能据此把 AUD-007 的旧 UPDATE/DELETE 缺口虚报为已关闭。
        Assert.Equal(true, await Scalar(oldTenant, "SELECT has_table_privilege(current_user,'plugin_qian_audit.audit_entries','UPDATE') AND has_table_privilege(current_user,'plugin_qian_audit.audit_entries','DELETE')"));
    }
    private static IHost BusinessHost(AsyncLocalTenantContext tenant, string connection)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(); builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<CrmDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(CrmDbContext.Schema)); return builder.Build();
    }
    private async Task PrepareBusiness(IHost host, string role)
    {
        using var scope = host.Services.CreateScope();
        await Admin("DROP SCHEMA IF EXISTS plugin_qian_sample_crm CASCADE;" + Migration(scope.ServiceProvider.GetRequiredService<CrmDbContext>()) +
            $"GRANT USAGE ON SCHEMA plugin_qian_sample_crm TO {role}; GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA plugin_qian_sample_crm TO {role}");
    }
    private static string Migration(DbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model; var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        return string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
    }
}
