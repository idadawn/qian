using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Runtime;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 平台入口租户主体与上下文不匹配均留拒绝日志且无放行审计()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var logs = new OperationsLog();
        await using var management = await ManagementHost(business, platform, logs, () => loader.GetContext("registry", ScopeKey.Global));
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        foreach (var item in new[] { (Tenant: (string?)A, Status: HttpStatusCode.Forbidden, Reason: "tenant_principal"), (Tenant: (string?)null, Status: HttpStatusCode.NotFound, Reason: "context_mismatch") })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(item.Tenant, "", "root", "user"));
            using var response = await client.GetAsync("/api/tenancy/tenants", Ct);
            Assert.Equal(item.Status, response.StatusCode);
            Assert.DoesNotContain(item.Reason, await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
            Assert.Equal(item.Reason, logs.Entries.Last()["Reason"]);
            Assert.Equal("root", logs.Entries.Last()["Subject"]);
        }
        Assert.Equal(2, logs.Entries.Count);
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        // 平台链上只有引导时安装、激活 system 组件的生命周期条目（S1-RT-037），没有放行审计。
        Assert.All(await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct), entry => Assert.StartsWith("qian.lifecycle/plugin-revision.", entry.Record.CapabilityId, StringComparison.Ordinal));
    }
}
