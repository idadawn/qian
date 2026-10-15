using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EntityFrameworkCore.Conventions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Qian.Events;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Tenancy.Tests;

public sealed class TenantDirectoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static PostgresTenantDirectory Store(IHost host) => new(host.Services.GetRequiredService<IDataService>(), host.Services.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>());

    [Fact]
    public async Task 缺少租户事件处理器时主档与Outbox一起回滚()
    {
        using var host = Host(withTenantEvents: false); await host.StartAsync(Ct); await Prepare(host);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(host).CreateAsync(new("rollback", "回滚"), Ct));
        Assert.Null(await Store(host).GetAsync("rollback", Ct));
    }

    [Fact]
    public async Task 真库创建待激活租户_目录跨宿主读取_并发同号只能创建一个()
    {
        using var first = Host(); using var second = Host(); await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        var results = await Task.WhenAll(Create(Store(first)), Create(Store(second)));
        Assert.Single(results, x => x is null); Assert.Single(results, x => x == "CONFLICT");
        var descriptor = await Store(second).GetAsync("tenant1", Ct); Assert.NotNull(descriptor);
        Assert.Equal(TenantStatus.Pending, descriptor.Status); Assert.Equal(TenantIsolation.Row, descriptor.Isolation); Assert.NotEqual(Guid.Empty, descriptor.Id);
        Assert.Null(await Store(second).GetAsync("unknown", Ct));
        await using var session = await first.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
        Assert.Equal(1, await session.Context.Tenants.CountAsync(Ct));
        async Task<string?> Create(PostgresTenantDirectory store)
        {
            try { await store.CreateAsync(new("tenant1", "租户一"), Ct); return null; }
            catch (QianErrorException error) { return error.Error.Code.Code; }
        }
    }

    [Fact]
    public async Task 主档编辑有并发令牌_过期版本失败_持久化后号与隔离不可变()
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host); var store = Store(host);
        var created = await store.CreateAsync(new("tenant1", "旧名称"), Ct);
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
        var row = await session.Context.Tenants.SingleAsync(Ct); var revision = session.Context.Entry(row).Property<uint>(KunDataConventions.RowVersionPropertyName).CurrentValue;
        await store.UpdateAsync(created.Id, new("新名称", revision), Ct);
        var conflict = await Assert.ThrowsAsync<QianErrorException>(() => store.UpdateAsync(created.Id, new("过期名称", revision), Ct));
        Assert.Equal("PRECONDITION_FAILED", conflict.Error.Code.Code); Assert.Equal("新名称", (await store.GetAsync("tenant1", Ct))!.Name);
        row.Code = "mutated"; await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveChangesAsync(Ct));
        await using var other = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
        (await other.Context.Tenants.SingleAsync(Ct)).Isolation = "schema";
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task 非法租户号或尚未支持的隔离策略不写入数据库()
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host); var store = Store(host);
        foreach (var code in new[] { "", "TENANT", "a-b", "租户", "has space", new string('a', 65) })
            Assert.Equal("INPUT_INVALID", (await Assert.ThrowsAsync<QianErrorException>(() => store.CreateAsync(new(code, "name"), Ct))).Error.Code.Code);
        foreach (var isolation in new[] { TenantIsolation.Schema, TenantIsolation.Database, (TenantIsolation)99 })
            await Assert.ThrowsAsync<QianErrorException>(() => store.CreateAsync(new("tenant1", "name", isolation), Ct));
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
        Assert.Empty(await session.Context.Tenants.ToArrayAsync(Ct));
    }

    [Fact]
    public async Task 管理员只保留成员指针_唯一管理员_ORM与原生SQL都隔离租户()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var a = await Store(host).CreateAsync(new("a", "A"), Ct); var b = await Store(host).CreateAsync(new("b", "B"), Ct);
        foreach (var id in new[] { a.Id, b.Id })
        {
            using var scope = tenant.BeginScope(id.ToString());
            await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
            session.Context.Administrators.Add(new TenantAdminRow { MembershipId = Guid.CreateVersion7() }); await session.SaveChangesAsync(Ct);
        }
        using (tenant.BeginScope(a.Id.ToString()))
        {
            await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
            Assert.Equal(a.Id.ToString(), (await session.Context.Administrators.SingleAsync(Ct)).TenantId);
            Assert.Null(await session.Context.Administrators.SingleOrDefaultAsync(x => x.TenantId == b.Id.ToString(), Ct));
            session.Context.Administrators.Add(new TenantAdminRow { TenantId = b.Id.ToString(), MembershipId = Guid.CreateVersion7() });
            var denied = await Assert.ThrowsAsync<DbUpdateException>(() => session.SaveChangesAsync(Ct));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, Assert.IsType<PostgresException>(denied.InnerException).SqlState);
        }
        using (tenant.BeginScope(a.Id.ToString()))
        {
            await using var duplicate = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
            duplicate.Context.Administrators.Add(new TenantAdminRow { MembershipId = Guid.CreateVersion7() });
            var conflict = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(Ct));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(conflict.InnerException).SqlState);
        }
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        await using (var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_tenancy.tenant_admins", app)) Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", app)) { set.Parameters.AddWithValue("tenant", a.Id.ToString()); await set.ExecuteNonQueryAsync(Ct); }
        await using (var read = new NpgsqlCommand("SELECT tenant_id FROM plugin_qian_tenancy.tenant_admins", app)) Assert.Equal(a.Id.ToString(), await read.ExecuteScalarAsync(Ct));
        await using var cross = new NpgsqlCommand("UPDATE plugin_qian_tenancy.tenant_admins SET tenant_id = @tenant", app); cross.Parameters.AddWithValue("tenant", b.Id.ToString());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => cross.ExecuteNonQueryAsync(Ct))).SqlState);
    }

    [Fact]
    public async Task 真实Data与Tenancy插件挂载目录_卸载后旧消费者失效()
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host); await Store(host).CreateAsync(new("a", "A"), Ct);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Definition("data", new DataPlugin(), [DataDefinition.Key]), Definition("tenancy", new TenancyPlugin(), [TenancyDefinition.Key]), Definition("consumer", consumer, [])], Ct);
        var directory = consumer.Context!.Tenancy();
        Assert.Equal("A", (await directory.GetAsync("a", Ct))!.Name);
        await runtime.UnmountAsync("tenancy", ScopeKey.Global, Ct);
        Assert.ThrowsAny<Exception>(() => consumer.Context!.Tenancy());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => directory.GetAsync("a", Ct).AsTask());
    }
    private static PluginDefinition Definition(string id, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides) => new(
        new PluginEntry(id, "qian." + id, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, provides);
    private sealed class Consumer : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [TenancyDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }
    private IHost Host(Kun.Core.MultiTenancy.ITenantContext? tenant = null, bool withTenantEvents = true)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<Kun.Core.MultiTenancy.ITenantContext>(tenant ?? new AsyncLocalTenantContext()); builder.Services.AddKunEventBus(registrar => registrar.RegisterTenancyEvents()); builder.Services.AddKunMediator(builder => { if (withTenantEvents) builder.AddQianTenancyEvents(); }); builder.Services.AddQianEvents();
        builder.Services.AddQianData<TenancyDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(TenancyDbContext.Schema));
        return builder.Build();
    }
    private async Task Prepare(IHost host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_tenancy CASCADE;\n" + sql + "\nGRANT USAGE ON SCHEMA plugin_qian_tenancy TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_tenancy TO qian_app;", admin);
        await create.ExecuteNonQueryAsync(Ct);
    }
}
