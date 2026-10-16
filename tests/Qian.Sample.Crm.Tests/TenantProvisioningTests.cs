using System.Text.Json;
using System.Net;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk;
using Microsoft.AspNetCore.Builder;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Lifecycle;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    private static readonly string[] InitializationCapabilities = ["qian.sample.crm/customer.list"];
    [Fact]
    public async Task 真实开通事件接SDK业务hook时保留拒绝_不伪造租户身份或越过pending策略()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        foreach (var artifact in platform.SystemArtifacts.Values)
        {
            var installed = await lifecycle.InstallAsync(artifact, "operator", Ct);
            await lifecycle.ActivateAsync(installed.PluginId, installed.Revision, "operator", Ct);
        }
        await loader.MountAsync([new PluginDefinition(new PluginEntry("crm", "qian.sample.crm", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new CrmPlugin(), [CrmDefinition.Key])], Ct);
        await AttachHooks(host.App);
        var directory = (string)((IApplicationBuilder)host.App).Properties["qian.tests.hooks"]!;
        File.WriteAllText(Path.Combine(directory, "hooks/hooks.json"), """{"hooks":{"tenant/provisioned":[{"hooks":[{"type":"plugin","plugin":"qian.sample.crm","handler":"listCustomers"}]}]}}""");
        await WaitForHooks(loader.GetContext("crm", ScopeKey.Global), group => group.GetProperty("hooks")[0].GetProperty("handler").GetString() == "listCustomers");
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        var provisioning = source.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        await AssertCode(PlatformErrors.AuthzDenied, () => provisioning.ProvisionAsync(new("c", "C"), new(Guid.CreateVersion7(), "owner-c"), "operator", Ct).AsTask());
        var descriptor = (await source.Tenancy().GetAsync("c", Ct))!;
        Assert.Equal(TenantStatus.Pending, descriptor.Status);
        Assert.Contains(await provisioning.GetStepsAsync(descriptor.Id, Ct), step => step.Step == "04:provisioned" && step.Status == "failed" && step.Reason == "AUTHZ_DENIED");
        // R96 要求初始授权在派发之后，失败不能越过该顺序给未完成租户授予 owner。
        Assert.Equal("0", await Text($"SELECT count(*)::text FROM plugin_qian_authz.role_assignments WHERE tenant_id='{descriptor.Id}'"));

        // 仅部署配置中的精确清单允许开户执行；不向请求体、普通 hook 或常规命令签发凭据。
        await loader.UnmountAsync("tenancy", ScopeKey.Global, Ct);
        await loader.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { initializationCapabilities = InitializationCapabilities })),
            new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey])], Ct);
        source = loader.GetContext("tenancy", ScopeKey.Global);
        provisioning = source.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        // 同一个已配置的生产方上下文，测试宿主直接派发没有编排凭据，不能借 hook 执行业务。
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(descriptor.Id.ToString()))
        {
            var scoped = source.Scoped(new ScopeKey("tenant", descriptor.Id.ToString()));
            await AssertCode(PlatformErrors.AuthzDenied, () => scoped.DispatchAsync(Qian.Surfaces.Abstractions.SurfaceHookEvents.TenantProvisioned,
                new Qian.Surfaces.Abstractions.HookNotification(scoped, descriptor.Id.ToString(), JsonSerializer.SerializeToElement(new { }), Ct), Ct).AsTask());
            Assert.Empty(await source.Audit().ReadAsync(0, 20, cancellationToken: Ct));
        }
        await provisioning.RetryAsync(descriptor.Id, "platform-operator", Ct);
        Assert.Equal("succeeded", (await provisioning.GetStepsAsync(descriptor.Id, Ct)).Single(step => step.Step == "04:provisioned").Status);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(descriptor.Id.ToString()))
        {
            var audit = Assert.Single(await source.Audit().ReadAsync(0, 20, cancellationToken: Ct));
            Assert.Equal(("hook", "platform-operator", "qian.sample.crm/customer.list"), (audit.Record.Channel, audit.Record.ActorId, audit.Record.CapabilityId));
            Assert.True((await source.Audit().VerifyAsync(Ct)).Valid);
            // 显式补一条测试主体的真实授权，让 HTTP 拒绝只剩 pending 租户策略。
            await source.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "service", Writer, "operator", Ct);
            await AssertCode(PlatformErrors.AuthzDenied, () => loader.GetContext("crm", ScopeKey.Global).Inject<IExecutableCapability>(
                CapabilityBindingRegistration.Key, "qian.sample.crm/customer.list").InvokeAsync(source.Scoped(new ScopeKey("tenant", descriptor.Id.ToString())),
                JsonSerializer.SerializeToElement(new ListCustomersQuery(), ListCustomersQuery.QianCapability.RequestTypeInfo), Ct).AsTask());
        }
        using (var pending = await host.Get(descriptor.Id.ToString(), "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        await provisioning.RetryAsync(descriptor.Id, "platform-operator", Ct);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(descriptor.Id.ToString()))
            Assert.Single(await source.Audit().ReadAsync(0, 20, cancellationToken: Ct));
    }

    [Fact]
    public async Task 真实开通第三步失败后续跑_A和B生命周期幂等_角色与管理员写入_缺席成员与通知明确跳过()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        var provisioning = source.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        var firstAdmin = new TenantAdministrator(Guid.CreateVersion7(), "owner-c");
        // 尚无激活制品，真实 Lifecycle 在第 3 步拒绝，不用替身冒充上游。
        await Assert.ThrowsAsync<QianErrorException>(() => provisioning.ProvisionAsync(new("c", "C"), firstAdmin, "operator", Ct).AsTask());
        var first = (await source.Tenancy().GetAsync("c", Ct))!;
        var failed = await provisioning.GetStepsAsync(first.Id, Ct);
        Assert.Equal([("01:directory", "succeeded", 1), ("02:isolation", "succeeded", 1)], failed.Take(2).Select(s => (s.Step, s.Status, s.Attempts)));
        Assert.Contains(failed, step => step.Step == "03:components" && step.Status == "failed" && step.Attempts == 1);
        Assert.Contains(failed, step => step.Step == "03:component:qian.audit" && step.Status == "failed" && step.Attempts == 1);
        var secondAdmin = new TenantAdministrator(Guid.CreateVersion7(), "owner-d");
        await Assert.ThrowsAsync<QianErrorException>(() => provisioning.ProvisionAsync(new("d", "D"), secondAdmin, "operator", Ct).AsTask());
        var second = (await source.Tenancy().GetAsync("d", Ct))!;

        var lifecycle = LifecyclePlatform.Lifecycle(host);
        foreach (var artifact in platform.SystemArtifacts.Values)
        {
            var installed = await lifecycle.InstallAsync(artifact, "operator", Ct);
            await lifecycle.ActivateAsync(installed.PluginId, installed.Revision, "operator", Ct);
        }
        var crm = await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "operator", Ct);
        await lifecycle.ActivateAsync(crm.PluginId, crm.Revision, "operator", Ct);
        await PrepareCrm(first.Id);
        Assert.Equal(first, await provisioning.RetryAsync(first.Id, "operator", Ct));
        await PrepareCrm(second.Id);
        Assert.Equal(second, await provisioning.RetryAsync(second.Id, "operator", Ct));
        Assert.Equal(TenantStatus.Pending, second.Status);
        // 两个租户已挂载的 CRM 均由真实租户开通编排调用 Lifecycle，不能只引用组件层已有验收。
        foreach (var pair in new[] { (first, firstAdmin), (second, secondAdmin) })
        {
            var (descriptor, admin) = pair;
            var run = Assert.Single(await lifecycle.GetRunsAsync(descriptor.Id.ToString(), "qian.sample.crm", Ct));
            Assert.Equal((LifecycleRunStates.Succeeded, 1), (run.Status, run.Attempts));
            var before = await provisioning.GetStepsAsync(descriptor.Id, Ct);
            Assert.Equal(1, before.Single(step => step.Step == "01:directory").Attempts);
            Assert.Equal(1, before.Single(step => step.Step == "02:isolation").Attempts);
            Assert.Equal(2, before.Single(step => step.Step == "03:components").Attempts);
            Assert.Equal(2, before.Single(step => step.Step == "03:component:qian.audit").Attempts);
            Assert.Equal("identity_unavailable", before.Single(step => step.Step == "05:membership").Reason);
            Assert.Equal("notify_unavailable", before.Single(step => step.Step == "08:notification").Reason);
            Assert.Equal(2, before.Count(step => step.Status == "skipped"));
            Assert.DoesNotContain(before, step => step.Status is "failed" or "running");
            Assert.Equal("4", await Text($"SELECT count(*)::text FROM plugin_qian_authz.roles WHERE tenant_id='{descriptor.Id}'"));
            Assert.Equal(admin.MembershipId.ToString(), await Text($"SELECT membership_id::text FROM plugin_qian_tenancy.tenant_admins WHERE tenant_id='{descriptor.Id}'"));
            Assert.Equal(admin.SubjectId, await Text($"SELECT a.subject_id FROM plugin_qian_authz.role_assignments a JOIN plugin_qian_authz.roles r ON a.role_id=r.id WHERE a.tenant_id='{descriptor.Id}' AND r.code='tenant.owner'"));
            await provisioning.RetryAsync(descriptor.Id, "operator", Ct);
            Assert.Equal(before.Where(s => s.Status == "succeeded"), (await provisioning.GetStepsAsync(descriptor.Id, Ct)).Where(s => s.Status == "succeeded"));
            foreach (var plugin in platform.SystemArtifacts.Keys)
                Assert.Equal(1, Assert.Single(await lifecycle.GetRunsAsync(descriptor.Id.ToString(), plugin, Ct)).Attempts);
        }
        Assert.Equal(first, await provisioning.ProvisionAsync(new("c", "C"), firstAdmin, "operator", Ct));
        await AssertCode(PlatformErrors.Conflict, () => provisioning.ProvisionAsync(new("c", "different"), firstAdmin, "operator", Ct).AsTask());
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(first.Id.ToString()))
            await AssertCode(PlatformErrors.AuthzDenied, () => provisioning.RetryAsync(second.Id, "operator", Ct).AsTask());

        async Task PrepareCrm(Guid id)
        {
            await lifecycle.EntitleAsync(id.ToString(), "qian.sample.crm", "operator", Ct);
            using var tenant = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(id.ToString());
            await loader.GetContext("registry", ScopeKey.Global).Registry().MountAsync(new ScopeKey("tenant", id.ToString()), Entry("1.0.0"), 0, "operator", Ct);
        }
    }
}
