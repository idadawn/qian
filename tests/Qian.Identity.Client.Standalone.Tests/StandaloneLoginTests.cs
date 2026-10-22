using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Identity.Client.Standalone.Tests;

// 验收 3：独立宿主只装 Qian.Identity.Client，对接 Qian 之外的 OIDC 发行方（Dex）完成授权码 + PKCE 登录。不部署 Qian 平台、不装 Config：
// 公开客户端没有密钥，进程内会话存储。外部发行方没有 Qian 的规范身份声明，所以 Principal 为空、只给出会话主体（授权 PEP 会一律拒绝）。
public sealed partial class StandaloneLoginTests(DexFixture dex) : IClassFixture<DexFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 被测宿主的依赖闭包里只允许出现 Client 本身及其 Runtime / SDK / Abstractions 依赖；Qian.Testing.Containers 是夹具。
    private static readonly string[] AllowedQianAssemblies =
    [
        "Qian.Identity.Client", "Qian.Identity.Abstractions", "Qian.Config.Abstractions", "Qian.Runtime", "Qian.Runtime.Errors",
        "Qian.Runtime.Errors.AspNetCore", "Qian.Testing.Containers", "Qian.Plugin.Sdk", "Qian.Data.Abstractions",
        "Qian.Authz.Abstractions", "Qian.Events.Abstractions", "Qian.Surfaces.Abstractions"
    ];

    [Fact]
    public async Task 只装Client的独立宿主对接外部OIDC发行方Dex完成登录与登出()
    {
        using var tls = CreateTlsCertificate();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(dex.RedirectUri.Port, listen => listen.UseHttps(tls)));
        builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = dex.Issuer, ClientId = DexFixture.ClientId, Scopes = ["openid", "email", "profile"], RedirectUri = dex.RedirectUri,
            RequireHttpsMetadata = false, UseInMemorySessionStore = true
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting(); app.UseQianIdentityClient(); app.UseAuthentication(); app.UseAuthorization();
        app.MapQianIdentityClient();
        app.MapGet("/me", (ICurrentUser user, HttpContext http) => Results.Json(new
        {
            userId = user.UserId, authenticated = user.IsAuthenticated, canonical = user.Principal is not null, email = http.User.FindFirst(ClaimTypes.Email)?.Value
        })).RequireAuthorization();
        await using var loader = new PluginLoader(app.Services);
        await loader.MountAsync([new(new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { issuer = dex.Issuer.ToString(), clientId = DexFixture.ClientId })), new IdentityClientComponent(), [IdentityDefinition.Key])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("identity", ScopeKey.Global));
        await app.StartAsync(Ct);
        var site = new Uri($"https://localhost:{dex.RedirectUri.Port}/");
        var cookies = new CookieContainer();
        using var browser = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies,
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == tls.GetCertHashString() }
        });

        using (var anonymous = await browser.GetAsync(new Uri(site, "me"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var login = await browser.GetAsync(new Uri(site, "auth/login?returnTo=%2Fme"), Ct);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorize = login.Headers.Location!;
        Assert.StartsWith(dex.Issuer.ToString(), authorize.ToString(), StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", authorize.Query, StringComparison.Ordinal);
        var callback = await SignInAtDex(browser, authorize);
        using (var completed = await browser.GetAsync(callback, Ct))
        {
            Assert.True(completed.StatusCode == HttpStatusCode.Redirect, await completed.Content.ReadAsStringAsync(Ct));
            Assert.Equal("/me", completed.Headers.Location!.OriginalString);
        }
        Assert.NotNull(cookies.GetCookies(site)[IdentityClientDefaults.SessionCookieName]);
        using (var me = await browser.GetAsync(new Uri(site, "me"), Ct))
        {
            var body = await me.Content.ReadAsStringAsync(Ct);
            Assert.True(me.StatusCode == HttpStatusCode.OK, body);
            var json = JsonDocument.Parse(body).RootElement;
            Assert.True(json.GetProperty("authenticated").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("userId").GetString()));
            Assert.Equal(DexFixture.Email, json.GetProperty("email").GetString());
            Assert.False(json.GetProperty("canonical").GetBoolean());
        }
        using (var csrf = await browser.GetAsync(new Uri(site, "auth/csrf"), Ct))
        {
            var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
            using var logout = new HttpRequestMessage(HttpMethod.Post, new Uri(site, "auth/logout"));
            logout.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("token").GetString());
            using var done = await browser.SendAsync(logout, Ct);
            Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        }
        using (var after = await browser.GetAsync(new Uri(site, "me"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public void 宿主的依赖闭包只有Client及其Runtime与SDK和Definition()
    {
        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Qian.Identity.Client.Standalone.Tests.deps.json")));
        var qian = deps.RootElement.GetProperty("libraries").EnumerateObject().Select(library => library.Name.Split('/')[0])
            .Where(name => name.StartsWith("Qian.", StringComparison.Ordinal) && name != "Qian.Identity.Client.Standalone.Tests").Distinct().Order().ToArray();
        Assert.Contains("Qian.Identity.Client", qian);
        Assert.Empty(UnexpectedDependencies(qian));
    }

    [Theory]
    [InlineData("Qian.Data")]
    [InlineData("Qian.Config.Apollo")]
    [InlineData("Qian.Identity.Server")]
    [InlineData("Qian.Registry")]
    [InlineData("Qian.Authz")]
    public void SDK元数据接入仍硬拒绝任何其它Provider(string provider) =>
        Assert.Equal(provider, Assert.Single(UnexpectedDependencies([.. AllowedQianAssemblies, provider])));

    private static string[] UnexpectedDependencies(IEnumerable<string> dependencies) =>
        dependencies.Where(name => !AllowedQianAssemblies.Contains(name, StringComparer.Ordinal)).ToArray();

    // 在 Dex 的本地密码库登录：跟随 Dex 内部的跳转到登录表单，提交口令，再跟随到依赖方回调为止；返回回调地址（不由测试请求它）。
    private async Task<Uri> SignInAtDex(HttpClient browser, Uri start)
    {
        var current = start;
        var submitted = false;
        for (var hop = 0; hop < 10; hop++)
        {
            using var response = await browser.GetAsync(current, Ct);
            if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.Redirect)
            {
                var next = new Uri(current, response.Headers.Location!);
                if (next.ToString().StartsWith(dex.RedirectUri.ToString(), StringComparison.Ordinal)) return next;
                current = next; continue;
            }
            var page = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK && !submitted, page);
            var action = WebUtility.HtmlDecode(FormAction().Match(page).Groups[1].Value);
            Assert.False(string.IsNullOrEmpty(action), page);
            using var form = await browser.PostAsync(new Uri(current, action), new FormUrlEncodedContent(new Dictionary<string, string> { ["login"] = DexFixture.Email, ["password"] = dex.Password }), Ct);
            Assert.True(form.StatusCode is HttpStatusCode.Found or HttpStatusCode.SeeOther, await form.Content.ReadAsStringAsync(Ct));
            submitted = true;
            current = new Uri(current, form.Headers.Location!);
            if (current.ToString().StartsWith(dex.RedirectUri.ToString(), StringComparison.Ordinal)) return current;
        }
        throw new InvalidOperationException("Dex 登录没有回到依赖方回调。");
    }

    private static X509Certificate2 CreateTlsCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); request.CertificateExtensions.Add(names.Build());
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12), null);
    }

    [GeneratedRegex("<form[^>]*action=\"([^\"]+)\"", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FormAction();
}
