using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Audit.Tests;

public sealed partial class AuditStoreTests
{
    [Fact]
    public async Task 显式平台角色只访问平台链_租户角色缺上下文仍不能读取平台记录()
    {
        var role = "qian_audit_platform_" + Guid.NewGuid().ToString("N")[..8]; var connection = await CreatePlatformRole(role);
        var tenant = new AsyncLocalTenantContext(); using var host = PlatformHost(tenant, connection); await host.StartAsync(Ct); await Prepare(host);
        await Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        var global = new PostgresAuditStore<PlatformAuditDbContext>(host.Services.GetRequiredService<IDataService>(), tenant, role);
        var local = Store(host, tenant);
        await global.AppendAsync(PlatformRecord(), Ct); Assert.Single(await global.ReadAsync(0, 10, null, Ct));
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await global.VerifyAsync(Ct));
        Assert.Equal("UNAUTHENTICATED", (await Assert.ThrowsAsync<QianErrorException>(() => local.AppendAsync(PlatformRecord(), Ct))).Error.Code.Code);
        using (tenant.BeginScope(A))
        {
            await local.AppendAsync(CanonicalizerTests.Record(), Ct);
            Assert.Equal(A, Assert.Single(await local.ReadAsync(0, 10, null, Ct)).Record.TenantId);
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => global.AppendAsync(PlatformRecord(), Ct))).Error.Code.Code);
        }
        Assert.Equal(1, (await global.VerifyAsync(Ct)).CheckedEntries);
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        await using (var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", app)) Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", app)) { set.Parameters.AddWithValue("tenant", A); await set.ExecuteNonQueryAsync(Ct); }
        await using (var read = new NpgsqlCommand("SELECT tenant_id FROM plugin_qian_audit.audit_entries", app)) Assert.Equal(A, await read.ExecuteScalarAsync(Ct));
        await using (var cross = new NpgsqlCommand("INSERT INTO plugin_qian_audit.audit_chain_state(chain_key, tenant_id, tip_seq) VALUES ('platform', NULL, 0)", app))
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync(Ct))).SqlState);
        await using var platform = new NpgsqlConnection(connection); await platform.OpenAsync(Ct);
        await using (var cross = new NpgsqlCommand("INSERT INTO plugin_qian_audit.audit_chain_state(chain_key, tenant_id, tip_seq) VALUES (@key, @tenant, 0)", platform))
        {
            cross.Parameters.AddWithValue("key", "tenant:" + B); cross.Parameters.AddWithValue("tenant", B);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync(Ct))).SqlState);
        }
        await using (var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NOT NULL", platform)) Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
        await using (var overwrite = new NpgsqlCommand("UPDATE plugin_qian_audit.audit_entries SET result = 'forged'", platform))
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => overwrite.ExecuteNonQueryAsync(Ct))).SqlState);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", platform)) { set.Parameters.AddWithValue("tenant", A); await set.ExecuteNonQueryAsync(Ct); }
        await using (var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", platform)) Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task 有角色切换权限也不能冒充平台登录_错误配置与角色继承被拒绝()
    {
        var role = "qian_audit_platform_" + Guid.NewGuid().ToString("N")[..8]; var connection = await CreatePlatformRole(role);
        var tenant = new AsyncLocalTenantContext(); using var host = PlatformHost(tenant, connection); await host.StartAsync(Ct); await Prepare(host);
        await Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        var global = new PostgresAuditStore<PlatformAuditDbContext>(host.Services.GetRequiredService<IDataService>(), tenant, role);
        await global.AppendAsync(PlatformRecord(), Ct);
        await Admin($"GRANT \"{role}\" TO qian_app;");
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;"));
            await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => global.ReadAsync(0, 10, null, Ct));
            await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
            await using (var change = new NpgsqlCommand($"SET ROLE \"{role}\"", app)) await change.ExecuteNonQueryAsync(Ct);
            await using var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", app);
            Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
        }
        finally { await Admin($"REVOKE \"{role}\" FROM qian_app;"); }
        var wrong = new PostgresAuditStore<PlatformAuditDbContext>(host.Services.GetRequiredService<IDataService>(), tenant, "wrong_role");
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => wrong.AppendAsync(PlatformRecord(), Ct));
        foreach (var invalid in new[] { "bad';drop", "bad\n", "Upper", new string('a', 64) })
            Assert.Throws<QianErrorException>(() => AuditPlatformAccess.CreateMigrationSql(invalid));
    }

    [Fact]
    public async Task 真实Audit插件提供读写服务_平台能力须显式配置_卸载后旧句柄失效()
    {
        var role = "qian_audit_platform_" + Guid.NewGuid().ToString("N")[..8]; var connection = await CreatePlatformRole(role);
        var tenant = new AsyncLocalTenantContext(); using var host = PlatformHost(tenant, connection); await host.StartAsync(Ct); await Prepare(host);
        await Admin("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        await using var runtime = new PluginLoader(host.Services); var consumer = new AuditConsumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg", platformRole = role } }), Define("consumer", consumer, [])], Ct);
        var service = consumer.Context!.Audit(); await service.AppendPlatformAsync(consumer.Context!, PlatformRecord(), Ct);
        using (tenant.BeginScope(A))
        {
            await service.AppendAsync(CanonicalizerTests.Record(), Ct); Assert.Single(await service.ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => service.AppendPlatformAsync(consumer.Context!, PlatformRecord(), Ct))).Error.Code.Code);
        }
        Assert.Single(await service.ReadPlatformAsync(consumer.Context!, 0, 10, cancellationToken: Ct));
        var tenantConsumer = new AuditConsumer();
        await runtime.MountAsync([new PluginDefinition(new PluginEntry("tenant-producer", "sample.tenant-producer", "1.0.0",
            new ScopeKey("tenant", A), 0, JsonSerializer.SerializeToElement(new { })), tenantConsumer, [])], Ct);
        var liftedTenantProducer = tenantConsumer.Context!.Scoped(ScopeKey.Global);
        Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => service.AppendPlatformAsync(liftedTenantProducer, PlatformRecord(), Ct))).Error.Code.Code);
        var tenantProducer = consumer.Context!.Scoped(new ScopeKey("tenant", A));
        Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => service.AppendPlatformAsync(tenantProducer, PlatformRecord(), Ct))).Error.Code.Code);
        await runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.VerifyPlatformAsync(consumer.Context!, Ct));
        await runtime.MountAsync([Define("audit", new AuditPlugin(), [AuditDefinition.Key])], Ct);
        var ordinary = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => ordinary.AppendPlatformAsync(runtime.GetContext("consumer", ScopeKey.Global), PlatformRecord(), Ct))).Error.Code.Code);
    }

    [Fact]
    public async Task 卸载真实审计插件会取消锁等待并排空_重装后可继续追加()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        await using var runtime = new PluginLoader(host.Services); var consumer = new AuditConsumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } }), Define("consumer", consumer, [])], Ct);
        var service = consumer.Context!.Audit(); using var scope = tenant.BeginScope(A);
        await using var holder = new NpgsqlConnection(postgres.ConnectionString); await holder.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", holder))
        { acquire.Parameters.AddWithValue("key", "qian.audit:tenant:" + A); await acquire.ExecuteNonQueryAsync(Ct); }
        var pending = service.AppendAsync(CanonicalizerTests.Record(), Ct);
        try
        {
            var observed = false; var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && !observed)
            {
                await using var inspect = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted)", holder);
                observed = (bool)(await inspect.ExecuteScalarAsync(Ct))!; if (!observed) await Task.Delay(20, Ct);
            }
            Assert.True(observed);
            await runtime.UnmountAsync("audit", ScopeKey.Global, Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => service.AppendAsync(CanonicalizerTests.Record(), Ct));
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", holder); await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
        await runtime.MountAsync([Define("audit", new AuditPlugin(), [AuditDefinition.Key])], Ct);
        var current = runtime.GetContext("consumer", ScopeKey.Global).Audit();
        Assert.Equal(1, (await current.AppendAsync(CanonicalizerTests.Record(), Ct)).Sequence);
    }

    private IHost PlatformHost(AsyncLocalTenantContext tenant, string platformConnection)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(platformConnection).UseKunSchema(AuditDbContext.Schema));
        return builder.Build();
    }
    private async Task<string> CreatePlatformRole(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
        return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
    }
    private static AuditRecord PlatformRecord() => new(Guid.CreateVersion7(), null, "service", "platform-audit-test", null,
        "platform.operation", "1.0.0", new string('1', 64), "api", null, "success", new string('2', 32), DateTimeOffset.UtcNow, CanonicalizerTests.Json("{}"));
    private static PluginDefinition Define(string id, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides, object? config = null) => new(
        new PluginEntry(id, "qian." + id, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
    private sealed class AuditConsumer : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [AuditDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }
}
