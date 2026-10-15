using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.EventBus.Outbox;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Data.Tests;

public sealed partial class DataSessionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 缺少事件目录或含未实现配置_Provider挂载失败()
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var withRegistry in new[] { false, true })
        {
            var services = new ServiceCollection();
            if (withRegistry) services.AddKunEventBus();
            using var host = services.BuildServiceProvider();
            await using var runtime = new PluginLoader(host);
            using var config = JsonDocument.Parse(withRegistry ? "{\"rls\":{\"enforce\":false}}" : "{}");
            await runtime.MountAsync([new PluginDefinition(
                new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, config.RootElement),
                new DataPlugin(), [DataDefinition.Key])], ct);
            Assert.Equal(PluginState.Failed, runtime.GetState("data", ScopeKey.Global));
            Assert.IsType<QianDataMisconfiguredException>(runtime.GetFailure("data", ScopeKey.Global));
        }
    }

    [Fact]
    public async Task 真实Provider_领域通知和Outbox使用同一上下文_失败整体回滚()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = new AsyncLocalTenantContext();
        using var host = BuildHost(tenant);
        await host.StartAsync(ct);
        await PrepareAsync(host);
        await using var runtime = await MountAsync(host);
        var data = runtime.GetContext("data", ScopeKey.Global).Data();
        using var tenantScope = tenant.BeginScope("a");
        var id = Guid.NewGuid();
        await using (var session = await data.OpenAsync<CustomerContext>(ct))
        {
            var customer = new Customer(id, "saved");
            session.Context.Add(customer);
            customer.Change("committed");
            await session.SaveChangesAsync(ct);
            // handler 在同一 scope 查询 ChangeTracker；另起 scope 会找不到这个待保存实体。
            Assert.True(customer.NotificationHandled);
            Assert.Single(await session.Context.OutboxMessages.ToListAsync(ct));
        }
        var failedId = Guid.NewGuid();
        await using (var failed = await data.OpenAsync<CustomerContext>(ct))
        {
            failed.Context.Add(new Customer(failedId, new string('x', 80)));
            failed.Outbox.Enqueue(new CustomerMessage(failedId, "must rollback"));
            await Assert.ThrowsAsync<DbUpdateException>(() => failed.SaveChangesAsync(ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => failed.SaveChangesAsync(ct));
        }
        await using var verify = await data.OpenAsync<CustomerContext>(ct);
        Assert.Equal(id, Assert.Single(await verify.Context.Set<Customer>().ToListAsync(ct)).Id);
        var message = Assert.Single(await verify.Context.OutboxMessages.ToListAsync(ct));
        Assert.Equal("a", message.TenantId);
        Assert.Contains(id.ToString(), message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(failedId.ToString(), message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 两个业务上下文的会话缓冲独立_不会写入最后注册的上下文()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = new AsyncLocalTenantContext();
        using var host = BuildHost(tenant);
        await host.StartAsync(ct);
        await PrepareAsync(host);
        await using var runtime = await MountAsync(host);
        var data = runtime.GetContext("data", ScopeKey.Global).Data();
        using var tenantScope = tenant.BeginScope("a");
        await using var first = await data.OpenAsync<CustomerContext>(ct);
        await using var second = await data.OpenAsync<LedgerContext>(ct);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        first.Context.Add(new Customer(firstId, "first"));
        first.Outbox.Enqueue(new CustomerMessage(firstId, "first"));
        second.Context.Add(new Customer(secondId, "second"));
        second.Outbox.Enqueue(new CustomerMessage(secondId, "second"));
        await first.SaveChangesAsync(ct);
        Assert.Empty(await second.Context.OutboxMessages.ToListAsync(ct));
        await second.SaveChangesAsync(ct);
        Assert.Contains(firstId.ToString(), Assert.Single(await first.Context.OutboxMessages.ToListAsync(ct)).Payload, StringComparison.Ordinal);
        Assert.Contains(secondId.ToString(), Assert.Single(await second.Context.OutboxMessages.ToListAsync(ct)).Payload, StringComparison.Ordinal);
        Assert.Equal(firstId, Assert.Single(await first.Context.Set<Customer>().ToListAsync(ct)).Id);
        Assert.Equal(secondId, Assert.Single(await second.Context.Set<Customer>().ToListAsync(ct)).Id);
    }

    [Fact]
    public async Task 通知失败或缺少Mediator均不提交_卸载撤销入口并释放会话()
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var mediator in new[] { true, false })
        {
            var tenant = new AsyncLocalTenantContext();
            using var host = BuildHost(tenant, mediator);
            await host.StartAsync(ct);
            await PrepareAsync(host);
            await using var runtime = await MountAsync(host);
            var data = runtime.GetContext("data", ScopeKey.Global).Data();
            using var tenantScope = tenant.BeginScope("a");
            await using var failed = await data.OpenAsync<CustomerContext>(ct);
            var customer = new Customer(Guid.NewGuid(), "not saved");
            failed.Context.Add(customer);
            customer.Change("notify-fail");
            var error = await Assert.ThrowsAnyAsync<Exception>(() => failed.SaveChangesAsync(ct));
            Assert.Equal(mediator, customer.NotificationHandled);
            if (mediator) Assert.Contains("通知失败测试", error.ToString(), StringComparison.Ordinal);
            else Assert.IsType<QianDataMisconfiguredException>(error);
            await using var verify = await data.OpenAsync<CustomerContext>(ct);
            Assert.Empty(await verify.Context.Set<Customer>().ToListAsync(ct));
            Assert.Empty(await verify.Context.OutboxMessages.ToListAsync(ct));
            var cachedContext = verify.Context;
            await runtime.UnmountAsync("data", ScopeKey.Global, ct);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await data.OpenAsync<CustomerContext>(ct));
            Assert.Throws<ObjectDisposedException>(() => verify.Context);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cachedContext.Set<Customer>().ToListAsync(ct));
        }
    }

    [Fact]
    public async Task 不能在既有会话中切换租户或把其他租户的Outbox混入()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = new AsyncLocalTenantContext();
        using var host = BuildHost(tenant);
        await host.StartAsync(ct);
        await using var runtime = await MountAsync(host);
        var data = runtime.GetContext("data", ScopeKey.Global).Data();
        using var tenantA = tenant.BeginScope("a");
        await using var session = await data.OpenAsync<CustomerContext>(ct);
        var cachedWriter = session.Outbox;
        using (tenant.BeginScope("b"))
        {
            Assert.Throws<QianDataMisconfiguredException>(() => session.Context);
            cachedWriter.Enqueue(new CustomerMessage(Guid.NewGuid(), "wrong tenant"));
        }
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => session.SaveChangesAsync(ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveChangesAsync(ct));
    }

    private IHost BuildHost(AsyncLocalTenantContext tenant, bool mediator = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        }));
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddKunEventBus(events => events.Register("sample.customer.changed.v1", SessionJson.Default.CustomerMessage));
        if (mediator) builder.Services.AddKunMediator(registry => registry.AddNotificationHandler<CustomerChanged, CustomerChangedHandler>());
        builder.Services.AddQianData<CustomerContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema("plugin_sessions"));
        builder.Services.AddQianData<LedgerContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema("plugin_ledger"));
        return builder.Build();
    }

    private static async Task<PluginLoader> MountAsync(IHost host)
    {
        var runtime = new PluginLoader(host.Services);
        await runtime.MountAsync([new PluginDefinition(
            new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new DataPlugin(), [DataDefinition.Key])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, runtime.GetState("data", ScopeKey.Global));
        return runtime;
    }

    private async Task PrepareAsync(IHost host)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = host.Services.CreateScope();
        var sql = scope.ServiceProvider.GetRequiredService<CustomerContext>().Database.GenerateCreateScript()
            + scope.ServiceProvider.GetRequiredService<LedgerContext>().Database.GenerateCreateScript();
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_sessions CASCADE; DROP SCHEMA IF EXISTS plugin_ledger CASCADE;", admin))
            await drop.ExecuteNonQueryAsync(ct);
        await using (var create = new NpgsqlCommand(sql, admin)) await create.ExecuteNonQueryAsync(ct);
        await using var grants = new NpgsqlCommand("""
            GRANT USAGE ON SCHEMA plugin_sessions, plugin_ledger TO qian_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_sessions, plugin_ledger TO qian_app;
            """, admin);
        await grants.ExecuteNonQueryAsync(ct);
    }

    public sealed class Customer : AggregateRoot<Guid, Guid?>, IMultiTenant
    {
        public Customer() { }
        public Customer(Guid id, string name) { Id = id; Name = name; }
        public string Name { get; private set; } = string.Empty;
        public string? TenantId { get; set; }
        public bool NotificationHandled { get; set; }
        public void Change(string name) { Name = name; AddDomainEvent(new CustomerChanged(Id, name, DateTime.UtcNow)); }
    }
    public sealed record CustomerChanged(Guid Id, string Name, DateTime OccurredAt) : IDomainEvent, INotification;
    public sealed class CustomerChangedHandler(CustomerContext context, IOutboxWriter writer) : INotificationHandler<CustomerChanged>
    {
        public ValueTask Handle(CustomerChanged notification, CancellationToken cancellationToken)
        {
            var customer = context.ChangeTracker.Entries<Customer>().Single(e => e.Entity.Id == notification.Id).Entity;
            customer.NotificationHandled = true;
            writer.Enqueue(new CustomerMessage(notification.Id, notification.Name));
            if (notification.Name == "notify-fail") throw new InvalidOperationException("通知失败测试");
            return ValueTask.CompletedTask;
        }
    }
    public sealed class CustomerContext(DbContextOptions<CustomerContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Customer>().Property(c => c.Name).HasMaxLength(24);
    }
    public sealed class LedgerContext(DbContextOptions<LedgerContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Customer>().Property(c => c.Name).HasMaxLength(24);
    }
}
internal sealed record CustomerMessage(Guid Id, string Name);
[JsonSerializable(typeof(CustomerMessage))]
internal sealed partial class SessionJson : JsonSerializerContext;
