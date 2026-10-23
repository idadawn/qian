using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Lifecycle;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

// S1-RT-038 验收 3：缺审计时租户级生命周期操作的 503 LIFECYCLE_AUDIT_UNAVAILABLE 经真实管理面宿主按 ErrorDetail 渲染（错误码目录里漏登记只会变成 500）。
// 租户级操作没有自己的 HTTP 端点，仓库里经 HTTP 触发 ProvisionAsync 的只有管理面开户的第 03 步；开户先过平台超管旁路（旁路缺审计时自身 fail-closed），
// 所以由第 02 步的宿主开户器（Kun ITenantProvisioner，CRM 宿主本来不登记）在旁路放行之后撤下审计，让第 03 步的开通以 503 拒绝（建议裁决 ③，2026-10-23 拍板为 A）。
public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 管理面开户途中审计被撤下_第03步的开通以503拒绝并按ErrorDetail渲染_带错误码与操作名_重装审计后重发同一请求续跑完成()
    {
        using var platform = new LifecyclePlatform { Management = true, ProvisionCrmByDefault = true };
        var withdrawal = new AuditWithdrawal();
        await using var business = await Start(lifecycle: platform, configure: builder => builder.Services.AddSingleton<ITenantProvisioner>(withdrawal));
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        var provisioning = source.Inject<ITenantProvisioning>(TenantProvisioning.Key);
        var lifecycle = LifecyclePlatform.Lifecycle(business);
        var create = new CreateTenant("c", "C") { Administrator = new TenantAdministrator(Guid.CreateVersion7(), "owner-c") };
        // 旁路放行后、第 02 步里审计被撤下：第 03 步为第一个 system 组件（qian.audit）开通时 Lifecycle 以 503 拒绝，整个请求按 ErrorDetail 渲染，message 里带操作名。
        withdrawal.Arm(loader);
        using (var response = await Manage(HttpMethod.Post, "/api/tenancy/tenants", create))
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, business.FailureEvidence + body);
            Assert.Equal(ErrorDetailJson.MediaType, response.Content.Headers.ContentType?.MediaType);
            var detail = ErrorDetailJson.Deserialize(body)!;
            Assert.Equal((LifecycleErrors.AuditUnavailable.Code, 503, "/api/tenancy/tenants"), (detail.Code, detail.Status, detail.Instance));
            Assert.Contains("tenant-plugin.provision", detail.Message, StringComparison.Ordinal);
        }
        Assert.Equal(1, withdrawal.Withdrawals);
        // 开户记录停在第 03 步：第一个组件步骤的原因就是该错误码、之后的组件没有步骤；被拒的租户没有安装行、没有执行记录。
        var tenant = (await source.Tenancy().GetAsync("c", Ct))!;
        Assert.Equal(TenantStatus.Pending, tenant.Status);
        var steps = await provisioning.GetStepsAsync(tenant.Id, Ct);
        Assert.Equal(("failed", LifecycleErrors.AuditUnavailable.Code), Outcome(Assert.Single(steps, step => step.Step == "03:components")));
        Assert.Equal(("failed", LifecycleErrors.AuditUnavailable.Code), Outcome(Assert.Single(steps, step => step.Step.StartsWith("03:component:", StringComparison.Ordinal))));
        Assert.Equal("03:component:qian.audit", Assert.Single(steps, step => step.Step.StartsWith("03:component:", StringComparison.Ordinal)).Step);
        Assert.Null(await lifecycle.GetInstallAsync(tenant.Id.ToString(), "qian.audit", Ct));
        Assert.Empty(await lifecycle.GetRunsAsync(tenant.Id.ToString(), "qian.audit", Ct));
        // 重装带平台链的审计后重发同一请求：开户幂等续跑，第 03 步为每个 system 组件（含 CRM）开通完成，租户链上有 CRM 的开通条目。
        await loader.MountAsync([new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { chain = new { platformRole = platform.AuditRole } })), new AuditPlugin(), [AuditDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuditKey])], Ct);
        using (var retried = await Manage(HttpMethod.Post, "/api/tenancy/tenants", create))
            Assert.True(retried.StatusCode == HttpStatusCode.Created, business.FailureEvidence + await retried.Content.ReadAsStringAsync(Ct));
        steps = await provisioning.GetStepsAsync(tenant.Id, Ct);
        var auditStep = Assert.Single(steps, step => step.Step == "03:component:qian.audit");
        Assert.Equal(("succeeded", 2), (auditStep.Status, auditStep.Attempts));
        Assert.Equal("succeeded", Assert.Single(steps, step => step.Step == "03:component:qian.sample.crm").Status);
        Assert.DoesNotContain(steps, step => step.Status is "failed" or "running");
        Assert.Equal((TenantInstallStates.Installed, (string?)"1.0.0"), Pick((await lifecycle.GetInstallAsync(tenant.Id.ToString(), "qian.sample.crm", Ct))!));
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenant.Id.ToString()))
        {
            var entries = await source.Audit().ReadAsync(0, 500, cancellationToken: Ct);
            Assert.True(entries.Count < 500, "租户链条目数到达读取上限：" + entries.Count);
            Assert.Contains(entries, entry => entry.Record.CapabilityId == LifecycleController.ProvisionAuditCapability && entry.Record.Resource == "lifecycle/install:" + tenant.Id + "/qian.sample.crm@1.0.0");
            Assert.Equal(steps.Count(step => step.Step.StartsWith("03:component:", StringComparison.Ordinal)), entries.Count(entry => entry.Record.CapabilityId == LifecycleController.ProvisionAuditCapability));
        }
        Assert.Equal(1, withdrawal.Withdrawals);

        Task<HttpResponseMessage> Manage(HttpMethod method, string url, object? input = null)
        {
            var message = new HttpRequestMessage(method, url); message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
            if (input is not null) message.Content = JsonContent.Create(input, input.GetType()); return client.SendAsync(message, Ct);
        }
        static (string, string?) Outcome(TenantProvisionStep step) => (step.Status, step.Reason);
    }

    // 第 02 步的宿主开户器（Kun ITenantProvisioner）：武装后第一次被调用时撤下 audit，之后无操作；用来在超管旁路放行之后、第 03 步之前拿掉审计。
    private sealed class AuditWithdrawal : ITenantProvisioner
    {
        private PluginLoader? _loader; private int _withdrawals;
        internal int Withdrawals => Volatile.Read(ref _withdrawals);
        internal void Arm(PluginLoader loader) => _loader = loader;
        public async Task ProvisionAsync(TenantInfo tenant, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _loader, null) is not { } loader) return;
            await loader.UnmountAsync("audit", ScopeKey.Global, cancellationToken);
            Interlocked.Increment(ref _withdrawals);
        }
        public Task DeprovisionAsync(string tenantId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
