using System.Reflection;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Testing.Containers;

namespace Qian.Authz.Tests;

public sealed partial class PermissionCatalogPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    private static PermissionCatalog Catalog(string version = "1.0.0", string risk = "high")
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var directory = Path.Combine(root!.FullName, "samples/Qian.Sample.Crm/metadata");
        var permissions = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "permissions.json")))!;
        var export = permissions.AsArray().Single(resource => resource!["actions"]!.AsArray().Any(action => action!.GetValue<string>() == "export"))!;
        export["risk"]!["export"] = risk;
        return new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.sample.crm", version,
            Json(permissions.ToJsonString()), Json(File.ReadAllText(Path.Combine(directory, "roles.json"))));
    }

    [Fact]
    public async Task 受限角色真实保存权限资源与策略包_版本不可变且重复登记幂等()
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host);
        var store = Store(host); var metadata = Catalog(); await store.PutAsync(metadata, Ct); await store.PutAsync(metadata, Ct);
        var roles = JsonSerializer.SerializeToElement(metadata.Roles.Select(role => new { role = role.Role, grant = role.Patterns }));
        var attributed = new PermissionCatalogCompiler(new SurfaceValidator()).FromAttributes("qian.sample.crm", "1.0.0",
            typeof(CrmPermissions).GetCustomAttributes<PermissionAttribute>(), roles);
        await store.PutAsync(attributed, Ct); // 同版本真实属性登记必须与 metadata 行完全一致，不能产生冲突或重复行。
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<AuthzDbContext>(Ct);
        Assert.Equal(1, await session.Context.Catalogs.CountAsync(Ct)); Assert.Equal(3, await session.Context.Permissions.CountAsync(Ct));
        Assert.Equal(1, await session.Context.ResourceTypes.CountAsync(Ct)); Assert.Equal(2, await session.Context.PolicyPacks.CountAsync(Ct));
        var export = await session.Context.Permissions.SingleAsync(row => row.Action == "export", Ct);
        Assert.Equal("high", export.Risk); Assert.True(export.StepUp); Assert.Equal("web", Json(export.Terminals)[0].GetString());
        var owner = await session.Context.PolicyPacks.SingleAsync(row => row.Role == "tenant.owner", Ct); Assert.Equal(3, Json(owner.Grants).GetArrayLength());
        var readBack = await store.GetAsync("qian.sample.crm", "1.0.0", Ct); Assert.NotNull(readBack); Assert.Equal(3, readBack.Permissions.Count);
        var conflict = await Assert.ThrowsAsync<QianErrorException>(() => store.PutAsync(Catalog(risk: "critical"), Ct)); Assert.Equal("CONFLICT", conflict.Error.Code.Code);
        Assert.Equal(PermissionRisk.High, (await store.GetAsync("qian.sample.crm", "1.0.0", Ct))!.Permissions.Single(permission => permission.Action == "export").Risk);
        await store.PutAsync(Catalog("2.0.0", "critical"), Ct);
        Assert.Equal(2, await session.Context.Catalogs.CountAsync(Ct));
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        await using var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", app);
        Assert.Equal(false, await role.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task 两个真实宿主竞争同一目录版本_冲突不会覆盖已提交元数据()
    {
        using var first = Host(); using var second = Host(); await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        var results = await Task.WhenAll(Write(Store(first), "high"), Write(Store(second), "critical"));
        Assert.Single(results, result => result is null); Assert.Single(results, result => result == "CONFLICT");
        await using var session = await first.Services.GetRequiredService<IDataService>().OpenAsync<AuthzDbContext>(Ct);
        Assert.Equal(1, await session.Context.Catalogs.CountAsync(Ct)); Assert.Equal(3, await session.Context.Permissions.CountAsync(Ct));
        Assert.Equal(2, await session.Context.PolicyPacks.CountAsync(Ct));
        async Task<string?> Write(PostgresPermissionCatalogStore store, string risk)
        {
            try { await store.PutAsync(Catalog(risk: risk), Ct); return null; }
            catch (QianErrorException error) { return error.Error.Code.Code; }
        }
    }

    [Fact]
    public async Task 数据库在策略包写入阶段拒绝时_整批目录与权限都回滚()
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using (var trigger = new NpgsqlCommand("""
            CREATE FUNCTION plugin_qian_authz.reject_pack() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'fixture reject policy pack'; END $$;
            CREATE TRIGGER reject_pack BEFORE INSERT ON plugin_qian_authz.policy_packs
            FOR EACH ROW EXECUTE FUNCTION plugin_qian_authz.reject_pack();
            """, admin)) await trigger.ExecuteNonQueryAsync(Ct);
        await Assert.ThrowsAsync<DbUpdateException>(() => Store(host).PutAsync(Catalog(), Ct));
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<AuthzDbContext>(Ct);
        Assert.Equal(0, await session.Context.Catalogs.CountAsync(Ct)); Assert.Equal(0, await session.Context.Permissions.CountAsync(Ct));
        Assert.Equal(0, await session.Context.ResourceTypes.CountAsync(Ct)); Assert.Equal(0, await session.Context.PolicyPacks.CountAsync(Ct));
    }

    [Permission("customer", "read")]
    [Permission("customer", "write")]
    [Permission("customer", "export", Risk = PermissionRisk.High, RequiresStepUp = true, Terminals = new[] { "web" })]
    private sealed class CrmPermissions;

    private IHost Host(Kun.Core.MultiTenancy.ITenantContext? tenant = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<Kun.Core.MultiTenancy.ITenantContext>(tenant ?? new AsyncLocalTenantContext()); builder.Services.AddKunEventBus(registrar => registrar.RegisterAuthzEvents());
        builder.Services.AddKunMediator(mediator => mediator.AddQianAuthzEvents());
        builder.Services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuthzDbContext.Schema));
        return builder.Build();
    }
    private static PostgresPermissionCatalogStore Store(IHost host) => new(host.Services.GetRequiredService<IDataService>());
    private async Task Prepare(IHost host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AuthzDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_authz CASCADE;\n" + sql + "\nGRANT USAGE ON SCHEMA plugin_qian_authz TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_authz TO qian_app;", admin);
        await create.ExecuteNonQueryAsync(Ct);
    }
}
