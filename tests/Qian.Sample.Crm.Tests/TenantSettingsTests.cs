using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Registry.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 租户可用六步_真实开户hook状态事件_配置写入跨宿主与合成视图一致_配额缺席明确_双租户隔离()
    {
        using var platform = new LifecyclePlatform { Management = true, ProvisionCrmByDefault = true, InitializationCapabilities = InitializationCapabilities };
        await using var business = await Start(lifecycle: platform);
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        await loader.MountAsync([new PluginDefinition(new PluginEntry("crm", "qian.sample.crm", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new CrmPlugin(), [CrmDefinition.Key])], Ct);
        await AttachHooks(business.App);
        var hookDirectory = (string)((IApplicationBuilder)business.App).Properties["qian.tests.hooks"]!;
        File.WriteAllText(Path.Combine(hookDirectory, "hooks/hooks.json"), """{"hooks":{"tenant/provisioned":[{"hooks":[{"type":"plugin","plugin":"qian.sample.crm","handler":"listCustomers"}]}]}}""");
        await WaitForHooks(loader.GetContext("crm", ScopeKey.Global), group => group.GetProperty("hooks")[0].GetProperty("handler").GetString() == "listCustomers");
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        // 先装组件 hook 与观察器，再由运营宿主重挂载 Authz；验证角色创建不依赖注册先后。
        using var rolesBeforeHooks = source.On(SurfaceHookEvents.TenantProvisioned, DispatchMode.Serial,
            (Func<HookNotification, ValueTask<JsonElement>>)(async notification =>
            {
                Assert.Equal("4", await Text($"SELECT count(*)::text FROM plugin_qian_authz.roles WHERE tenant_id='{notification.Subject}'"));
                return JsonSerializer.SerializeToElement<object?>(null);
            }), priority: 1);
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        var created = new List<TenantDescriptor>();
        foreach (var code in new[] { "c", "d" })
        {
            using var response = await Manage(HttpMethod.Post, "/api/tenancy/tenants", new CreateTenant(code, code.ToUpperInvariant())
                { Administrator = new(Guid.CreateVersion7(), "owner-" + code), Quotas = new TenantQuotas(5) });
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
            var tenant = (await response.Content.ReadFromJsonAsync<TenantDescriptor>(Ct))!; created.Add(tenant); Assert.Equal(TenantStatus.Pending, tenant.Status);
            var steps = await source.Inject<ITenantProvisioning>(TenantProvisioning.Key).GetStepsAsync(tenant.Id, Ct);
            Assert.Equal(2, steps.Count(step => step.Status == "skipped"));
            Assert.Contains(steps, step => step.Step == "03:component:qian.sample.crm" && step.Status == "succeeded");
            Assert.Equal(1, Assert.Single(await LifecyclePlatform.Lifecycle(business).GetRunsAsync(tenant.Id.ToString(), "qian.sample.crm", Ct)).Attempts);
            using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenant.Id.ToString()))
            {
                // 租户链：开户 hook 一条（root、hook 通道），其余是 Tenancy 第 03 步为每个 system 组件调用开通时 Lifecycle 记的开通条目（S1-RT-037）。
                var entries = await source.Audit().ReadAsync(0, 50, cancellationToken: Ct);
                Assert.Equal("root", Assert.Single(entries, entry => entry.Record.Channel == "hook").Record.ActorId);
                Assert.Equal(steps.Count(step => step.Step.StartsWith("03:component:", StringComparison.Ordinal)), entries.Count - 1);
                Assert.Equal(entries.Count - 1, entries.Count(entry => entry.Record.CapabilityId == "qian.lifecycle/tenant-plugin.provision"));
                await source.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "service", Writer, "root", Ct);
            }
        }
        var a = created[0]; var b = created[1]; var path = "/api/tenancy/tenants/" + a.Id;
        using (var pending = await business.Get(a.Id.ToString(), "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        foreach (var tenant in created)
        { using var active = await Manage(HttpMethod.Post, "/api/tenancy/tenants/" + tenant.Id + ":activate", new ActivateTenant(ValidUntil: DateTimeOffset.UtcNow.AddDays(1))); Assert.Equal(HttpStatusCode.OK, active.StatusCode); }
        using (var active = await business.Get(a.Id.ToString(), "/api/customers")) Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        await using var replica = await SettingsReplica(platform);
        using (replica.Tenant.BeginScope(a.Id.ToString())) Assert.Null(await replica.Settings.Get<string>("qian.audit", "chain.store", Ct));
        business.App.Configuration["qian.audit:policyEnabled"] = "true";
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(a.Id.ToString()))
            Assert.True(await source.Inject<ITenantSettings>(TenantSettings.Key).Get<bool?>("qian.audit", "policyEnabled", Ct));
        var registry = loader.GetContext("registry", ScopeKey.Global).Registry();
        var mountedNotifications = 0;
        using var listener = source.On(SurfaceHookEvents.EntryMounted, DispatchMode.Serial, (Func<HookNotification, ValueTask<JsonElement>>)(notification =>
        { if (notification.Source.Entry.Plugin == "qian.registry" && notification.Source.Scope.Id == a.Id.ToString()) mountedNotifications++; return ValueTask.FromResult(JsonSerializer.SerializeToElement<object?>(null)); }));
        var invalid = new UpdateTenantSettings(JsonSerializer.SerializeToElement(new { chain = new { store = "invalid" } }), 0);
        using (var rejected = await Manage(HttpMethod.Put, path + "/settings/qian.audit", invalid))
        {
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var detail = ErrorDetailJson.Deserialize(await rejected.Content.ReadAsStringAsync(Ct))!;
            Assert.Contains(detail.Errors!, field => field.Pointer.Contains("config", StringComparison.Ordinal));
        }
        var elapsed = Stopwatch.StartNew();
        using (var put = await Manage(HttpMethod.Put, path + "/settings/qian.audit", new UpdateTenantSettings(JsonSerializer.SerializeToElement(new { chain = new { store = "pg" } }), 0)))
        { Assert.True(put.StatusCode == HttpStatusCode.OK, await put.Content.ReadAsStringAsync(Ct)); Assert.Equal(1, (await put.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!.Revision); }
        await AssertHttp(await Manage(HttpMethod.Put, path + "/settings/qian.audit", new UpdateTenantSettings(JsonSerializer.SerializeToElement(new { chain = new { store = "pg" } }), 0)), HttpStatusCode.Conflict, "REGISTRY_REVISION_CONFLICT");
        await AssertHttp(await Manage(HttpMethod.Put, path + "/settings/qian.config", new UpdateTenantSettings(JsonSerializer.SerializeToElement(new { }), 0)), HttpStatusCode.BadRequest, "INPUT_INVALID");
        Assert.Equal(1, mountedNotifications);
        using (replica.Tenant.BeginScope(a.Id.ToString()))
        {
            while (await replica.Settings.Get<string>("qian.audit", "chain.store", Ct) != "pg")
            { Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5)); await Task.Delay(25, Ct); }
        }
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
        var propagation = elapsed.Elapsed;
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(a.Id.ToString()))
        {
            var entry = Assert.Single(await registry.GetConfigurationViewAsync(new ScopeKey("tenant", a.Id.ToString()), Ct), entry => entry.PluginId == "qian.audit");
            Assert.Equal("pg", entry.Config!.Value.GetProperty("chain").GetProperty("store").GetString());
            Assert.Equal("pg", await source.Inject<ITenantSettings>(TenantSettings.Key).Get<string>("qian.audit", "chain.store", Ct));
            Assert.Contains(await source.Audit().ReadAsync(0, 20, cancellationToken: Ct), record => record.Record.CapabilityId == "qian.tenancy/settings.write" && record.Record.Resource == "qian.audit");
            var quota = source.Inject<ITenantQuota>(TenantSettings.QuotaKey);
            Assert.Equal("CARDINALITY_USER_LIMIT", (await Assert.ThrowsAsync<QianErrorException>(() => quota.EnsureCanAddUserAsync(5, TenantMemberKind.Administrator, Ct).AsTask())).Error.Code.Code);
            await quota.EnsureCanAddUserAsync(5, TenantMemberKind.PlatformSuperadmin, Ct);
        }
        using (var view = await business.Get(a.Id.ToString(), "/api/registry/scopes/tenant:" + a.Id + "/entries"))
        {
            Assert.True(view.StatusCode == HttpStatusCode.OK, await view.Content.ReadAsStringAsync(Ct));
            var json = await view.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("pg", json.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("pluginId").GetString() == "qian.audit")
                .GetProperty("config").GetProperty("chain").GetProperty("store").GetString());
        }
        using (replica.Tenant.BeginScope(b.Id.ToString())) Assert.Null(await replica.Settings.Get<string>("qian.audit", "chain.store", Ct));
        using (var response = await Manage(HttpMethod.Get, path + "/quota"))
        { var quota = (await response.Content.ReadFromJsonAsync<TenantQuotaView>(Ct))!; Assert.Equal(5, quota.Limits.Users); Assert.Null(quota.UsersUsed); Assert.False(quota.UsageAvailable); }
        foreach (var (action, expected) in new[] { ("suspend", HttpStatusCode.Forbidden), ("resume", HttpStatusCode.OK) })
        { using var response = await Manage(HttpMethod.Post, path + ":" + action); Assert.Equal(HttpStatusCode.OK, response.StatusCode); using var command = await business.Get(a.Id.ToString(), "/api/customers"); Assert.Equal(expected, command.StatusCode); }
        var recordB = await Text($"SELECT id::text FROM plugin_qian_sample_crm.customers WHERE tenant_id='{b.Id}' LIMIT 1");
        using (var cross = await business.Get(a.Id.ToString(), "/api/customers/" + recordB)) Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET valid_until=now()-interval '1 second' WHERE id='{a.Id}'::uuid;");
        var scan = source.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, "qian.tenancy/tenant.scan-expired");
        await scan.InvokeAsync(source, JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7() }), Ct);
        using (var expired = await business.Get(a.Id.ToString(), "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
        Assert.Contains(TenancyEvents.Expired, await EventTypes(TenancyDbContext.Schema, a.Id.ToString()));
        // 以仍 active 的 B 请求 A 的客户，和不存在的记录同为404。
        var recordA = await Text($"SELECT id::text FROM plugin_qian_sample_crm.customers WHERE tenant_id='{a.Id}' LIMIT 1");
        using (var cross = await business.Get(b.Id.ToString(), "/api/customers/" + recordA)) Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        var audit = await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct);
        Assert.Contains(audit, entry => entry.Record.Payload.TryGetProperty("bypass", out var bypass) && bypass.GetString() == "superadmin");
        TestContext.Current.TestOutputHelper?.WriteLine($"跨宿主配置生效 {propagation.TotalMilliseconds:F0} ms，Identity 用量缺席，六步流程通过。");

        Task<HttpResponseMessage> Manage(HttpMethod method, string url, object? input = null)
        {
            var message = new HttpRequestMessage(method, url); message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
            if (input is not null) message.Content = JsonContent.Create(input, input.GetType()); return client.SendAsync(message, Ct);
        }
    }

    private static void MapSettingsRegistry(WebApplication app) => app.MapQianRegistry(http =>
        http.RequestServices.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("registry", ScopeKey.Global));

    private async Task<SettingsReaderHost> SettingsReplica(LifecyclePlatform platform)
    {
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(registrar => registrar.RegisterTenancyEvents().RegisterRegistryEvents().RegisterAuthzEvents());
        builder.Services.AddKunMediator(mediator => mediator.AddQianTenancyEvents().AddQianRegistryEvents().AddQianAuthzEvents()); builder.Services.AddQianEvents();
        builder.Services.AddQianData<TenancyDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(TenancyDbContext.Schema));
        builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(RegistryDbContext.Schema));
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        var host = builder.Build(); await host.StartAsync(Ct); var loader = new PluginLoader(host.Services);
        var original = platform.RegistryOptions;
        var registry = new RegistryPlugin(new RegistryRuntimeOptions { Runtime = loader, InstalledComponents = original.InstalledComponents, Profiles = original.Profiles,
            Profile = original.Profile, Surfaces = original.Surfaces, Resolve = original.Resolve });
        await loader.MountAsync([
            Definition("data", new DataPlugin(), [DataDefinition.Key]), Definition("audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]),
            Definition("registry", registry, [RegistryDefinition.Key]), Definition("tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("registry", ScopeKey.Global));
        await loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(Ct);
        return new SettingsReaderHost(host, loader);
        static PluginDefinition Definition(string id, IQianPlugin plugin, ServiceKey[] keys) => new(new PluginEntry(id, "qian." + id, "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), plugin, keys);
    }
    private sealed class SettingsReaderHost(IHost host, PluginLoader loader) : IAsyncDisposable
    {
        internal ITenantContext Tenant => host.Services.GetRequiredService<ITenantContext>();
        internal ITenantSettings Settings => loader.GetContext("tenancy", ScopeKey.Global).Inject<ITenantSettings>(TenantSettings.Key);
        public async ValueTask DisposeAsync() { await loader.GetContext("registry", ScopeKey.Global).Registry().DisposeAsync(); await loader.DisposeAsync(); await host.StopAsync(Ct); host.Dispose(); }
    }
}
