using System.Net;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;

namespace Qian.Sample.Crm.Tests;

// S1-YUE-012（⑥）：没有单一权限点的已认证端点——处理器内判定的平台表面（能力目录、导航、外壳目录、有效权限）与只要求已认证的端点——
// 也按服务端 identity_revision 下限比对：旧修订 401 UNAUTHENTICATED（不做权限判定，客户端据此换令牌）并记拒绝决策；不带修订的令牌不比对。
public sealed partial class ShellSurfaceTests
{
    [Fact]
    public async Task 处理器内判定与只要求已认证的端点也比对identity_revision_旧令牌401且记拒绝决策_不带修订的令牌不比对()
    {
        await using var host = await Start(capabilities: true, decisionLogSampling: 1);
        Task<HttpResponseMessage> Call(string path, long? revision) => host.Send(Get(path, host.Token(A, "crm.read crm.write", identityRevision: revision)));
        // 见到修订 3 的已验证令牌即把下限懒抬高到 3（只增不减）。
        using (var seen = await Call("/api/authz/me/permissions", 3)) Assert.True(seen.StatusCode == HttpStatusCode.OK, host.FailureEvidence);
        foreach (var path in new[] { "/api/authz/me/permissions", "/api/authz/meta", "/api/capabilities", "/api/capabilities/navigation", "/api/capabilities/shell" })
        {
            using (var stale = await Call(path, 2))
            {
                Assert.True(stale.StatusCode == HttpStatusCode.Unauthorized, path + " " + await stale.Content.ReadAsStringAsync(Ct));
                Assert.Equal("UNAUTHENTICATED", await Code(stale));
                Assert.True(stale.Headers.CacheControl is { NoStore: true }, path);
            }
            using (var current = await Call(path, 3)) Assert.True(current.StatusCode == HttpStatusCode.OK, path + " " + host.FailureEvidence);
            using (var bare = await Call(path, null)) Assert.True(bare.StatusCode == HttpStatusCode.OK, path);
        }
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
        {
            var denied = (await Decisions(host, A)).Where(item => item.Result == "denied").ToArray();
            foreach (var target in new[] { "GET /api/authz/me/permissions", "GET /api/authz/meta", "GET /api/capabilities", "GET /api/capabilities/navigation", "GET /api/capabilities/shell" })
                Assert.Contains((Writer, "denied", AuthzEnforcement.HttpEntryPoint, target, AuthzEnforcement.StaleIdentityRevision), denied);
        }
    }
}
