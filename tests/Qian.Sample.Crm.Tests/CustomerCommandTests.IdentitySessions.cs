using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Runtime;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    // 以 web 客户端的身份用 refresh token 换令牌，返回状态码（被拒是 400）。
    private protected static async Task<HttpStatusCode> RefreshStatus(RunningApp host, string refreshToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(host.Identity.Issuer) };
        using var response = await http.PostAsync("connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = "web", ["client_secret"] = host.Identity.Secret
        }), Ct);
        return response.StatusCode;
    }

    // 用访问令牌请求 userinfo：令牌仍对应有效会话是 200，会话失效或修订过期是 401。
    private protected static async Task<HttpStatusCode> UserInfoStatus(RunningApp host, string accessToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(host.Identity.Issuer) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, Ct);
        return response.StatusCode;
    }
}

// 会话中心挂进真实的 CRM 参考宿主（S1-IDP-004，R95）：租户冻结 / 恢复走真实 Tenancy 的状态机，
// 身份侧的会话校验读真实的租户目录，后台对账把失效持久化并发出事件。
public sealed partial class IdentityAuthzTests
{
    [Fact]
    public async Task 真实Tenancy冻结租户后该租户的会话失效_refresh被拒_对账持久吊销并发事件_别的租户不受影响_恢复后可重新登录()
    {
        await using var host = await Start(identity: true);
        var lifecycle = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global).Inject<ITenantLifecycle>(TenantLifecycle.Key);
        var (personA, phoneA) = await NewPerson(host, "甲方管理员"); await Join(host, A, personA.Id, "甲方管理员", administrator: true);
        var (personB, phoneB) = await NewPerson(host, "乙方管理员"); await Join(host, B, personB.Id, "乙方管理员", administrator: true);
        var tokensA = await UserTokens(host, phoneA); var tokensB = await UserTokens(host, phoneB);
        var sessionA = new JsonWebToken(tokensA.AccessToken).GetClaim("sid").Value;
        Assert.Equal(HttpStatusCode.OK, await UserInfoStatus(host, tokensA.AccessToken));

        // 冻结 A：会话校验读的是真实租户目录，所以马上生效——userinfo 401、refresh 被拒，不依赖对账。
        await lifecycle.SuspendAsync(Guid.Parse(A), "operator", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, await UserInfoStatus(host, tokensA.AccessToken));
        Assert.Equal(HttpStatusCode.BadRequest, await RefreshStatus(host, tokensA.RefreshToken!));

        // 对账（宿主里缩到 1 秒）把失效持久化：会话行记下原因，旁路收尾后发出 session_revoked 事件。
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (await Text($"SELECT count(*) FROM plugin_qian_identity.sessions WHERE id = '{sessionA}'::uuid AND revoke_reason = 'tenant_suspended' AND revocation_processed_at IS NOT NULL") != "1")
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "对账没有在 30 秒内吊销被冻结租户的会话");
            await Task.Delay(100, Ct);
        }
        var envelope = await Text("SELECT payload FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.session_revoked.v1'");
        Assert.Contains(sessionA, envelope, StringComparison.Ordinal);
        Assert.Contains(A, envelope, StringComparison.Ordinal);
        Assert.Contains("tenant_suspended", envelope, StringComparison.Ordinal);

        // 乙方不受影响：令牌照常，refresh 照常。
        Assert.Equal(HttpStatusCode.OK, await UserInfoStatus(host, tokensB.AccessToken));
        Assert.Equal(HttpStatusCode.OK, await RefreshStatus(host, tokensB.RefreshToken!));

        // 恢复 A：已吊销的会话不会复活，用户重新登录即可。
        await lifecycle.ResumeAsync(Guid.Parse(A), "operator", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, await UserInfoStatus(host, tokensA.AccessToken));
        Assert.Equal(HttpStatusCode.BadRequest, await RefreshStatus(host, tokensA.RefreshToken!));
        var again = await UserTokens(host, phoneA);
        Assert.Equal(HttpStatusCode.OK, await UserInfoStatus(host, again.AccessToken));
    }
}
