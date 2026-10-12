using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
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
using Qian.Testing.Containers;

namespace Qian.Data.Tests;

public sealed class CrossHostTenantModelTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 独立宿主复用同租户模型_旧访问器退出或换租户不影响当前读取(bool previousChangedTenant)
    {
        var ct = TestContext.Current.CancellationToken;
        var own = "a-" + Guid.NewGuid().ToString("N");
        var foreign = "b-" + Guid.NewGuid().ToString("N");
        var firstTenant = new AsyncLocalTenantContext(); IModel model;
        using (firstTenant.BeginScope(own))
        using (var first = BuildHost(firstTenant))
        {
            await first.StartAsync(ct);
            using var scope = first.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrossHostContext>(); model = db.Model;
            var design = db.GetService<IDesignTimeModel>().Model;
            var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, design.GetRelationalModel());
            var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, design).Select(command => command.CommandText));
            Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
            await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(ct);
            await using (var create = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_cross_host CASCADE;\n" + sql + "\nGRANT USAGE ON SCHEMA plugin_cross_host TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_cross_host TO qian_app;", admin)) await create.ExecuteNonQueryAsync(ct);
            db.Rows.Add(new CrossHostRow { Id = Guid.NewGuid(), TenantId = own }); await db.SaveChangesAsync(ct);
            // 第二宿主使用详情查询，覆盖同一模型下不同查询形状的首次编译。
            Assert.Equal(1, await db.Rows.CountAsync(ct));
            await first.StopAsync(ct);
        }

        using var previous = firstTenant.BeginScope(previousChangedTenant ? foreign : null);
        var currentTenant = new AsyncLocalTenantContext();
        using var current = currentTenant.BeginScope(own);
        using var second = BuildHost(currentTenant); await second.StartAsync(ct);
        await using var session = await second.Services.GetRequiredService<IDataService>().OpenAsync<CrossHostContext>(ct);
        Assert.Same(model, session.Context.Model);
        Assert.Equal(own, (await session.Context.Rows.AsNoTracking().SingleAsync(ct)).TenantId);
        await second.StopAsync(ct);
    }

    private IHost BuildHost(ITenantContext tenant)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<CrossHostContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema("plugin_cross_host"));
        return builder.Build();
    }

    public sealed class CrossHostRow : IMultiTenant
    {
        public Guid Id { get; set; }
        public string? TenantId { get; set; }
    }

    public sealed class CrossHostContext(DbContextOptions<CrossHostContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        public DbSet<CrossHostRow> Rows => Set<CrossHostRow>();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<CrossHostRow>().ToTable("rows");
    }
}
