using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.Core.Security;
using Kun.EntityFrameworkCore.Conventions;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Testing.Containers;
using System.Text.Json;

namespace Qian.Data.Tests;

public sealed class DataConventionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 删除主体与时间自动填充_直接Remove软删除_既有审计不能伪改()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = new CurrentActor { UserId = Guid.NewGuid().ToString() };
        var creator = actor.UserId;
        var clock = new TestClock();
        var created = clock.GetUtcNow();
        var tenant = new AsyncLocalTenantContext();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ICurrentUser>(actor);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddQianData<DeletionContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema("plugin_deletion"));
        using var host = builder.Build();
        await host.StartAsync(ct);
        using var tenantScope = tenant.BeginScope("a");
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeletionContext>();
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(ct);
            await using (var ddl = new NpgsqlCommand(db.Database.GenerateCreateScript(), admin)) await ddl.ExecuteNonQueryAsync(ct);
            await using var grant = new NpgsqlCommand("GRANT USAGE ON SCHEMA plugin_deletion TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_deletion TO qian_app;", admin);
            await grant.ExecuteNonQueryAsync(ct);
        }
        var soft = new Customer { DisplayName = "soft" };
        var remove = new Customer { DisplayName = "remove" };
        db.AddRange(soft, remove);
        db.Entry(soft).Property(QianDataConventions.DeletedByPropertyName).CurrentValue = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        Assert.Null(db.Entry(soft).Property(QianDataConventions.DeletedByPropertyName).CurrentValue);
        actor.UserId = Guid.NewGuid().ToString();
        var deleter = actor.UserId;
        clock.Now = created.AddHours(1);
        soft.Delete();
        await db.SaveChangesAsync(ct);
        Assert.False(await db.Set<Customer>().AnyAsync(c => c.Id == soft.Id, ct));
        var deleted = await ReadAudit(soft.Id);
        Assert.True(deleted.GetProperty("is_deleted").GetBoolean());
        Assert.Equal(deleter, deleted.GetProperty("deleted_by").GetString());
        Assert.Equal(clock.Now, deleted.GetProperty("deleted_at").GetDateTimeOffset());
        Assert.Equal(creator, deleted.GetProperty("created_by").GetString());
        Assert.Equal(created, deleted.GetProperty("created_at").GetDateTimeOffset());
        db.Entry(soft).Property(QianDataConventions.DeletedByPropertyName).CurrentValue = Guid.NewGuid();
        clock.Now = created.AddHours(2);
        soft.DisplayName = "audit-preserved";
        await db.SaveChangesAsync(ct);
        var preserved = await ReadAudit(soft.Id);
        Assert.Equal(deleter, preserved.GetProperty("deleted_by").GetString());
        Assert.Equal(created.AddHours(1), preserved.GetProperty("deleted_at").GetDateTimeOffset());
        actor.UserId = null;
        db.Remove(remove);
        await db.SaveChangesAsync(ct);
        var removed = await ReadAudit(remove.Id);
        Assert.True(removed.GetProperty("is_deleted").GetBoolean());
        Assert.Equal("remove", removed.GetProperty("display_name").GetString());
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("deleted_by").ValueKind);
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("updated_by").ValueKind);
        Assert.Equal(clock.Now, removed.GetProperty("deleted_at").GetDateTimeOffset());
        Assert.Equal(clock.Now, removed.GetProperty("updated_at").GetDateTimeOffset());
        Assert.Equal(creator, removed.GetProperty("created_by").GetString());
        Assert.Empty(await db.Set<Customer>().ToListAsync(ct));

        async Task<JsonElement> ReadAudit(Guid id)
        {
            var json = await db.Database.SqlQueryRaw<string>("SELECT to_jsonb(c)::text AS \"Value\" FROM plugin_deletion.customer_records c WHERE id = {0}", id).SingleAsync(ct);
            using var parsed = JsonDocument.Parse(json);
            return parsed.RootElement.Clone();
        }
    }

    [Fact]
    public async Task 缺少插件schema或替换模型约定_启动拒绝()
    {
        foreach (var replaceModel in new[] { false, true })
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
            builder.Services.AddQianData<CustomerContext>((_, options) =>
            {
                options.UseNpgsql(postgres.AppConnectionString);
                if (replaceModel) options.UseKunSchema("plugin_conventions")
                    .ReplaceService<IModelCustomizer, ModelCustomizer>();
            });
            using var host = builder.Build();
            await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.StartAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task 自动模型约定和真实数据库并发_派生上下文漏调base仍安全()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = new AsyncLocalTenantContext();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddQianData<CustomerContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString)
            .UseKunSchema("plugin_conventions"));
        using var host = builder.Build();
        await host.StartAsync(ct);
        using var tenantScope = tenant.BeginScope("a");
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerContext>();
        var customerType = db.Model.FindEntityType(typeof(Customer))!;
        Assert.Equal("customer_records", customerType.GetTableName());
        Assert.Equal("display_name", customerType.FindProperty(nameof(Customer.DisplayName))!.GetColumnName());
        Assert.Equal("plugin_conventions", customerType.GetSchema());
        Assert.NotNull(db.Model.FindEntityType(typeof(OutboxMessage)));
        Assert.NotEmpty(customerType.GetDeclaredQueryFilters());
        Assert.False(customerType.FindProperty(nameof(Customer.TenantId))!.IsNullable);
        Assert.Contains(customerType.GetIndexes(), index => index.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(Customer.TenantId)]));
        Assert.Equal("xmin", customerType.FindProperty(KunDataConventions.RowVersionPropertyName)!.GetColumnName());
        Assert.True(customerType.FindProperty(KunDataConventions.RowVersionPropertyName)!.IsConcurrencyToken);
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(ct);
            await using (var ddl = new NpgsqlCommand(db.Database.GenerateCreateScript(), admin)) await ddl.ExecuteNonQueryAsync(ct);
            await using var policy = new NpgsqlCommand("""
                GRANT USAGE ON SCHEMA plugin_conventions TO qian_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_conventions TO qian_app;
                ALTER TABLE plugin_conventions.customer_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE plugin_conventions.customer_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_scope ON plugin_conventions.customer_records
                  USING (tenant_id = current_setting('app.current_tenant_id', true))
                  WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true));
                """, admin);
            await policy.ExecuteNonQueryAsync(ct);
        }
        var customer = new Customer { DisplayName = "原始值" };
        var existingId = Guid.NewGuid();
        var imported = new Customer { DisplayName = "既有主键" };
        imported.UseId(existingId);
        db.AddRange(customer, imported, new Customer { DisplayName = "隐藏" });
        await db.SaveChangesAsync(ct);
        Assert.False(await db.Set<Customer>().AnyAsync(item => item.DisplayName == "隐藏", ct));
        Assert.Equal("a", customer.TenantId);
        Assert.Equal(7, customer.Id.ToByteArray(bigEndian: true)[6] >> 4);
        Assert.Equal(existingId, imported.Id);
        Assert.Null(customer.CreatedBy);
        Assert.InRange(customer.CreatedAt, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Null(customer.UpdatedAt);
        await using var otherScope = host.Services.CreateAsyncScope();
        var other = otherScope.ServiceProvider.GetRequiredService<CustomerContext>();
        var stale = await other.Set<Customer>().SingleAsync(item => item.Id == customer.Id, ct);
        customer.DisplayName = "先提交";
        await db.SaveChangesAsync(ct);
        Assert.NotNull(customer.UpdatedAt);
        stale.DisplayName = "冲突提交";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync(ct));
        customer.Delete();
        await db.SaveChangesAsync(ct);
        Assert.False(await db.Set<Customer>().AnyAsync(item => item.Id == customer.Id, ct));
        await host.StopAsync(ct);
    }

    public sealed class Customer : AggregateRoot<Guid, Guid?>, IMultiTenant
    {
        public string? TenantId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public void UseId(Guid id) => Id = id;
    }

    public sealed class CustomerContext(DbContextOptions<CustomerContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 故意不调用 base，验证装配后的模型定制器不会遗漏安全约定。
            modelBuilder.Entity<Customer>().ToTable("CustomerRecords");
            modelBuilder.Entity<Customer>().HasQueryFilter(customer => customer.DisplayName != "隐藏");
        }
    }

    public sealed class DeletionContext(DbContextOptions<DeletionContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Customer>().ToTable("customer_records");
    }

    private sealed class CurrentActor : ICurrentUser
    {
        public string? UserId { get; set; }
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyList<string> Roles => [];
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
