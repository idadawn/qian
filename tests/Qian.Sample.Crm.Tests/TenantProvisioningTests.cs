using System.Text;
using System.Text.Json;
using System.Net;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
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

public abstract partial class CustomerCommandTests
{
    private protected static readonly string[] InitializationCapabilities = ["qian.sample.crm/customer.list"];
}

public sealed partial class TenantOperationsTests
{
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
            // 租户链上只有 Tenancy 第 03 步为 system 组件开通时 Lifecycle 记的开通条目（S1-RT-037），被拒的派发没有留下记录。
            Assert.All(await source.Audit().ReadAsync(0, 50, cancellationToken: Ct), entry => Assert.Equal("qian.lifecycle/tenant-plugin.provision", entry.Record.CapabilityId));
        }
        await provisioning.RetryAsync(descriptor.Id, "platform-operator", Ct);
        Assert.Equal("succeeded", (await provisioning.GetStepsAsync(descriptor.Id, Ct)).Single(step => step.Step == "04:provisioned").Status);
        int chainLength;
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(descriptor.Id.ToString()))
        {
            var entries = await source.Audit().ReadAsync(0, 50, cancellationToken: Ct);
            var audit = Assert.Single(entries, entry => entry.Record.Channel == "hook");
            Assert.Equal(("hook", "platform-operator", "qian.sample.crm/customer.list"), (audit.Record.Channel, audit.Record.ActorId, audit.Record.CapabilityId));
            chainLength = entries.Count;
            Assert.True((await source.Audit().VerifyAsync(Ct)).Valid);
            // 显式补一条测试主体的真实授权，让 HTTP 拒绝只剩 pending 租户策略。
            await source.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "service", Writer, "operator", Ct);
            await AssertCode(PlatformErrors.AuthzDenied, () => loader.GetContext("crm", ScopeKey.Global).Inject<IExecutableCapability>(
                CapabilityBindingRegistration.Key, "qian.sample.crm/customer.list").InvokeAsync(source.Scoped(new ScopeKey("tenant", descriptor.Id.ToString())),
                JsonSerializer.SerializeToElement(new ListCustomersQuery(), ListCustomersQuery.QianCapability.RequestTypeInfo), Ct).AsTask());
        }
        using (var pending = await host.Get(descriptor.Id.ToString(), "/api/customers")) Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        // 续跑：hook 与各组件的开通都不重复记。
        await provisioning.RetryAsync(descriptor.Id, "platform-operator", Ct);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(descriptor.Id.ToString()))
        {
            var entries = await source.Audit().ReadAsync(0, 50, cancellationToken: Ct);
            Assert.Single(entries, entry => entry.Record.Channel == "hook"); Assert.Equal(chainLength, entries.Count);
        }
    }

    [Fact]
    public async Task 真实开通第三步失败后续跑_A和B生命周期幂等_角色与管理员写入_身份组件建立管理员成员_缺席的通知明确跳过()
    {
        using var platform = new LifecyclePlatform();
        // 内置账号模型挂进同一宿主：开通编排第 5 步经身份组件的成员开户端口建立管理员的 Membership（S1-IDP-003）。
        await using var host = await Start(lifecycle: platform, identity: true);
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        var provisioning = source.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        // 管理员必须是已存在的 Person：SubjectId 即 Person 的 sub，也是第 7 步授予 tenant.owner 的主体。
        var (ownerC, phoneC) = await NewPerson(host, "owner-c"); var (ownerD, _) = await NewPerson(host, "owner-d");
        var firstAdmin = new TenantAdministrator(Guid.CreateVersion7(), ownerC.Id.ToString("D"));
        // 尚无激活制品，真实 Lifecycle 在第 3 步拒绝，不用替身冒充上游。
        await Assert.ThrowsAsync<QianErrorException>(() => provisioning.ProvisionAsync(new("c", "C"), firstAdmin, "operator", Ct).AsTask());
        var first = (await source.Tenancy().GetAsync("c", Ct))!;
        var failed = await provisioning.GetStepsAsync(first.Id, Ct);
        Assert.Equal([("01:directory", "succeeded", 1), ("02:isolation", "succeeded", 1)], failed.Take(2).Select(s => (s.Step, s.Status, s.Attempts)));
        Assert.Contains(failed, step => step.Step == "03:components" && step.Status == "failed" && step.Attempts == 1);
        Assert.Contains(failed, step => step.Step == "03:component:qian.audit" && step.Status == "failed" && step.Attempts == 1);
        var secondAdmin = new TenantAdministrator(Guid.CreateVersion7(), ownerD.Id.ToString("D"));
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
            var membershipStep = before.Single(step => step.Step == "05:membership");
            Assert.Equal(("succeeded", null, 1), (membershipStep.Status, membershipStep.Reason, membershipStep.Attempts));
            Assert.Equal("notify_unavailable", before.Single(step => step.Step == "08:notification").Reason);
            Assert.Equal(1, before.Count(step => step.Status == "skipped"));
            // 管理员 Membership 真的建立了：ID 与开通输入一致、是租户管理员、属于该 Person 与该租户。
            Assert.Equal($"true|{admin.SubjectId}", await Text($"SELECT is_tenant_administrator::text || '|' || person_id::text FROM plugin_qian_identity.memberships WHERE id = '{admin.MembershipId}' AND tenant_id = '{descriptor.Id}'"));
            Assert.DoesNotContain(before, step => step.Status is "failed" or "running");
            Assert.Equal("4", await Text($"SELECT count(*)::text FROM plugin_qian_authz.roles WHERE tenant_id='{descriptor.Id}'"));
            Assert.Equal(admin.MembershipId.ToString(), await Text($"SELECT membership_id::text FROM plugin_qian_tenancy.tenant_admins WHERE tenant_id='{descriptor.Id}'"));
            Assert.Equal(admin.SubjectId, await Text($"SELECT a.subject_id FROM plugin_qian_authz.role_assignments a JOIN plugin_qian_authz.roles r ON a.role_id=r.id WHERE a.tenant_id='{descriptor.Id}' AND r.code='tenant.owner'"));
            await provisioning.RetryAsync(descriptor.Id, "operator", Ct);
            Assert.Equal(before.Where(s => s.Status == "succeeded"), (await provisioning.GetStepsAsync(descriptor.Id, Ct)).Where(s => s.Status == "succeeded"));
            Assert.Equal("1", await Text($"SELECT count(*)::text FROM plugin_qian_identity.memberships WHERE tenant_id = '{descriptor.Id}'")); // 重试不重复建立成员
            foreach (var plugin in platform.SystemArtifacts.Keys)
                Assert.Equal(1, Assert.Single(await lifecycle.GetRunsAsync(descriptor.Id.ToString(), plugin, Ct)).Attempts);
        }
        Assert.Equal(first, await provisioning.ProvisionAsync(new("c", "C"), firstAdmin, "operator", Ct));
        await AssertCode(PlatformErrors.Conflict, () => provisioning.ProvisionAsync(new("c", "different"), firstAdmin, "operator", Ct).AsTask());
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(first.Id.ToString()))
            await AssertCode(PlatformErrors.AuthzDenied, () => provisioning.RetryAsync(second.Id, "operator", Ct).AsTask());
        // R95（真实租户目录）：租户仍是 pending，管理员凭据正确也不能登录；激活后经同一条登录流水线登录，会话指向本租户与刚建立的成员。
        async Task<SignInResult> SignInAs(string phone)
        {
            using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(IdentityPassword));
            return await host.Identity.SignIn.SignInAsync(new LoginInput("web", null, null, null, proof) { Identifier = phone }, new LoginContext(), Ct);
        }
        Assert.Equal("TENANT_NOT_ACTIVE", (await SignInAs(phoneC)).Error!.Code.Code);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'active' WHERE id = '{first.Id}'::uuid;");
        var signedIn = await SignInAs(phoneC);
        Assert.Equal((first.Id, firstAdmin.MembershipId), (signedIn.Session!.TenantId, signedIn.Session.MembershipId));

        async Task PrepareCrm(Guid id)
        {
            await lifecycle.EntitleAsync(id.ToString(), "qian.sample.crm", "operator", Ct);
            using var tenant = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(id.ToString());
            await loader.GetContext("registry", ScopeKey.Global).Registry().MountAsync(new ScopeKey("tenant", id.ToString()), Entry("1.0.0"), 0, "operator", Ct);
        }
    }
}
