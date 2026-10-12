using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;
using Qian.Testing.Containers;

namespace Qian.Data.Tests;

public sealed class DataMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 生成的迁移真实执行_租户隔离不被额外宽松策略绕过()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = CreateBuilder();
        using var host = builder.Build();
        await host.StartAsync(ct);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MigrationContext>();
        // 使用真实迁移差异器和 Provider SQL 生成器，不在测试中补写租户策略。
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
        var sql = string.Join('\n', commands.Select(command => command.CommandText));
        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.Contains("AS RESTRICTIVE", sql, StringComparison.Ordinal);
        Assert.Contains("NULLIF(current_setting('app.current_tenant_id', true), '')", sql, StringComparison.Ordinal);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var create = new NpgsqlCommand(sql, admin)) await create.ExecuteNonQueryAsync(ct);
        await using (var seed = new NpgsqlCommand("""
            GRANT USAGE ON SCHEMA plugin_migrations TO qian_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_migrations TO qian_app;
            INSERT INTO plugin_migrations.customers (id, tenant_id) VALUES (1, 'a'), (2, 'b');
            CREATE POLICY careless_allow_all ON plugin_migrations.customers USING (true) WITH CHECK (true);
            """, admin)) await seed.ExecuteNonQueryAsync(ct);
        await using var app = new NpgsqlConnection(postgres.AppConnectionString);
        await app.OpenAsync(ct);
        await using (var flags = new NpgsqlCommand("SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid = 'plugin_migrations.customers'::regclass", app))
            Assert.Equal(true, await flags.ExecuteScalarAsync(ct));
        await Count(0);
        await SetTenant("a");
        await Count(1);
        await using (var insert = new NpgsqlCommand("INSERT INTO plugin_migrations.customers (id, tenant_id) VALUES (3, 'b')", app))
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        await SetTenant("b");
        await Count(1);
        await SetTenant("");
        await Count(0);
        await using (var insert = new NpgsqlCommand("INSERT INTO plugin_migrations.customers (id, tenant_id) VALUES (3, '')", app))
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        await host.StopAsync(ct);

        async Task Count(long expected)
        {
            await using var query = new NpgsqlCommand("SELECT count(*) FROM plugin_migrations.customers", app);
            Assert.Equal(expected, await query.ExecuteScalarAsync(ct));
        }
        async Task SetTenant(string tenant)
        {
            await using var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", app);
            set.Parameters.AddWithValue("tenant", tenant);
            await set.ExecuteScalarAsync(ct);
        }
    }

    [Fact]
    public async Task 覆盖安全迁移生成器_启动拒绝()
    {
        var builder = CreateBuilder(true);
        using var host = builder.Build();
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void 缺失租户目标模型_不生成裸租户表()
    {
        using var host = CreateBuilder().Build();
        using var scope = host.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<MigrationContext>().GetService<IMigrationsSqlGenerator>();
        var operation = new CreateTableOperation { Name = "customers", Schema = "plugin_migrations" };
        operation.Columns.Add(new AddColumnOperation { Name = "tenant_id", Table = "customers", Schema = "plugin_migrations", ClrType = typeof(string), ColumnType = "text", IsNullable = false });
        Assert.Throws<QianDataMisconfiguredException>(() => generator.Generate([operation]));
    }

    private HostApplicationBuilder CreateBuilder(bool replaceGenerator = false)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddQianData<MigrationContext>((_, options) =>
        {
            options.UseNpgsql(postgres.AppConnectionString).UseKunSchema("plugin_migrations");
            if (replaceGenerator) options.ReplaceService<IMigrationsSqlGenerator, NpgsqlMigrationsSqlGenerator>();
        });
        return builder;
    }

    public sealed class Customer : IMultiTenant
    {
        public int Id { get; set; }
        public string? TenantId { get; set; }
    }
    public sealed class MigrationContext(DbContextOptions<MigrationContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Customer>().ToTable("customers");
    }
}
