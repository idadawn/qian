using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Interceptors;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Kun.MultiTenancy.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Qian.Testing.Containers;
using Qian.Runtime.Errors;

namespace Qian.Data.Tests;

public sealed class DataSafetyTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;
    public DataSafetyTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task PostgreSQL角色边界_超级用户对照与受限角色原生SQL()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var admin = new NpgsqlConnection(_postgres.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var ddl = new NpgsqlCommand("""
            CREATE TABLE data_role_boundary_probe (id int PRIMARY KEY, tenant_id text NOT NULL);
            INSERT INTO data_role_boundary_probe VALUES (1, 'a'), (2, 'b');
            ALTER TABLE data_role_boundary_probe ENABLE ROW LEVEL SECURITY;
            ALTER TABLE data_role_boundary_probe FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_scope ON data_role_boundary_probe
              USING (tenant_id = current_setting('app.current_tenant_id', true))
              WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true));
            SELECT set_config('app.current_tenant_id', 'a', false);
            """, admin)) await ddl.ExecuteNonQueryAsync(ct);
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM data_role_boundary_probe", admin))
            Assert.Equal(2L, await count.ExecuteScalarAsync(ct));
        await using var app = new NpgsqlConnection(_postgres.AppConnectionString);
        await app.OpenAsync(ct);
        await using (var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", app))
            Assert.Equal(false, await role.ExecuteScalarAsync(ct));
        await using (var missing = new NpgsqlCommand("SELECT count(*) FROM data_role_boundary_probe", app))
            Assert.Equal(0L, await missing.ExecuteScalarAsync(ct));
        await using (var tenant = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', 'a', false)", app))
            await tenant.ExecuteScalarAsync(ct);
        await using (var count = new NpgsqlCommand("SELECT count(*) FROM data_role_boundary_probe", app))
            Assert.Equal(1L, await count.ExecuteScalarAsync(ct));
        await using var insert = new NpgsqlCommand("INSERT INTO data_role_boundary_probe VALUES (3, 'b')", app);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(ct));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 在线应用_超级用户连接含预打开连接_启动拒绝(bool preopened, bool assumeRestrictedRole)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        if (preopened) await connection.OpenAsync(ct);
        if (assumeRestrictedRole)
        {
            await using var role = new NpgsqlCommand("SET ROLE qian_app", connection);
            await role.ExecuteNonQueryAsync(ct);
        }
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddQianData<ProbeContext>((_, options) => options.UseNpgsql(connection).UseKunSchema("plugin_data_safety"));
        using var host = builder.Build();
        var error = await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.StartAsync(ct));
        Assert.Equal("DATA_MISCONFIGURED", error.Error.Code.Code);
        Assert.Contains("BYPASSRLS", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 在线应用_普通登录角色带BYPASSRLS_启动拒绝()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(_postgres.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var createRole = new NpgsqlCommand($"CREATE ROLE data_bypass LOGIN NOSUPERUSER BYPASSRLS PASSWORD '{password}'", admin))
            await createRole.ExecuteNonQueryAsync(ct);
        var connection = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Username = "data_bypass", Password = password };
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddQianData<ProbeContext>((_, options) => options.UseNpgsql(connection.ConnectionString).UseKunSchema("plugin_data_safety"));
        using var host = builder.Build();
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.StartAsync(ct));
    }

    [Fact]
    public async Task 在线应用_同步重开连接也执行角色检查()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddQianData<ProbeContext>((_, options) => options.UseNpgsql(_postgres.ConnectionString).UseKunSchema("plugin_data_safety"));
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProbeContext>();
        // 即使没有启动宿主，实际打开应用连接时也不能使用管理员凭据。
        Assert.Throws<QianDataMisconfiguredException>(() => db.Database.OpenConnection());
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task AddQianData_自动装配_真实数据库按租户过滤()
    {
        var tenant = new AsyncLocalTenantContext();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddQianData<ProbeContext>((_, options) => options.UseNpgsql(_postgres.AppConnectionString).UseKunSchema("plugin_data_safety"));
        using var host = builder.Build();
        // 启动只校验配置，不建表或执行迁移。
        await host.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(host.Services.GetRequiredService<ErrorCatalog>().Contains(DataErrors.Misconfigured));
        try
        {
            await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("""
                CREATE SCHEMA plugin_data_safety;
                GRANT USAGE ON SCHEMA plugin_data_safety TO qian_app;
                CREATE TABLE plugin_data_safety.data_safety_probe (id int PRIMARY KEY, tenant_id text NOT NULL);
                GRANT SELECT, INSERT, UPDATE, DELETE ON plugin_data_safety.data_safety_probe TO qian_app;
                INSERT INTO plugin_data_safety.data_safety_probe VALUES (1, 'tenant-a'), (2, 'tenant-b');
                ALTER TABLE plugin_data_safety.data_safety_probe ENABLE ROW LEVEL SECURITY;
                ALTER TABLE plugin_data_safety.data_safety_probe FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_scope ON plugin_data_safety.data_safety_probe
                  USING (tenant_id = current_setting('app.current_tenant_id', true))
                  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true));
                """, connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            foreach (var id in new[] { "tenant-a", "tenant-b", "tenant-a" })
            {
                using var tenantScope = tenant.BeginScope(id);
                await using var scope = host.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ProbeContext>();
                Assert.IsType<KunMultiTenantModelCacheKeyFactory>(db.GetService<IModelCacheKeyFactory>());
                Assert.Equal(id, Assert.Single(await db.Set<Probe>().Select(x => x.TenantId)
                    .ToArrayAsync(TestContext.Current.CancellationToken)));
                // 原生 SQL 绕过 EF 查询过滤器，单独证明 RLS。
                var rawTenant = await db.Database.SqlQueryRaw<string>(
                    "SELECT tenant_id AS \"Value\" FROM plugin_data_safety.data_safety_probe").SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(id, rawTenant);
                await using var tx = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var guc = await db.Database.SqlQueryRaw<string>(
                    "SELECT current_setting('app.current_tenant_id', true) AS \"Value\"").SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(id, guc);
            }
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
            Assert.False(host.Services.GetRequiredService<ErrorCatalog>().Contains(DataErrors.Misconfigured));
        }
    }

    [Fact]
    public async Task AddQianData_缓存键被覆盖_宿主拒绝启动()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddQianData<ProbeContext>((_, options) => options
            .UseNpgsql(_postgres.AppConnectionString)
            .UseKunSchema("plugin_data_safety")
            .ReplaceService<IModelCacheKeyFactory, ModelCacheKeyFactory>());
        using var host = builder.Build();
        var error = await Assert.ThrowsAsync<QianDataMisconfiguredException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Equal("DATA_MISCONFIGURED", error.Error.Code.Code);
        Assert.Contains("KunMultiTenantModelCacheKeyFactory", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void 手工装配_缺少拦截器或缓存键_拒绝构造(bool interceptors, bool factory)
    {
        var tenant = new AsyncLocalTenantContext();
        var options = new DbContextOptionsBuilder<ProbeContext>().UseNpgsql(_postgres.AppConnectionString);
        if (interceptors)
            options.AddInterceptors(new TenantRlsConnectionInterceptor(tenant, NullLogger<TenantRlsConnectionInterceptor>.Instance),
                new TenantRlsTransactionInterceptor(tenant));
        if (factory) options.ReplaceService<IModelCacheKeyFactory, KunMultiTenantModelCacheKeyFactory>();
        Assert.Throws<QianDataMisconfiguredException>(() => new ProbeContext(options.Options, tenant));
    }

    public sealed class Probe
        : IMultiTenant
    {
        public int Id { get; set; }
        public string? TenantId { get; set; }
    }

    public sealed class ProbeContext(DbContextOptions<ProbeContext> options, ITenantContext tenant)
        : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Probe>(entity =>
            {
                entity.ToTable("data_safety_probe");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
