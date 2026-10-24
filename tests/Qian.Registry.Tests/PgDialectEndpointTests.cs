using System.Net;
using System.Text.Json;
using Json.Schema;
using Kun.AspNetCore.Authorization;
using Kun.Authorization;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Data.AspNetCore;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 平台类型端点随PG方言挂卸变化_重挂恢复_仍经过真实注册表与权限门()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services =>
        {
            services.AddQianDataTypes();
            AddAuthz(services, checker: true);
        });
        await PrepareAuthz(running.Host);
        using var permissions = await GrantDataReader(running, tenant);
        await using var http = await StartEndpointHost(running, tenant, middleware: true, configureServices: services =>
        {
            services.AddKunAuthorization();
            services.AddSingleton(running.Host.Services.GetRequiredService<IPermissionChecker>());
            services.AddSingleton(running.Host.Services.GetRequiredService<IDataTypeCatalog>());
        }, configureEndpoints: app =>
        {
            app.UseAuthorization();
            app.MapQianDataTypes(new PluginEndpointBinding("data", "qian.data", DataDefinition.Key, version: "1.0.0"));
        });

        var dialects = running.Host.Services.GetRequiredService<IDataDialectRegistry>();
        await AssertCatalog([]);
        using (var anonymous = await http.Get("/api/data/meta/types", null))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        // 这个租户没有授予读取权限；注册表内的全局 Data 可见也不能绕过权限判定。
        using (var denied = await http.Get("/api/data/meta/types", "01900000-0000-7000-8000-000000000003"))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            // 使用仓库清单的真实入口和 provides；这是已装配代码的挂卸验收，不冒充 qian add 安装。
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.data.dialect.pg/.qian-plugin/plugin.json")));
            var root = manifest.RootElement;
            var entry = (IQianPlugin)Activator.CreateInstance(Type.GetType(root.GetProperty("runtime").GetProperty("entry").GetString()!, throwOnError: true)!)!;
            await running.Runtime.MountAsync([new PluginDefinition(
                new PluginEntry("pg", root.GetProperty("id").GetString()!, root.GetProperty("version").GetString()!, ScopeKey.Global, 0, Patch("{}")),
                entry, root.GetProperty("provides").EnumerateArray().Select(item => new ServiceKey(item.GetString()!)).ToArray())], Ct);
            Assert.True(running.Runtime.GetState("pg", ScopeKey.Global) == PluginState.Ready, running.Runtime.GetFailure("pg", ScopeKey.Global)?.ToString());
            Assert.Same(Assert.Single(running.Root.List(DataDialectContract.Key)).Value, dialects.Find("pg"));
            await AssertCatalog(["pg"]);

            // HTTP 表面绑定的是 Data；只卸载方言后端点仍 200，不能误把空目录做成 404。
            await running.Runtime.UnmountAsync("pg", ScopeKey.Global, Ct);
            Assert.Empty(running.Root.List(DataDialectContract.Key));
            Assert.Null(dialects.Find("pg"));
            Assert.Equal(DataErrors.DialectNotFound, Assert.Throws<QianErrorException>(() =>
                running.Host.Services.GetRequiredService<IDataTypeCatalog>().MapField("pg", new DataFieldDefinition("json"))).Error.Code);
            await AssertCatalog([]);
        }

        // 对照：隐藏 Data 本身才是平台端点 404；另一租户不受影响，撤销过滤后恢复。
        using (running.Root.Restrict(new ScopeKey("tenant", HttpTenantA), registration => registration.OwnerEntryId != "data"))
        {
            using var hidden = await http.Get("/api/data/meta/types", HttpTenantA);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            using var other = await http.Get("/api/data/meta/types", HttpTenantB);
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        }
        await AssertCatalog([]);

        async Task AssertCatalog(string[] expected)
        {
            var schema = JsonSchema.FromText(DataContractSchemas.DataTypes, new BuildOptions { SchemaRegistry = new SchemaRegistry() });
            foreach (var tenantId in new[] { HttpTenantA, HttpTenantB })
            {
                using var response = await http.Get("/api/data/meta/types", tenantId);
                var body = await Json(response);
                Assert.True(response.StatusCode == HttpStatusCode.OK, body.ToString());
                Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
                Assert.True(schema.Evaluate(body).IsValid, body.ToString());
                Assert.Equal(10, body.GetProperty("types").GetArrayLength());
                Assert.Equal(expected, body.GetProperty("dialects").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
                if (expected.Length > 0)
                {
                    var mappings = Assert.Single(body.GetProperty("dialects").EnumerateArray()).GetProperty("mappings");
                    Assert.Equal(10, mappings.GetArrayLength());
                    Assert.Equal("jsonb", mappings.EnumerateArray().Single(mapping => mapping.GetProperty("type").GetString() == "json").GetProperty("nativeType").GetString());
                }
            }
        }
    }

    private static async Task<IDisposable> GrantDataReader(RunningRegistry running, AsyncLocalTenantContext tenant)
    {
        static JsonElement Read(string name) => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.data/metadata", name)));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.data", "1.0.0", Read("permissions.json"), Read("roles.json"));
        var data = running.Host.Services.GetRequiredService<IDataService>();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        foreach (var id in new[] { HttpTenantA, HttpTenantB })
        {
            using var current = tenant.BeginScope(id);
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.EnsureBuiltinsAsync("test-bootstrap", Ct);
            await roles.InstallDefaultsAsync("qian.data", "1.0.0", null, "test-bootstrap", Ct);
            await roles.AssignAsync("tenant.owner", "service", "reader", "test-bootstrap", Ct);
        }
        return running.Host.Services.GetRequiredService<IPermissionRegistry>().Register("1.0.0", catalog.Permissions);
    }
}
