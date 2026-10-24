using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class TenantOperationsTests
{
    [Fact]
    public async Task 租户状态HTTP与Outbox同事务_冻结恢复和扫描幂等_业务入口立即拒绝_审批可否决()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        using var created = await Manage(HttpMethod.Post, "/api/tenancy/tenants", new CreateTenant("c", "C") { Administrator = new(Guid.CreateVersion7(), "owner-c") });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var descriptor = (await created.Content.ReadFromJsonAsync<TenantDescriptor>(Ct))!; var id = descriptor.Id.ToString();
        var path = "/api/tenancy/tenants/" + id;
        Assert.Equal([TenancyEvents.Created], await EventTypes(TenancyDbContext.Schema, id));
        var lifecycle = LifecyclePlatform.Lifecycle(business);
        var crm = await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "root", Ct); await lifecycle.ActivateAsync(crm.PluginId, crm.Revision, "root", Ct);
        await lifecycle.EntitleAsync(id, "qian.sample.crm", "root", Ct); await lifecycle.ProvisionAsync(id, "qian.sample.crm", "root", Ct);
        await lifecycle.MountAsync(new ScopeKey("tenant", id), Entry("1.0.0"), "root", Ct);
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(id))
            await source.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "service", Writer, "root", Ct);
        using (var pending = await business.Get(id, "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        var activation = new ActivateTenant(ValidUntil: DateTimeOffset.UtcNow.AddDays(1));
        using (var activated = await Manage(HttpMethod.Post, path + ":activate", activation)) Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        using (var repeated = await Manage(HttpMethod.Post, path + ":activate", activation)) Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using (var allowed = await business.Get(id, "/api/customers")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using (source.On(TenantLifecycle.Approving, DispatchMode.Serial, (Func<TenantTransitionApproval, ValueTask<bool>>)(_ => ValueTask.FromResult(false))))
            await AssertHttp(await Manage(HttpMethod.Post, path + ":suspend"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        foreach (var action in new[] { "suspend", "suspend" })
        { using var response = await Manage(HttpMethod.Post, path + ":" + action); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using (var denied = await business.Get(id, "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var action in new[] { "resume", "resume" })
        { using var response = await Manage(HttpMethod.Post, path + ":" + action); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using (var allowed = await business.Get(id, "/api/customers")) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        // 固定扫描前置的数据库时间，不以等待真实调度器冒充本任务范围。
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET valid_until=now()-interval '1 second' WHERE id='{id}'::uuid;");
        var scan = source.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, "qian.tenancy/tenant.scan-expired");
        var input = JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), limit = 100 });
        Assert.Equal(1, (await scan.InvokeAsync(source, input, Ct)).GetProperty("expired").GetInt32());
        Assert.Equal(0, (await scan.InvokeAsync(source, input, Ct)).GetProperty("expired").GetInt32());
        Assert.Equal(TenantStatus.Grace, (await source.Tenancy().GetByIdAsync(descriptor.Id, Ct))!.Status);
        Assert.Equal(TenantStatus.Active, (await source.Tenancy().GetByIdAsync(Guid.Parse(A), Ct))!.Status);
        using (var expired = await business.Get(id, "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        using (var catalog = await CatalogRequest(business, id, "/api/capabilities"))
            Assert.Empty((await catalog.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        await AssertHttp(await Manage(HttpMethod.Post, path + ":activate", activation), HttpStatusCode.Conflict, "TENANCY_STATE_CONFLICT");
        Assert.Equal([TenancyEvents.Created, TenancyEvents.Activated, TenancyEvents.Suspended, TenancyEvents.Resumed, TenancyEvents.Expired], await EventTypes(TenancyDbContext.Schema, id));
        using var meta = await Manage(HttpMethod.Get, "/api/tenancy/meta/state-machine"); Assert.Equal(HttpStatusCode.OK, meta.StatusCode);

        Task<HttpResponseMessage> Manage(HttpMethod method, string url, object? input = null)
        {
            var message = new HttpRequestMessage(method, url); message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
            if (input is not null) message.Content = JsonContent.Create(input, input.GetType()); return client.SendAsync(message, Ct);
        }
    }
}
