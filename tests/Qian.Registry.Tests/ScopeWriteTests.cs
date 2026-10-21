using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Runtime;

namespace Qian.Registry.Tests;

// RT-029：HTTP/JWT → 真 Authz 超管审计 → Registry 管理服务 → Lifecycle → 受限 PG/Outbox → 真实 scope 重合入。
public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 管理写入复用生命周期_修订与禁用幂等_过期冲突_租户隔离_权限权益和别名错误头_运营CLI单条撤销()
    {
        var role = "qian_authz_platform_" + Guid.NewGuid().ToString("N")[..8]; var platform = await CreatePlatformLogin(role);
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, events: true, services: services =>
        {
            AddAuthz(services, checker: true);
            services.AddQianData<PlatformAuthzDbContext>((_, options) => options.UseNpgsql(platform).UseKunSchema(AuthzDbContext.Schema));
            services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
            services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(LifecycleDbContext.Schema));
        }, prepareHost: async host =>
        {
            using var scope = host.Services.CreateScope();
            await AdminExec("DROP SCHEMA IF EXISTS plugin_qian_lifecycle CASCADE;" + scope.ServiceProvider.GetRequiredService<LifecycleDbContext>().GetService<IMigrator>().GenerateScript() +
                "GRANT USAGE ON SCHEMA plugin_qian_lifecycle TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_lifecycle TO qian_app;");
        });
        await PrepareAuthz(running.Host); await PrepareAudit(running.Host);
        await AdminExec("BEGIN;" + AuthzPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        using var active = await GrantRegistryRoles(running, tenant);
        await new PostgresPlatformRoleStore(running.Host.Services.GetRequiredService<IDataService>(), tenant, role).AssignSuperadminAsync("root", "operator", Ct);
        await running.Runtime.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, Patch("{}")), new AuditPlugin(), [AuditDefinition.Key]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = role })),
                new AuthzPlugin(), [AuthzDefinition.Key, AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey, AuthzDefinition.SuperadminBypassKey]),
            new PluginDefinition(new PluginEntry("config", "qian.config", "1.0.0", ScopeKey.Global, 0, Patch("{}")), new ConfigurationPlugin(), [ConfigDefinition.Key]),
            new PluginDefinition(new PluginEntry("lifecycle", "qian.lifecycle", "1.0.0", ScopeKey.Global, 0, Patch("{}")), new LifecyclePlugin(new LifecycleRuntimeOptions()), [LifecycleDefinition.Key])], Ct);
        Assert.Equal(PluginState.Ready, running.Runtime.GetState("lifecycle", ScopeKey.Global));
        // 本任务只写 scope；先前开通的租户安装记录由夹具准备，不把挂载误当作安装/开通。
        await AdminExec($"INSERT INTO plugin_qian_lifecycle.tenant_plugin_installs (tenant_id,plugin_id,state,version,updated_by,updated_at,row_version) VALUES ('{HttpTenantA}','test.private','Installed','1.0.0','fixture',now(),1);");
        await using var http = await StartManagementHost(running, tenant, platformTargetTenant: HttpTenantA);
        await using var business = await StartEndpointHost(running, tenant, true);
        var path = $"/api/registry/tenants/{HttpTenantA}/patches/tenant:{HttpTenantA}/shared";
        var body = new { revision = 0, patch = new { id = "shared", config = new { label = "A" } } };
        using (var missing = await http.Put(path.Replace("/registry/", "/kernel/", StringComparison.Ordinal), body, null, null))
        { Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode); Assert.True(missing.Headers.Contains("Deprecation")); }
        using (var denied = await http.Put(path, body, null, "intruder")) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await http.Put(path, body, HttpTenantA, Reader)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await http.Put(path, body, HttpTenantB, Holder)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var entitledMissing = await http.Put(path, body, HttpTenantA, Holder))
        { Assert.Equal(HttpStatusCode.Forbidden, entitledMissing.StatusCode); Assert.Equal("PLUGIN_NOT_ENTITLED", (await Json(entitledMissing)).GetProperty("code").GetString()); }
        using (var mismatch = await http.Put(path.Replace("/patches/tenant:" + HttpTenantA, "/patches/tenant:" + HttpTenantB, StringComparison.Ordinal), body, null))
            Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);
        using (var forged = await http.Put(path.Replace("/registry/", "/kernel/", StringComparison.Ordinal), new { revision = 0, tenant = HttpTenantB, patch = body.patch }, null))
        { Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode); Assert.True(forged.Headers.Contains("Deprecation")); }
        using (var forged = await http.Put(path, new { revision = 0, patch = new { id = "shared", tenant_id = HttpTenantB } }, null)) Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using (var mixed = await http.Put(path, new { revision = 0, patch = new { id = "shared", disabled = true, config = new { label = "ignored" } } }, null)) Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
        Assert.Empty(await Outbox(running.Host.Services, tenant, HttpTenantA));
        using (var added = await http.Put(path, body, null))
        { Assert.True(added.StatusCode == HttpStatusCode.OK, await added.Content.ReadAsStringAsync(Ct)); Assert.Equal(1, (await Json(added)).GetProperty("revision").GetInt32()); }
        await WithinFiveSeconds(() => running.Label("shared", new ScopeKey("tenant", HttpTenantA)) == "A");
        Assert.NotEqual("A", running.Label("shared", ScopeKey.Global));
        using (var other = await business.Get("/surface", HttpTenantB)) Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        using (var same = await http.Put(path, new { revision = 1, body.patch }, null)) Assert.Equal(1, (await Json(same)).GetProperty("revision").GetInt32());
        Assert.Single(await Outbox(running.Host.Services, tenant, HttpTenantA));
        using (var stale = await http.Put(path.Replace("/registry/", "/kernel/", StringComparison.Ordinal), body, null))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("CONFLICT", (await Json(stale)).GetProperty("code").GetString());
            Assert.True(stale.Headers.Contains("Deprecation")); Assert.Contains("<" + path + ">; rel=\"successor-version\"", stale.Headers.GetValues("Link"));
        }
        using (var required = await http.Put(path.Replace("/shared", "/data", StringComparison.Ordinal), new { revision = 0, patch = new { id = "data", disabled = true } }, null))
            Assert.Equal(HttpStatusCode.BadRequest, required.StatusCode);
        using (var disabled = await http.Put(path, new { revision = 1, patch = new { id = "shared", disabled = true } }, null)) Assert.Equal(2, (await Json(disabled)).GetProperty("revision").GetInt32());
        using (var disabled = await http.Put(path, new { revision = 2, patch = new { id = "shared", disabled = true } }, null)) Assert.Equal(2, (await Json(disabled)).GetProperty("revision").GetInt32());
        using (var stale = await http.Put(path, new { revision = 1, patch = new { id = "shared", disabled = true } }, null)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var restored = await http.Put(path, new { revision = 2, body.patch }, null)) Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        var addedByCli = await RunScopeCli(http, "add", "test.private@1.0.0", "--entry", "private");
        Assert.Equal(0, addedByCli.Exit); Assert.Equal("Ready", JsonSerializer.Deserialize<JsonElement>(addedByCli.Output).GetProperty("entries")[0].GetProperty("state").GetString());
        using (var mounted = await business.Get("/private", HttpTenantA)) Assert.Equal(HttpStatusCode.OK, mounted.StatusCode);
        var conflictByCli = await RunScopeCli(http, "add", "test.private@1.0.0", "--entry", "private", "--revision", "0");
        Assert.Equal(1, conflictByCli.Exit); Assert.Equal("CONFLICT", JsonSerializer.Deserialize<JsonElement>(conflictByCli.Error).GetProperty("diagnostics")[0].GetProperty("error").GetProperty("code").GetString());
        var batch = await RunScopeCli(http, "remove", "shared", "private");
        Assert.Equal(1, batch.Exit);
        using (var preserved = await business.Get("/private", HttpTenantA)) Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        var removedByCli = await RunScopeCli(http, "remove", "private");
        Assert.Equal(0, removedByCli.Exit);
        Assert.Equal(["private"], JsonSerializer.Deserialize<JsonElement>(removedByCli.Output).GetProperty("entries").EnumerateArray().Select(item => item.GetProperty("entryId").GetString()));
        Assert.Equal(0, (await RunScopeCli(http, "remove", "private")).Exit);
        Assert.Equal(0, (await RunScopeCli(http, "remove", "shared")).Exit);
        using (var unmounted = await business.Get("/private", HttpTenantA)) Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
        var privatePath = path.Replace("/shared", "/private", StringComparison.Ordinal);
        // 纯租户新增条目撤销后仅有墓碑，幂等确认不再次发事件；重建继承修订，旧墓碑客户端不能覆盖。
        var eventsBefore = (await Outbox(running.Host.Services, tenant, HttpTenantA)).Count;
        using (var removed = await http.Put(privatePath, new { revision = 2, patch = new { id = "private", disabled = true } }, null)) Assert.Equal(2, (await Json(removed)).GetProperty("revision").GetInt32());
        Assert.Equal(eventsBefore, (await Outbox(running.Host.Services, tenant, HttpTenantA)).Count);
        using (var rebuilt = await http.Put(privatePath, new { revision = 0, patch = new { id = "private", plugin = "test.private@1.0.0", config = new { } } }, null)) Assert.Equal(3, (await Json(rebuilt)).GetProperty("revision").GetInt32());
        using (var old = await http.Put(privatePath, new { revision = 2, patch = new { id = "private", disabled = true } }, null)) Assert.Equal(HttpStatusCode.Conflict, old.StatusCode);
        Assert.Empty(await Outbox(running.Host.Services, tenant, HttpTenantB));
        using (tenant.BeginScope(HttpTenantA))
        {
            var audits = await running.Root.Audit().ReadAsync(0, 100, cancellationToken: Ct);
            Assert.Contains(audits, item => item.Record.Payload.GetProperty("permission").GetString() == RegistryDefinition.ScopeWritePermission);
            Assert.All(audits, item => Assert.Equal("superadmin", item.Record.Payload.GetProperty("bypass").GetString()));
            Assert.True((await running.Root.Audit().VerifyAsync(Ct)).Valid);
        }
        await running.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        using (var noAudit = await http.Put(privatePath, new { revision = 3, patch = new { id = "private", disabled = true } }, null)) Assert.Equal(HttpStatusCode.NotFound, noAudit.StatusCode);
    }

    private static async Task<(int Exit, string Output, string Error)> RunScopeCli(ManagementHost host, params string[] arguments)
    {
        var info = new ProcessStartInfo("node") { WorkingDirectory = RepositoryRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        info.ArgumentList.Add(Path.Combine(RepositoryRoot(), "packages/qian-cli/dist/main.js"));
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var argument in new[] { "--scope", "tenant:" + HttpTenantA, "--url", host.Address.TrimEnd('/'), "--json", "--no-home" }) info.ArgumentList.Add(argument);
        info.Environment["QIAN_ACCESS_TOKEN"] = host.Token(null, "root");
        using var process = Process.Start(info)!; var output = process.StandardOutput.ReadToEndAsync(Ct); var error = process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct); return (process.ExitCode, await output, await error);
    }
}
