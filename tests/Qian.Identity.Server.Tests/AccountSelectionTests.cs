using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// S1-YUE-002：外壳切换租户走标准 prompt=select_account——账号 Cookie 仍有效时授权端点 302 到同源选择页，页面换会话后回到去掉 prompt 的授权地址签新码；
// id_token 带标准 name claim（成员显示名 / 平台身份的自然人显示名），访问令牌不带。其余 prompt 值仍按 interaction_required 拒绝。
public sealed class AccountSelectionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Id(Guid id) => id.ToString("D");

    [Fact]
    public async Task select_account_无Cookie走登录挑战且ReturnUrl去掉prompt_有Cookie跳同源选择页_切换后回授权地址签新码_id_token带name()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, b, person, phone, inA, inB) = world;
        var browser = host.NewBrowser();
        var (verifier, nonce, authorize) = Authorize(host, "select_account");
        // 没有账号 Cookie：仍是登录挑战；带回去的 ReturnUrl 已去掉 prompt，登录（含其中的租户选择）完成后不会再被要求选一次。
        using (var challenge = await browser.Http.GetAsync(authorize, Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
            var location = challenge.Headers.Location!;
            Assert.Equal("/Account/Login", location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0]);
            var returnUrl = QueryHelpers.ParseQuery(location.IsAbsoluteUri ? location.Query : "?" + location.OriginalString.Split('?', 2)[1])["ReturnUrl"].ToString();
            Assert.StartsWith("/connect/authorize?", returnUrl, StringComparison.Ordinal);
            Assert.DoesNotContain("prompt", returnUrl, StringComparison.Ordinal);
            Assert.Contains("code_challenge=", returnUrl, StringComparison.Ordinal);
        }
        var login = await browser.Login(phone, Password);
        Assert.Equal("selection_required", login.Body.GetProperty("status").GetString());
        var selected = await browser.Select(login.Body.GetProperty("ticket").GetString()!, inA.Id);
        Assert.Equal("authenticated", selected.Body.GetProperty("status").GetString());
        // 有效 Cookie + select_account：302 到同源选择页，return_url 是去掉 prompt 的授权请求原地址（同源相对路径，不是开放重定向）。
        string resume;
        using (var redirect = await browser.Http.GetAsync(authorize, Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
            var location = redirect.Headers.Location!.OriginalString;
            Assert.StartsWith("/login/select-tenant?return_url=", location, StringComparison.Ordinal);
            resume = QueryHelpers.ParseQuery(location["/login/select-tenant".Length..])["return_url"].ToString();
            Assert.StartsWith("/connect/authorize?", resume, StringComparison.Ordinal);
            Assert.DoesNotContain("prompt", resume, StringComparison.Ordinal);
            var query = QueryHelpers.ParseQuery(resume["/connect/authorize".Length..]);
            Assert.Equal("web", query["client_id"]); Assert.Equal(nonce, query["nonce"]); Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal("openid offline_access qian.api", query["scope"]);
        }
        // 选择页的两步：切换 → 选 B（IDP-003 既有语义：新会话、旧会话吊销 reason=switch）。
        var begin = await browser.Json(HttpMethod.Post, "api/identity/session/switch", new { });
        Assert.Equal("selection_required", begin.Body.GetProperty("status").GetString());
        var switched = await browser.Select(begin.Body.GetProperty("ticket").GetString()!, inB.Id);
        Assert.Equal((b.Id, inB.Id), (switched.Body.GetProperty("tenant_id").GetGuid(), switched.Body.GetProperty("membership_id").GetGuid()));
        // 回到 return_url：没有 prompt，直接签发授权码；换码后 id_token 带 name（乙方成员），访问令牌没有 name。
        string code;
        using (var issued = await browser.Http.GetAsync(resume, Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, issued.StatusCode);
            Assert.StartsWith(host.Redirect, issued.Headers.Location!.ToString(), StringComparison.Ordinal);
            code = QueryHelpers.ParseQuery(issued.Headers.Location!.Query)["code"].ToString();
        }
        using var kun = host.Kun("web");
        var tokens = await kun.ExchangeCodeAsync(new(code, host.Redirect, verifier, nonce), Ct);
        Assert.Equal("乙方成员", tokens.ValidatedPrincipal!.FindFirst("name")!.Value);
        Assert.Equal(Id(b.Id), tokens.ValidatedPrincipal.FindFirst("tenant_id")!.Value);
        var access = new JsonWebToken(tokens.AccessToken);
        Assert.False(access.TryGetClaim("name", out _));
        Assert.Equal(Id(inB.Id), access.GetClaim("membership_id").Value);
        // 刷新：新 id_token 仍带 name，访问令牌仍不带。
        var refreshed = await kun.RefreshAsync(tokens.RefreshToken!, Id(person.Id), Ct);
        Assert.Equal("乙方成员", refreshed.ValidatedPrincipal!.FindFirst("name")!.Value);
        Assert.False(new JsonWebToken(refreshed.AccessToken).TryGetClaim("name", out _));
        // 其他交互要求仍不能静默复用 Cookie；none 不得与 select_account 并用（OpenIddict 在协议校验阶段就以 invalid_request 拒绝）。
        using (var login2 = await browser.Http.GetAsync(Authorize(host, "login").Url, Ct)) await AssertProtocolError(login2, "interaction_required");
        using (var mixed = await browser.Http.GetAsync(Authorize(host, "none select_account").Url, Ct)) await AssertProtocolError(mixed, "invalid_request");
    }

    [Fact]
    public async Task 平台身份的id_token带自然人显示名_用户信息端点不带name()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (operatorPerson, phone) = await CreatePerson(host, "运营", platform: true);
        var browser = host.NewBrowser();
        var login = await browser.Login(phone, Password);
        Assert.Equal("authenticated", login.Body.GetProperty("status").GetString());
        var (verifier, nonce, authorize) = Authorize(host, null);
        string code;
        using (var issued = await browser.Http.GetAsync(authorize, Ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, issued.StatusCode);
            code = QueryHelpers.ParseQuery(issued.Headers.Location!.Query)["code"].ToString();
        }
        using var kun = host.Kun("web");
        var tokens = await kun.ExchangeCodeAsync(new(code, host.Redirect, verifier, nonce), Ct);
        Assert.Equal("运营", tokens.ValidatedPrincipal!.FindFirst("name")!.Value);
        Assert.Equal(Id(operatorPerson.Id), tokens.ValidatedPrincipal.FindFirst("sub")!.Value);
        Assert.False(new JsonWebToken(tokens.AccessToken).TryGetClaim("name", out _));
        using var request = new HttpRequestMessage(HttpMethod.Get, "connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await browser.Http.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.False(info.RootElement.TryGetProperty("name", out _));
    }

    [Theory]
    [InlineData("/login/select-tenant", true)]
    [InlineData("/", true)]
    [InlineData("//evil.example/select", false)]
    [InlineData("https://evil.example/select", false)]
    [InlineData("/login/select?x=1", false)]
    [InlineData("", false)]
    public void 选择页路径必须是同源相对路径(string path, bool accepted)
    {
        using var rsa = RSA.Create(2048);
        using var cert = new CertificateRequest("CN=selection-path", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var options = new IdentityServerOptions
        {
            Issuer = new Uri("https://identity.example/"), SigningCertificates = [cert], EncryptionCertificates = [cert],
            Accounts = new IdentityAccountOptions { SelectionPath = path },
            Clients = [new() { Application = new OpenIddictApplicationDescriptor
                { ClientId = "test", ClientType = OpenIddictConstants.ClientTypes.Confidential, ClientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), ConsentType = OpenIddictConstants.ConsentTypes.Implicit }, Resources = ["https://api.example"] }]
        };
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        if (accepted) builder.Services.AddQianIdentityServer(options, builder.Environment);
        else Assert.Throws<ArgumentException>(() => builder.Services.AddQianIdentityServer(options, builder.Environment));
    }

    // 浏览器直接发出的授权请求（web 客户端、PKCE S256、openid 带 nonce），可附 prompt。
    private static (string Verifier, string Nonce, string Url) Authorize(AccountsHost.Running host, string? prompt)
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48)); var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = "web", ["redirect_uri"] = host.Redirect, ["response_type"] = "code", ["scope"] = "openid offline_access qian.api", ["nonce"] = nonce,
            ["code_challenge_method"] = "S256", ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        };
        if (prompt is not null) query["prompt"] = prompt;
        return (verifier, nonce, QueryHelpers.AddQueryString("connect/authorize", query));
    }

    // OpenIddict 把协议错误以 302 带回 redirect_uri（error=…）；请求本身不合法（如 prompt 组合）时直接 400 并在响应里给出错误码。
    private static async Task AssertProtocolError(HttpResponseMessage response, string error)
    {
        if (response.StatusCode == HttpStatusCode.Redirect) { Assert.Equal(error, QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].ToString()); return; }
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
