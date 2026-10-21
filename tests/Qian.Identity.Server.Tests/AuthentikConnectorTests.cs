using System.Net;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// S1-IDP-007 验收 1（E2E）：以容器里的真实 Authentik 为上游，经通用 OIDC 连接器（授权码 + PKCE，验 id_token 签名 / iss / aud / nonce）登录 Qian。
// 首次登录进入确认绑定，确认后建立外部身份；再次登录直接进入；同租户内把同一外部身份再绑给别的账号被拒。
public sealed class AuthentikConnectorTests(PostgresFixture postgres, AuthentikFixture authentik) : IClassFixture<PostgresFixture>, IClassFixture<AuthentikFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 经Authentik首次登录确认绑定_再次登录直接进入_同租户重复绑定被拒()
    {
        await using var host = await AccountsHost.Start(postgres);
        var alpha = host.Tenants.Add("alpha");
        var (jia, jiaPhone) = await CreatePerson(host, "甲");
        var jiaMember = await Join(host, alpha, jia.Id, "甲成员");
        var (yi, yiPhone) = await CreatePerson(host, "乙");
        var yiMember = await Join(host, alpha, yi.Id, "乙成员");
        var (bing, bingPhone) = await CreatePerson(host, "丙");
        var bingMember = await Join(host, alpha, bing.Id, "丙成员", account: "bing");

        var callback = new Uri(host.Issuer + "connect/callback/oidc");
        var client = await authentik.CreateClientAsync(callback, Ct);
        var alice = await authentik.CreateUserAsync("Alice Ops", Ct);
        var bob = await authentik.CreateUserAsync("Bob Ops", Ct);
        // 客户端密钥只经 secretRef 交给连接器；Authentik 跑在回环地址的 http 上，所以显式关闭 https 要求。
        var secrets = new StaticSecrets("secretRef://platform/identity/connector-oidc/client-secret", client.ClientSecret);
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
        await host.MountAsync(ConnectorsPlugin.Oidc("connector.oidc",
            new { issuer = client.Issuer, clientId = client.ClientId, clientSecret = secrets.Reference, requireHttps = false, displayName = "运维 Authentik" }, secrets, handler));
        Assert.Equal(PluginState.Ready, host.Loader.GetState("connector.oidc", ScopeKey.Global));

        async Task<Guid> Through(AccountsHost.Browser browser, AuthentikFixture.User user)
        {
            var started = await browser.Begin("alpha", "oidc");
            Assert.StartsWith(authentik.BaseUri.AbsoluteUri + "application/o/authorize/", started.RedirectUri, StringComparison.Ordinal);
            var returned = await authentik.SignInAsync(new Uri(started.RedirectUri), user, callback, Ct);
            return await browser.Follow(returned, "oidc");
        }

        // 1. Alice 首次登录：进入确认绑定，展示上游资料（邮箱掩码）。
        var first = host.NewBrowser();
        var flow = await Through(first, alice);
        Assert.False(first.HasAccountCookie);
        var view = await first.View(flow);
        Assert.Equal(("bind", "bind"), (view.GetProperty("status").GetString(), view.GetProperty("next").GetString()));
        Assert.Equal(("oidc", "运维 Authentik"), (view.GetProperty("connector").GetProperty("protocol").GetString(), view.GetProperty("connector").GetProperty("display_name").GetString()));
        var external = view.GetProperty("binding").GetProperty("external");
        Assert.Equal("Alice Ops", external.GetProperty("display_name").GetString());
        Assert.NotEqual(alice.Email, external.GetProperty("email").GetString());
        Assert.DoesNotContain(alice.Email, view.GetRawText(), StringComparison.Ordinal);

        // 2. 登录已有账号并显式确认：建立外部身份（tenant × oidc × Authentik 的 sub），签发会话。
        Assert.Equal(HttpStatusCode.OK, (await first.Verify(flow, jiaPhone, Password)).Status);
        var bound = await first.Bind(flow, "link");
        Assert.Equal(HttpStatusCode.OK, bound.Status);
        Assert.Equal(jiaMember.Id, bound.Body.GetProperty("membership_id").GetGuid());
        Assert.True(first.HasAccountCookie);
        var subject = await host.Text("SELECT subject FROM plugin_qian_identity.external_identities WHERE membership_id = @m AND connector_id = 'oidc'", ("m", jiaMember.Id));
        Assert.Matches("^[0-9a-f]{64}$", subject); // Authentik 的 hashed_user_id
        Assert.Equal(jiaMember.Id.ToString("D"), await host.Bound(alpha.Id, "oidc", subject));

        // 3. Alice 再次登录：回调直接签发会话。
        var again = host.NewBrowser();
        var second = await Through(again, alice);
        Assert.True(again.HasAccountCookie);
        Assert.Equal("done", (await again.View(second)).GetProperty("next").GetString());
        Assert.Equal(jiaMember.Id, (await again.Json(HttpMethod.Get, "api/identity/me")).Body.GetProperty("membership_id").GetGuid());

        // 4. 同租户重复绑定：Bob 在两个浏览器都进入确认绑定，先绑给乙，再绑给丙被拒（409，不透露乙）。
        var three = host.NewBrowser(); var four = host.NewBrowser();
        var third = await Through(three, bob);
        var fourth = await Through(four, bob);
        Assert.Equal(HttpStatusCode.OK, (await three.Verify(third, yiPhone, Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await three.Bind(third, "link")).Status);
        Assert.Equal(HttpStatusCode.OK, (await four.Verify(fourth, bingPhone, Password)).Status);
        var conflict = await four.Bind(fourth, "link");
        Assert.Equal(HttpStatusCode.Conflict, conflict.Status);
        Assert.Equal(IdentityErrors.BindingConflict.Code, ConnectorFlow.Code(conflict.Body));
        foreach (var leak in (string[])[yiMember.Id.ToString("D"), yi.Id.ToString("D"), "乙", yiPhone])
            Assert.DoesNotContain(leak, conflict.Body.GetRawText(), StringComparison.Ordinal);
        Assert.False(four.HasAccountCookie);
        Assert.Equal("", await host.Text("SELECT coalesce(max(subject), '') FROM plugin_qian_identity.external_identities WHERE membership_id = @m", ("m", bingMember.Id)));
        var bobAgain = host.NewBrowser();
        await Through(bobAgain, bob);
        Assert.Equal(yiMember.Id, (await bobAgain.Json(HttpMethod.Get, "api/identity/me")).Body.GetProperty("membership_id").GetGuid());
    }
}
