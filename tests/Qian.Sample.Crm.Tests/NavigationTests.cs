using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Data;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task UI清单菜单经真实挂载权限与restrict过滤_未挂载资源同404()
    {
        await using var host = await Start(capabilities: true);
        using var anonymous = await host.Client.GetAsync("/api/capabilities/navigation", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var first = await Menus(host, A); Assert.Equal(2, first.Length);
        var customer = Assert.Single(first, item => item.Route == "/customers");
        Assert.Equal("qian:" + A + ":qian.sample.crm:page:%2Fcustomers", customer.ResourceRef);
        Assert.Equal("客户", customer.Title);
        var second = Assert.Single(await Menus(host, B)); Assert.Equal("/customers", second.Route);
        using var denied = await MenuItem(host, B, first.Single(item => item.Route == "/customers/new").ResourceRef.Replace(A, B, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (runtime.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilitiesDefinition.NavigationItemsKey))
        {
            Assert.Empty(await Menus(host, A)); Assert.Single(await Menus(host, B));
            using var hidden = await MenuItem(host, A, customer.ResourceRef); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        Assert.Empty(await Menus(host, A));
        using var unmounted = await MenuItem(host, A, customer.ResourceRef); Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
    }

    [Fact]
    public async Task 外壳目录只列本租户实际挂载且带UI清单的组件_restrict与卸载后不出现_未挂载的远程地址不交出()
    {
        await using var host = await Start(capabilities: true);
        using var anonymous = await host.Client.GetAsync("/api/capabilities/shell", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var shell = await Shell(host, A);
        Assert.Equal(A, shell.GetProperty("tenant").GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(shell.GetProperty("tenant").GetProperty("name").GetString()));
        var remote = Assert.Single(shell.GetProperty("remotes").EnumerateArray());
        Assert.Equal("qian.sample.crm", remote.GetProperty("id").GetString());
        Assert.Equal("1.0.0", remote.GetProperty("version").GetString());
        Assert.Equal("global", remote.GetProperty("scope").GetString());
        // 清单是制品快照里 ui/manifest.json 的原文：远程清单地址、两页的路由与权限与菜单同源。
        var manifest = remote.GetProperty("manifest");
        Assert.Equal("/remotes/qian.sample.crm/mf-manifest.json", manifest.GetProperty("federation").GetProperty("manifest").GetString());
        Assert.Equal(["/customers", "/customers/new"], manifest.GetProperty("pages").EnumerateArray().Select(page => page.GetProperty("route").GetString()!).ToArray());
        Assert.Equal(A, (await Shell(host, A)).GetProperty("tenant").GetProperty("id").GetString());
        Assert.Equal(B, (await Shell(host, B)).GetProperty("tenant").GetProperty("id").GetString());
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (runtime.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilitiesDefinition.ShellRemotesKey))
        {
            Assert.Empty((await Shell(host, A)).GetProperty("remotes").EnumerateArray());
            Assert.Single((await Shell(host, B)).GetProperty("remotes").EnumerateArray());
        }
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        foreach (var tenant in new[] { A, B })
        {
            using var response = await CatalogRequest(host, tenant, "/api/capabilities/shell");
            var text = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + text);
            Assert.Empty(JsonDocument.Parse(text).RootElement.GetProperty("remotes").EnumerateArray());
            Assert.DoesNotContain("mf-manifest.json", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task 真库租户组件修订遮蔽全局菜单_另一租户不变_禁用后不回退()
    {
        await using var host = await Start(capabilities: true);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var repository = new PostgresPatchRepository(host.App.Services.GetRequiredService<IDataService>(), tenant, new SurfaceValidator());
        using (tenant.BeginScope(A))
            await repository.PutAsync(new ScopeKey("tenant", A), "crm", JsonSerializer.SerializeToElement(new { id = "crm", plugin = "qian.sample.crm@1.0.1", config = new { } }), 0, "acceptance", Ct);
        Assert.Equal("租户专属客户", Assert.Single(await Menus(host, A), item => item.Route == "/customers").Title);
        Assert.Equal("客户", Assert.Single(await Menus(host, B)).Title);
        // 外壳目录同一视图：A 换成租户 scope 的 1.0.1 条目（清单随之换成租户专属），B 仍是全局的 1.0.0。
        var scoped = Assert.Single((await Shell(host, A)).GetProperty("remotes").EnumerateArray());
        Assert.Equal(("1.0.1", "tenant:" + A, "租户专属客户"), (scoped.GetProperty("version").GetString(), scoped.GetProperty("scope").GetString(),
            scoped.GetProperty("manifest").GetProperty("pages")[0].GetProperty("nav").GetProperty("title").GetString()));
        var global = Assert.Single((await Shell(host, B)).GetProperty("remotes").EnumerateArray());
        Assert.Equal(("1.0.0", "global"), (global.GetProperty("version").GetString(), global.GetProperty("scope").GetString()));
        using (tenant.BeginScope(A))
            await repository.PutAsync(new ScopeKey("tenant", A), "crm", JsonSerializer.SerializeToElement(new { id = "crm", disabled = true }), 1, "acceptance", Ct);
        Assert.Empty(await Menus(host, A)); Assert.Single(await Menus(host, B));
        Assert.Empty((await Shell(host, A)).GetProperty("remotes").EnumerateArray());
        Assert.Single((await Shell(host, B)).GetProperty("remotes").EnumerateArray());
    }

    [Fact]
    public async Task 菜单偏好按租户主体存真库_不改变权限_审计失败整笔回滚()
    {
        await using var host = await Start(capabilities: true);
        var customer = Assert.Single(await Menus(host, A), item => item.Route == "/customers");
        using var saved = await Preference(host, A, customer.ResourceRef, new NavigationPreference(true, -1));
        Assert.True(saved.StatusCode == HttpStatusCode.NoContent, host.FailureEvidence + await saved.Content.ReadAsStringAsync(Ct));
        Assert.DoesNotContain(await Menus(host, A), item => item.Route == "/customers"); Assert.Single(await Menus(host, B));
        using var direct = await MenuItem(host, A, customer.ResourceRef); Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        using var crossTenant = await Preference(host, B, customer.ResourceRef, new NavigationPreference(false)); Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenant.BeginScope(A))
            await new PostgresTenantRoleStore(host.App.Services.GetRequiredService<IDataService>(), tenant).AssignAsync("tenant.user", "service", "crm-command-reader", "acceptance", Ct);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/capabilities/navigation"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, "crm.read", "crm-command-reader"));
            using var otherSubject = await host.Send(request);
            Assert.Equal(HttpStatusCode.OK, otherSubject.StatusCode);
            var visible = (await otherSubject.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items");
            Assert.Single(visible.EnumerateArray());
            Assert.Equal("/customers", visible[0].GetProperty("route").GetString());
        }
        using (tenant.BeginScope(A))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CapabilitiesDbContext>(Ct);
            var preference = Assert.Single(await session.Context.NavigationPreferences.ToListAsync(Ct));
            Assert.Equal(A, preference.TenantId); Assert.Equal(Writer, preference.SubjectId); Assert.Equal("service", preference.SubjectType); Assert.True(preference.Hidden);
            var audit = host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit();
            var entry = Assert.Single(await audit.ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal("qian.capabilities/navigation.preference", entry.Record.CapabilityId);
            var input = JsonSerializer.SerializeToElement(new { hidden = true, order = -1, resourceRef = customer.ResourceRef });
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(AuditCanonicalizer.Canonicalize(input)))), entry.Record.InputHash);
        }
        await Admin("CREATE FUNCTION plugin_qian_audit.reject_preference() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced rollback'; END $$; CREATE TRIGGER reject_preference BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_preference();");
        using var failed = await Preference(host, A, customer.ResourceRef, new NavigationPreference(false, 9)); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.DoesNotContain(await Menus(host, A), item => item.Route == "/customers");
        await Admin("DROP TRIGGER reject_preference ON plugin_qian_audit.audit_entries;");
        using var restored = await Preference(host, A, customer.ResourceRef, new NavigationPreference(false, 9)); Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.Equal(9, Assert.Single(await Menus(host, A), item => item.Route == "/customers").Order);
        using (tenant.BeginScope(A))
            Assert.Equal(new AuditChainVerification(true, 2, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        using (tenant.BeginScope(B))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CapabilitiesDbContext>(Ct);
            Assert.Empty(await session.Context.NavigationPreferences.ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task 并发修改个人菜单偏好只保留一行_两笔成功更新均入审计链()
    {
        await using var host = await Start(capabilities: true);
        var resource = Assert.Single(await Menus(host, A), item => item.Route == "/customers").ResourceRef;
        var responses = await Task.WhenAll(Preference(host, A, resource, new NavigationPreference(false, 3)), Preference(host, A, resource, new NavigationPreference(false, 8)));
        try { Assert.All(responses, response => Assert.True(response.StatusCode == HttpStatusCode.NoContent, host.FailureEvidence)); }
        finally { foreach (var response in responses) response.Dispose(); }
        using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
        await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CapabilitiesDbContext>(Ct);
        var preference = Assert.Single(await session.Context.NavigationPreferences.ToListAsync(Ct)); Assert.True(preference.Order is 3 or 8);
        Assert.Equal(new AuditChainVerification(true, 2, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
    }

    private static async Task<NavigationItem[]> Menus(RunningApp host, string tenant)
    {
        using var response = await CatalogRequest(host, tenant, "/api/capabilities/navigation");
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("private", value.GetProperty("cacheScope").GetString());
        return value.GetProperty("items").Deserialize<NavigationItem[]>(JsonSerializerOptions.Web)!;
    }
    private static async Task<JsonElement> Shell(RunningApp host, string tenant)
    {
        using var response = await CatalogRequest(host, tenant, "/api/capabilities/shell");
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
        Assert.True(response.Headers.CacheControl is { NoStore: true, Private: true });
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("private", value.GetProperty("cacheScope").GetString());
        return value;
    }
    private static Task<HttpResponseMessage> MenuItem(RunningApp host, string tenant, string resource)
        => CatalogRequest(host, tenant, "/api/capabilities/navigation/item?resourceRef=" + Uri.EscapeDataString(resource));
    private static Task<HttpResponseMessage> Preference(RunningApp host, string tenant, string resource, NavigationPreference preference)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/capabilities/navigation/preference?resourceRef=" + Uri.EscapeDataString(resource)) { Content = JsonContent.Create(preference) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write"));
        return host.Send(request);
    }
}
