using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing.Containers;
using static OpenIddict.Abstractions.OpenIddictConstants;
using IdentitySession = Qian.Identity.Abstractions.Session;

namespace Qian.Identity.Server.Tests;

// RFC 8707 resource：第一层由 Qian 在 OpenIddict 管道里按客户端白名单逐字比较（不做 URI 规范化，只有主机的标识也能用），
// 第二层是 OpenIddict 按 Resources 推导的 rsrc: 权限；协议端点再兜底，多资源客户端必须指定 resource，单资源客户端按登记缺省，
// 换码与 refresh 只能在原授权内收窄。资源标识都只有主机、不带结尾斜杠，正是 OpenIddict 自带第一层永远对不上的写法。
public sealed partial class ResourceIndicatorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Api = "https://api.qian.test";
    private const string Files = "https://files.qian.test";
    private const string Other = "https://other.qian.test";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 授权请求带登记内的resource_访问令牌只有该受众_单资源客户端不带时按登记缺省()
    {
        await using var host = await ResourceHost.Start(postgres);
        using (var files = await host.Exchange("multi", await host.Code("multi", Files))) Assert.Equal([Files], (await Issued(files)).Audiences);
        using (var api = await host.Exchange("multi", await host.Code("multi", Api))) Assert.Equal([Api], (await Issued(api)).Audiences);
        // 单资源客户端的行为不变：不带 resource 取唯一登记的资源，显式带上同一个结果相同。
        using (var implied = await host.Exchange("single", await host.Code("single"))) Assert.Equal([Api], (await Issued(implied)).Audiences);
        using (var named = await host.Exchange("single", await host.Code("single", Api))) Assert.Equal([Api], (await Issued(named)).Audiences);
        // client_credentials 同样按 resource 收窄。
        using (var machine = await host.ClientCredentials("machine", Files)) Assert.Equal([Files], (await Issued(machine)).Audiences);
    }

    [Fact]
    public async Task 登记外或属于其他客户端的resource被拒_多资源客户端不带resource被拒()
    {
        await using var host = await ResourceHost.Start(postgres);
        // 第一层：不在本客户端白名单内的一律 invalid_target，包括没有任何客户端登记的资源……
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Authorize("multi", "https://unregistered.qian.test")));
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.ClientCredentials("machine", "https://unregistered.qian.test")));
        // ……和登记在别的客户端名下的资源。
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Authorize("multi", Other)));
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Authorize("single", Files)));
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.ClientCredentials("machine", Other)));
        // 多资源客户端不带 resource：不再默认签发带全部受众的令牌。
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Authorize("multi")));
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.ClientCredentials("machine")));
        // 一项越界整个请求作废，不按交集签发。
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Authorize("multi", Api, Other)));
        // 令牌端点先认证客户端再查白名单：密钥不对只报 invalid_client，不能借 resource 试探别的客户端登记了什么。
        Assert.Equal(Errors.InvalidClient, await Rejected(await host.ForgedClientCredentials("machine", Other)));
        Assert.Equal(Errors.InvalidClient, await Rejected(await host.ForgedClientCredentials("machine", Files)));
    }

    [Fact]
    public async Task 换码与refresh只能在原授权内收窄_不能扩大()
    {
        await using var host = await ResourceHost.Start(postgres);
        // 授权时显式要了两个资源：换码不带 resource 沿用两者，refresh 可以收窄到其一。
        using var both = await host.Exchange("multi", await host.Code("multi", Api, Files));
        var wide = await Issued(both);
        Assert.Equal([Api, Files], wide.Audiences);
        using var narrowed = await host.Refresh("multi", wide.Refresh!, Files);
        var files = await Issued(narrowed);
        Assert.Equal([Files], files.Audiences);
        // 收窄后的集合也是新 refresh token 的上限：之后不带 resource 仍只有 Files，再要 Api 被拒。
        using var again = await host.Refresh("multi", files.Refresh!);
        var still = await Issued(again);
        Assert.Equal([Files], still.Audiences);
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Refresh("multi", still.Refresh!, Api)));
        // 换码时同样可以收窄。
        using (var exchanged = await host.Exchange("multi", await host.Code("multi", Api, Files), Api)) Assert.Equal([Api], (await Issued(exchanged)).Audiences);
        // 原授权只有 Api：换码要 Files（客户端白名单内、但未授予）被拒，refresh 也不能扩大到 Files。
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Exchange("multi", await host.Code("multi", Api), Files)));
        using var single = await host.Exchange("multi", await host.Code("multi", Api));
        var api = await Issued(single);
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Refresh("multi", api.Refresh!, Files)));
        Assert.Equal(Errors.InvalidTarget, await Rejected(await host.Refresh("multi", api.Refresh!, Api, Files)));
        // 被拒的 refresh 请求没有消耗 refresh token：同一个 token 仍能按原授权刷新。
        using var kept = await host.Refresh("multi", api.Refresh!);
        Assert.Equal([Api], (await Issued(kept)).Audiences);
    }

    [Fact]
    public async Task 第二层_OpenIddict应用缺了对应的rsrc权限时白名单内的资源也被拒()
    {
        await using var host = await ResourceHost.Start(postgres);
        var code = await host.Code("multi", Api, Files);
        // 镜像进 OpenIddict 应用的 rsrc: 权限由 Resources 推导；模拟镜像过期或被改，去掉 Files 那一项：
        // 白名单仍含 Files，第一层放行，OpenIddict 的权限检查拒绝（ID2192 报 invalid_request），授权端点与令牌端点都一样。
        await host.RemovePermission("multi", Files);
        Assert.Equal(Errors.InvalidRequest, await Rejected(await host.Authorize("multi", Files)));
        Assert.Equal(Errors.InvalidRequest, await Rejected(await host.Exchange("multi", code, Files)));
        using var api = await host.Exchange("multi", code, Api);
        Assert.Equal([Api], (await Issued(api)).Audiences);
    }

    // 成功的令牌响应：访问令牌的受众（排序）与轮换后的 refresh token。
    private static async Task<(string[] Audiences, string? Refresh)> Issued(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var json = JsonDocument.Parse(body).RootElement;
        var access = new JsonWebToken(json.GetProperty("access_token").GetString());
        return (access.Audiences.Order(StringComparer.Ordinal).ToArray(), json.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null);
    }

    // 协议错误码：授权端点随跳转回到 redirect_uri（不带 code），令牌端点是 JSON（客户端认证失败可能是 401）；回调尚未校验时授权端点直接给 400 文本。
    private static async Task<string?> Rejected(HttpResponseMessage response)
    {
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
                Assert.False(query.ContainsKey("code"), response.Headers.Location.ToString());
                return query.TryGetValue("error", out var error) ? error.ToString() : null;
            }
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized, body);
            if (response.Content.Headers.ContentType?.MediaType == "application/json") return JsonDocument.Parse(body).RootElement.GetProperty("error").GetString();
            return ErrorLine().Match(body) is { Success: true } match ? match.Groups[1].Value : body;
        }
    }

    [GeneratedRegex(@"^error:\s*(\S+)", RegexOptions.Multiline)]
    private static partial Regex ErrorLine();

    // 真实 OpenIddict 与 Data（PostgreSQL 受限应用角色），宿主自带账号服务、测试专用的已登录 Cookie；
    // 四个客户端：multi（Api + Files）、single（Api）、other（Other，只为把它的资源登记进并集）、machine（client_credentials，Api + Files）。
    private sealed class ResourceHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly PluginLoader _loader;
        private readonly X509Certificate2 _certificate;

        private ResourceHost(WebApplication app, PluginLoader loader, X509Certificate2 certificate, HttpClient browser, string issuer, string secret)
        {
            _app = app; _loader = loader; _certificate = certificate; Browser = browser; Issuer = issuer; Secret = secret;
        }

        private HttpClient Browser { get; }
        private string Issuer { get; }
        private string Secret { get; }

        // 发行方地址带端口，构建宿主前就要确定；绑定时端口已被占就换一个从头启动（同 S1-QA-014 的做法）。
        internal static async Task<ResourceHost> Start(PostgresFixture postgres)
        {
            for (var attempt = 1; ; attempt++)
            {
                using var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
                try { return await StartOn(postgres, port); }
                catch (IOException error) when (attempt < 3 && error.InnerException is AddressInUseException) { }
            }
        }

        private static async Task<ResourceHost> StartOn(PostgresFixture postgres, int port)
        {
            var issuer = $"http://127.0.0.1:{port}/";
            using var rsa = RSA.Create(2048);
            var certificate = new CertificateRequest("CN=Qian resource indicators", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseUrls(issuer); builder.Logging.ClearProviders();
            builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); builder.Services.AddKunEventBus(_ => { });
            builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(IdentityDbContext.Schema));
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAuthentication("qian.identity.signin").AddCookie("qian.identity.signin");
            builder.Services.AddQianIdentityServer(new IdentityServerOptions
            {
                Issuer = new Uri(issuer), AllowLoopbackHttp = true, SigningCertificates = [certificate], EncryptionCertificates = [certificate],
                Clients = [CodeFlow("multi", issuer, secret, Api, Files), CodeFlow("single", issuer, secret, Api), CodeFlow("other", issuer, secret, Other), Machine(secret, Api, Files)]
            }, builder.Environment);
            var app = builder.Build(); app.UseQianIdentityServer(); app.UseAuthentication(); app.MapQianIdentityServer();
            // 仅测试程序集的账号夹具：真实 Cookie 认证，协议服务中没有此路由或假登录。
            app.MapPost("/_test/authenticated-account", () => Results.SignIn(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.CreateVersion7().ToString()), new Claim("tenant_id", Guid.CreateVersion7().ToString()), new Claim("membership_id", Guid.CreateVersion7().ToString()),
                new Claim("identity_revision", "0"), new Claim("sid", Guid.CreateVersion7().ToString()), new Claim("amr", "pwd")
            }, "test-account")), authenticationScheme: "qian.identity.signin"));
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var sql = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript();
                await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct);
                await using var command = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_identity CASCADE;" + sql +
                    "GRANT USAGE ON SCHEMA plugin_qian_identity TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_identity TO qian_app;", db);
                await command.ExecuteNonQueryAsync(Ct);
            }
            var loader = new PluginLoader(app.Services);
            await loader.MountAsync([
                new(new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new DataPlugin(), [DataDefinition.Key]),
                new(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer })), new IdentityServerPlugin(new Accounts()), [IdentityDefinition.Key])], Ct);
            Assert.True(loader.GetState("identity", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("identity", ScopeKey.Global)?.ToString());
            // 启动失败（含端口被占）先释放这次建的宿主，才能换端口重来。
            try { await app.StartAsync(Ct); }
            catch { await loader.DisposeAsync(); await app.DisposeAsync(); certificate.Dispose(); throw; }
            var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(issuer) };
            using var login = await browser.PostAsync("_test/authenticated-account", null, Ct); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            return new ResourceHost(app, loader, certificate, browser, issuer, secret);
        }

        private static IdentityClientRegistration CodeFlow(string id, string issuer, string secret, params string[] resources)
        {
            var application = new OpenIddictApplicationDescriptor { ClientId = id, ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
            application.RedirectUris.Add(new Uri(issuer + "callback"));
            application.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
            return new IdentityClientRegistration { Application = application, Resources = resources };
        }

        private static IdentityClientRegistration Machine(string secret, params string[] resources)
        {
            var application = new OpenIddictApplicationDescriptor { ClientId = "machine", ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
            application.Permissions.UnionWith([Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials, Permissions.Prefixes.Scope + "qian.api"]);
            return new IdentityClientRegistration { Application = application, Resources = resources };
        }

        // 直接改 OpenIddict 的应用记录（实体缓存已关，立即生效），去掉一条 rsrc: 权限；客户端密钥的哈希原样保留。
        internal async Task RemovePermission(string client, string resource)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = (await applications.FindByClientIdAsync(client, Ct))!;
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, application, Ct);
            Assert.True(descriptor.Permissions.Remove(Permissions.Prefixes.Resource + resource));
            await applications.UpdateAsync(application, descriptor, Ct);
        }

        internal async Task<HttpResponseMessage> Authorize(string client, params string[] resources) =>
            await Authorize(client, Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48)), resources);

        internal async Task<(string Code, string Verifier)> Code(string client, params string[] resources)
        {
            var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
            using var response = await Authorize(client, verifier, resources);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(Ct));
            var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
            Assert.True(query.ContainsKey("code"), response.Headers.Location.ToString());
            return (query["code"].ToString(), verifier);
        }

        internal Task<HttpResponseMessage> Exchange(string client, (string Code, string Verifier) code, params string[] resources) =>
            Token(client, Secret, [new("grant_type", "authorization_code"), new("code", code.Code), new("redirect_uri", Issuer + "callback"), new("code_verifier", code.Verifier)], resources);

        internal Task<HttpResponseMessage> Refresh(string client, string refreshToken, params string[] resources) =>
            Token(client, Secret, [new("grant_type", "refresh_token"), new("refresh_token", refreshToken)], resources);

        internal Task<HttpResponseMessage> ClientCredentials(string client, params string[] resources) =>
            Token(client, Secret, [new("grant_type", "client_credentials"), new("scope", "qian.api")], resources);

        internal Task<HttpResponseMessage> ForgedClientCredentials(string client, params string[] resources) =>
            Token(client, "forged-" + Secret, [new("grant_type", "client_credentials"), new("scope", "qian.api")], resources);

        private Task<HttpResponseMessage> Authorize(string client, string verifier, string[] resources)
        {
            var query = new List<KeyValuePair<string, string?>>
            {
                new("client_id", client), new("redirect_uri", Issuer + "callback"), new("response_type", "code"), new("scope", "openid offline_access qian.api"),
                new("nonce", Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24))), new("code_challenge_method", "S256"),
                new("code_challenge", Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))))
            };
            query.AddRange(resources.Select(resource => new KeyValuePair<string, string?>("resource", resource)));
            return Browser.GetAsync(QueryHelpers.AddQueryString("connect/authorize", query), Ct);
        }

        // 每个 resource 是一个独立的表单字段（RFC 8707 的多值写法）。
        private Task<HttpResponseMessage> Token(string client, string secret, List<KeyValuePair<string, string>> form, string[] resources)
        {
            form.Add(new("client_id", client)); form.Add(new("client_secret", secret));
            form.AddRange(resources.Select(resource => new KeyValuePair<string, string>("resource", resource)));
            return Browser.PostAsync("connect/token", new FormUrlEncodedContent(form), Ct);
        }

        public async ValueTask DisposeAsync() { await _loader.DisposeAsync(); await _app.DisposeAsync(); Browser.Dispose(); _certificate.Dispose(); }
    }

    private sealed class Accounts : IIdentityService
    {
        public Principal? CurrentPrincipal => null;
        public IIdentityConnectorRegistry Connectors => throw new NotSupportedException();
        public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentitySession>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
