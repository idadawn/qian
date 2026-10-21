using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kun.Identity.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Events;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server.Tests;

// 会话中心测试共用的辅助：登录并换令牌、走完登出、轮询旁路结果、读取已发布的事件。
internal static partial class SessionSupport
{
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static readonly LoginContext Net = new("203.0.113.9", "UT-Agent/1.0");
    internal static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    internal static string Id(Guid id) => id.ToString("D");

    internal static string Code(System.Text.Json.JsonElement problem) => problem.GetProperty("code").GetString()!;

    /// <summary>停掉宿主自带的后台处理器，由测试亲手驱动吊销旁路与租户对账（需要确定性或要扮演另一个副本时用）。</summary>
    internal static async Task StopProcessor(AccountsHost.Running host)
    {
        foreach (var processor in host.Services.GetServices<IHostedService>().OfType<IdentitySessionProcessor>()) await processor.StopAsync(Ct);
    }

    /// <summary>旁路是异步的：轮询到条件成立，最多等 <paramref name="timeout"/>。</summary>
    internal static async Task Eventually(Func<Task<bool>> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!await condition().ConfigureAwait(false))
        {
            if (DateTimeOffset.UtcNow > deadline) Assert.Fail("等待超时：" + because);
            await Task.Delay(50, Ct);
        }
    }

    /// <summary>Outbox 里指定类型的全部事件信封，按发生顺序。</summary>
    internal static async Task<IReadOnlyList<QianEventEnvelope>> EventsOf(AccountsHost.Running host, string type)
    {
        var joined = await host.Text("SELECT coalesce(string_agg(payload, E'\\n' ORDER BY occurred_at, id), '') FROM plugin_qian_identity.outbox_messages WHERE event_type_id = @t", ("t", type));
        return joined.Length == 0 ? [] : [.. joined.Split('\n').Select(QianEventEnvelope.Parse)];
    }

    /// <summary>已发布的会话吊销事件里指定会话的那条；还没发布时为 null。</summary>
    internal static async Task<QianEventEnvelope?> Revoked(AccountsHost.Running host, Guid session) =>
        (await EventsOf(host, "identity.session_revoked.v1")).SingleOrDefault(e => e.Payload.GetProperty("session_id").GetString() == Id(session));

    internal static string Redirect(AccountsHost.Running host, string client) => client == "web" ? host.Redirect : host.Issuer + "mobile-callback";

    /// <summary>登录并走完授权码流程；返回会话标识与令牌。单一成员身份时登录直接成功，不需要选择。</summary>
    internal static async Task<(Guid Session, OidcTokenResponse Tokens)> SignIn(AccountsHost.Running host, AccountsHost.Browser browser, string phone, Guid? tenant, string client = "web")
    {
        var login = await browser.Login(phone, AccountSupport.Password, client, tenant);
        Assert.Equal("authenticated", login.Body.GetProperty("status").GetString());
        var code = await browser.Code(client);
        Assert.NotNull(code);
        using var kun = host.Kun(client);
        var tokens = await kun.ExchangeCodeAsync(new(code.Value.Code, Redirect(host, client), code.Value.Verifier, code.Value.Nonce), Ct);
        return (Guid.ParseExact(new JsonWebToken(tokens.AccessToken).GetClaim("sid").Value, "D"), tokens);
    }

    /// <summary>走完 OIDC 登出：先取确认页拿到防伪字段，再提交；返回提交的状态码（成功是 302 跳回登出后地址）。</summary>
    internal static async Task<HttpStatusCode> Logout(AccountsHost.Running host, AccountsHost.Browser browser, string idToken)
    {
        var form = new Dictionary<string, string> { ["id_token_hint"] = idToken, ["post_logout_redirect_uri"] = host.Issuer + "signed-out" };
        using (var confirmation = await browser.Http.GetAsync(QueryHelpers.AddQueryString("connect/logout", form.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))), Ct))
        {
            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
            foreach (Match field in HiddenInputs().Matches(await confirmation.Content.ReadAsStringAsync(Ct))) form[WebUtility.HtmlDecode(field.Groups[1].Value)] = WebUtility.HtmlDecode(field.Groups[2].Value);
        }
        using var accepted = await browser.Http.PostAsync("connect/logout", new FormUrlEncodedContent(form), Ct);
        return accepted.StatusCode;
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\">", RegexOptions.CultureInvariant)]
    private static partial Regex HiddenInputs();

    /// <summary>用 refresh token 换新令牌，返回状态码（被拒是 400）。</summary>
    internal static async Task<HttpStatusCode> Refresh(AccountsHost.Browser browser, string client, string refreshToken)
    {
        using var response = await browser.Grant(client, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken });
        return response.StatusCode;
    }

    internal static long RevisionOf(string accessToken) => long.Parse(new JsonWebToken(accessToken).GetClaim("identity_revision").Value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>某 Person 已吊销的会话，按建立顺序（时间戳相同时顺序不确定，调用方需要时再排序）。</summary>
    internal static async Task<Guid[]> RevokedSessions(AccountsHost.Running host, Guid person)
    {
        var text = await host.Text("SELECT coalesce(string_agg(id::text, ',' ORDER BY created_at), '') FROM plugin_qian_identity.sessions WHERE person_id = @p::uuid AND revoked_at IS NOT NULL", ("p", Id(person)));
        return text.Length == 0 ? [] : [.. text.Split(',').Select(Guid.Parse)];
    }

    /// <summary>登录审计里记的 sid 哈希：SHA-256 的前 16 个十六进制字符，不是明文。</summary>
    internal static string AuditHash(Guid session) => Convert.ToHexStringLower(SHA256.HashData(session.ToByteArray()))[..16];
}
