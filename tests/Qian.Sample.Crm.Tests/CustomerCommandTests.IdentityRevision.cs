using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Runtime;

namespace Qian.Sample.Crm.Tests;

// S1-AZ-009（S1-DEC-027，R7）：真实身份组件与授权组件同在参考宿主（同进程）。撤销授予后 3 秒内：
// - 身份修订递增；
// - 旧令牌在身份侧（userinfo）与 PEP 侧都被拒；
// - refresh 拿到新修订，并被两侧放行。
// PEP 侧直接调三处入口共用的执行端口，主体与修订取自真实的身份令牌：参考宿主的 REST 端点按设计只收机器主体，
// 陈旧令牌在 HTTP 映射为 401、在 gRPC 映射为 Unauthenticated，由 AuthzEnforcementTests 覆盖。
public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 撤销授予后三秒内身份修订递增_旧令牌在userinfo与PEP执行端口都被拒_refresh拿到新修订并被放行_非user与无成员身份的主体不递增()
    {
        await using var host = await Start(identity: true);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var authz = Runtime(host).Loader.GetContext("authz", ScopeKey.Global);
        var (person, phone) = await NewPerson(host, "成员甲"); var member = await Join(host, A, person.Id, "成员甲");
        var subject = person.Id.ToString("D");
        Task<string?> MemberRevision() => Text($"SELECT identity_revision FROM plugin_qian_identity.memberships WHERE id = '{member.Id:D}'::uuid");
        // 与 HTTP / gRPC / 命令入口同一个执行端口；主体、租户与修订只取自已签发的访问令牌。
        async Task<AuthorizationDecision> Enforce(string accessToken)
        {
            var token = new JsonWebToken(accessToken);
            using var scope = tenant.BeginScope(A);
            return await Runtime(host).AuthzSource().Enforcement().EnforceAsync(new EnforcementRequest(
                new EnforcementSubject(token.Subject, token.GetClaim("tenant_id").Value, token.GetClaim("principal_kind").Value, TokenRevision(accessToken)),
                CustomerRead, null, "api", AuthzEnforcement.CommandEntryPoint, "qian.sample.crm/customer.list"), Ct);
        }

        // 生产路径的授予端口（开通编排同款）授予 tenant.user（含 customer:read）与 tenant.developer：每次授予提交后递增该成员在 A 的修订。
        using (tenant.BeginScope(A))
        {
            var grants = authz.Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
            await grants.AssignAsync("tenant.user", "user", subject, "acceptance", Ct);
            await grants.AssignAsync("tenant.developer", "user", subject, "acceptance", Ct);
        }
        Assert.Equal("2", await MemberRevision());
        var first = await UserTokens(host, phone);
        Assert.Equal((subject, 2L), (new JsonWebToken(first.AccessToken).Subject, TokenRevision(first.AccessToken)));
        var allowed = await Enforce(first.AccessToken);
        Assert.True(allowed.Allow, allowed.Reason);
        Assert.Equal(HttpStatusCode.OK, await UserInfoStatus(host, first.AccessToken));

        // 撤销 tenant.developer（与组件入口同一装配的角色存储）：提交后在同一调用内递增修订，并把 PEP 下限抬到新值。
        var elapsed = Stopwatch.StartNew();
        using (tenant.BeginScope(A)) Assert.True(await AuthzPlugin.RoleStore(authz, tenant).RevokeAsync("tenant.developer", "user", subject, "acceptance", Ct));
        Assert.Equal("3", await MemberRevision());
        Assert.Equal(HttpStatusCode.Unauthorized, await UserInfoStatus(host, first.AccessToken));
        // 仍持有 tenant.user（customer:read），旧令牌照样被拒：原因是令牌陈旧，不是缺权限。
        var stale = await Enforce(first.AccessToken);
        Assert.Equal((false, AuthzEnforcement.StaleIdentityRevision), (stale.Allow, stale.Reason));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3), $"撤销授予 {elapsed.Elapsed.TotalMilliseconds:F0} ms 后旧令牌才在两侧失效，超过 3 秒。");
        Assert.Equal("3", await Text($"SELECT identity_revision FROM plugin_qian_authz.subject_identity_revisions WHERE tenant_id = '{A}' AND subject_type = 'user' AND subject_id = '{subject}'"));

        // refresh：会话没有被吊销，换到带新修订的令牌，身份侧与 PEP 都放行；拒绝进了决策日志。
        var refreshed = await RefreshAccessToken(host, first.RefreshToken!);
        Assert.Equal(3, TokenRevision(refreshed));
        Assert.Equal(HttpStatusCode.OK, await UserInfoStatus(host, refreshed));
        var renewed = await Enforce(refreshed);
        Assert.True(renewed.Allow, renewed.Reason);
        using (tenant.BeginScope(A))
            Assert.Contains((subject, "denied", AuthzEnforcement.CommandEntryPoint, "qian.sample.crm/customer.list", AuthzEnforcement.StaleIdentityRevision), await Decisions(host, A));

        // 非 user 主体、不是 Person 的 user 主体、在本租户没有成员身份的 Person：授予照常提交，不递增任何修订、不报错。
        var stranger = (await NewPerson(host, "路人")).Person;
        using (tenant.BeginScope(A))
        {
            var grants = authz.Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
            await grants.AssignAsync("tenant.user", "service", Reader, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", "not-a-person", "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", stranger.Id.ToString("D"), "acceptance", Ct);
        }
        Assert.Equal("3", await MemberRevision());
        Assert.Equal("0", await Text($"SELECT count(*) FROM plugin_qian_identity.memberships WHERE person_id = '{stranger.Id:D}'::uuid"));
    }

    private static long TokenRevision(string accessToken) =>
        long.Parse(new JsonWebToken(accessToken).GetClaim("identity_revision").Value, NumberStyles.None, CultureInfo.InvariantCulture);

    // 以 web 客户端用 refresh token 换令牌，返回新的访问令牌。
    private static async Task<string> RefreshAccessToken(RunningApp host, string refreshToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(host.Identity.Issuer) };
        using var response = await http.PostAsync("connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = "web", ["client_secret"] = host.Identity.Secret
        }), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;
    }
}
