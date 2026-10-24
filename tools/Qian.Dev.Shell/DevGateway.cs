using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.Gateway;
using Kun.Identity.Abstractions.Session;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Dev.TenantOps;
using Qian.Gateway;
using Qian.Identity.Abstractions;
using Qian.Identity.Client;
using Qian.Runtime;
using Qian.Sample.Crm;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Qian.Dev.Shell;

/// <summary>网关 BFF 宿主与它的插件装载器；停机时一起释放。</summary>
internal sealed class DevGatewayHost(WebApplication app, PluginLoader loader) : IAsyncDisposable
{
    internal WebApplication App => app;
    public async ValueTask DisposeAsync() { await loader.DisposeAsync().ConfigureAwait(false); await app.DisposeAsync().ConfigureAwait(false); }
}

// 网关 BFF 宿主，照安全验收夹具 tests/Shared/GatewayTestHost：同一份 Gateway / Identity.Client / Config 生产入口。
// 浏览器只持有 qian.sid（HttpOnly、Secure）；/api 经会话换 60 秒 S2S 转发到业务宿主，令牌只在服务端。外壳静态托管（入口、带哈希资源、/app-config.json、
// 参考应用的远程制品）与 bootstrap 聚合端点都是产品扩展（S1-YUE-012），与生产网关同一份代码。
// 额外只有三件开发期的事：/api-alt 第二个 API 前缀（验收 4 改 apiBase 后仍可用）、/__dev/session/* 把当前会话的访问令牌改成已到期（验收 1 的刷新与过期对话框）、
// /__dev/identity/* 递增身份修订或只抬平台表面的修订下限（验收 1 的会话刷新后重拉与「刷新成功但平台表面持续 401」）。
internal static class DevGateway
{
    internal const string Issuer = "https://gateway.qian.local/s2s";
    internal const string Resource = "https://api.qian.local";
    internal const string Cluster = "qian.sample.crm";
    private const string SecretReference = "secretRef://platform/identity/client-secret";
    /// <summary>运行时配置的缺省内容（K8s ConfigMap 的本机替身）：四个地址取外壳缺省值，只给品牌。</summary>
    internal const string DefaultAppConfig = """{ "brand": "Qian 乾" }""" + "\n";

    internal static async Task<DevGatewayHost> StartAsync(Uri address, DevGatewayKeys keys, Uri identityIssuer, string businessAddress, string platformAddress, ShellStaticOptions shell,
        Func<Principal, CancellationToken, Task<long>> bump, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            if (address.Host == "localhost") kestrel.ListenLocalhost(address.Port, listen => listen.UseHttps(keys.Tls));
            else kestrel.Listen(IPAddress.Parse(address.Host), address.Port, listen => listen.UseHttps(keys.Tls));
        });
        DevHost.Logging(builder.Logging);
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<ISecretAccessObserver>(new SecretObserver());
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = keys.SecretRoot,
            Bindings = [new(SecretReference, new KubernetesSecretLocation("qian", "identity", "client-secret"))],
            Policies = [new("qian.identity.client", [new("identity/*", "platform")], [new("identity/*", "platform")])]
        });
        builder.Services.AddQianConfiguration(new ConfigurationRoot([]));
        // 单进程开发宿主用进程内会话存储（生产是 Redis）；其余与生产相同：规范模式（带 resource）、refresh 单飞、Secure Cookie。
        builder.Services.AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = identityIssuer, ClientId = DevIdentity.ClientId, Resource = Resource, RedirectUri = new Uri(address, "auth/callback"),
            Scopes = ["openid", "offline_access", "qian.api", .. DevIdentity.ApplicationScopes], RequireHttpsMetadata = false, UseInMemorySessionStore = true, DefaultReturnPath = "/"
        });
        // 受众分簇：面向用户的平台表面（能力目录 / 导航 / 外壳目录、有效权限）走平台表面宿主，其余 /api 走参考应用的命令宿主；/api-alt 是同一组路由的第二个前缀。
        var configuration = new GatewayConfiguration(identityIssuer.AbsoluteUri, Resource, Issuer, keys.SigningKeyPath, keys.PublicKeyPath, [],
            [.. Routes("api"), .. Routes("api-alt")], [new(Cluster, businessAddress), new(PlatformSurfaceHost.Audience, platformAddress)]);
        builder.Services.AddQianGateway(configuration, IdentityClientDefaults.SessionScheme);
        builder.Services.AddSingleton<ITransformProvider, AlternatePrefixTransform>();
        var app = builder.Build();
        app.UseForwardedHeaders(); app.UseQianShell(shell); app.UseRouting(); app.UseQianIdentityClient(); app.UseQianGateway();
        app.MapQianIdentityClient(); app.MapQianGateway();
        // bootstrap 聚合端点：规范路径 /api/shell/bootstrap，外壳把它随 apiBase 映射，所以 /api-alt 前缀下也挂一份（下游仍按网关路由表的 /api 路径选簇）。
        app.MapQianShellBootstrap(); app.MapQianShellBootstrap("/api-alt/shell/bootstrap");
        // 开发路由（不是产品契约）：只对已认证的本会话生效，且仍要带防伪令牌。expire 让服务端访问令牌立刻到期（下次请求透明刷新，页面无感）；
        // break 同时毁掉 refresh token（刷新必败 → 会话结束 → 并发 401 被 SDK 合并成一次刷新尝试，会话已不在、取防伪令牌即 401 不再 POST → 过期对话框）；
        // identity/bump 经真实身份端口递增当前成员的 identity_revision，再把平台表面的修订下限抬到新修订（同进程宿主里授权变更后 IdentityRevisionPropagation 就是这样做的，S1-DEC-027）：
        // 下一批请求带着旧修订 401 → SDK 合并成一次 POST /auth/session/refresh → 依赖方换到带新修订的令牌 → 重放 200 → 外壳重拉 bootstrap。
        // identity/stale 只把下限抬到当前令牌修订 + 1、不递增身份修订：依赖方刷新仍成功，换到的令牌修订不变，平台表面持续 401 →
        // SDK 只刷新一次、重放仍 401 → 只弹一个过期对话框，外壳不再发起新一轮（会话结束对外壳是粘性的）。
        // 抬下限走平台表面宿主的真实 PEP：网关用自己的 S2S 签发器为当前主体签一枚修订改过的令牌，调一次 GET /api/authz/me/permissions，
        // PEP 的 IdentityRevisionGuard 见到已验证的更高修订即把下限抬上去（懒抬高，与生产同一代码路径；没有开发期的下限表）。
        app.MapPost("/__dev/session/expire", http => MutateSession(http, breakRefresh: false)).RequireAuthorization();
        app.MapPost("/__dev/session/break", http => MutateSession(http, breakRefresh: true)).RequireAuthorization();
        app.MapPost("/__dev/identity/bump", http => BumpRevision(http, platformAddress, bump)).RequireAuthorization();
        app.MapPost("/__dev/identity/stale", http => StaleRevision(http, platformAddress)).RequireAuthorization();
        app.MapQianShell(shell);
        var loader = new PluginLoader(app.Services);
        try
        {
            await loader.MountAsync([
                Definition("config", "qian.config", new ConfigurationPlugin(), JsonSerializer.SerializeToElement(new { }), ConfigDefinition.Key),
                Definition("identity", "qian.identity.client", new IdentityClientComponent(),
                    JsonSerializer.SerializeToElement(new { issuer = identityIssuer.AbsoluteUri, clientId = DevIdentity.ClientId, clientSecret = SecretReference }), IdentityDefinition.Key),
                Definition("gateway", "qian.gateway", new GatewayComponent(), JsonSerializer.SerializeToElement(configuration, JsonSerializerOptions.Web), GatewayDefinition.Key)], ct).ConfigureAwait(false);
            foreach (var id in new[] { "config", "identity", "gateway" })
                if (loader.GetState(id, ScopeKey.Global) != PluginState.Ready) throw new InvalidOperationException("网关组件未就绪：" + id, loader.GetFailure(id, ScopeKey.Global));
            await app.StartAsync(ct).ConfigureAwait(false);
            return new DevGatewayHost(app, loader);
        }
        catch { await loader.DisposeAsync().ConfigureAwait(false); await app.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private static GatewayRoute[] Routes(string prefix) =>
    [
        new(prefix + "-capabilities", "/" + prefix + "/capabilities/{**rest}", PlatformSurfaceHost.Audience),
        new(prefix + "-authz", "/" + prefix + "/authz/{**rest}", PlatformSurfaceHost.Audience),
        new(prefix, "/" + prefix + "/{**rest}", Cluster)
    ];

    private static async Task MutateSession(HttpContext http, bool breakRefresh)
    {
        var id = http.Request.Cookies[IdentityClientDefaults.SessionCookieName];
        var store = http.RequestServices.GetRequiredService<ISessionStore>();
        var data = string.IsNullOrEmpty(id) ? null : await store.GetAsync(id, http.RequestAborted).ConfigureAwait(false);
        if (id is null || data is null) { http.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        var expired = data with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1), RefreshToken = breakRefresh ? "qian-dev-broken-refresh-token" : data.RefreshToken };
        await store.SetAsync(id, expired, http.RequestServices.GetRequiredService<IdentityClientOptions>().SessionLifetime, http.RequestAborted).ConfigureAwait(false);
        http.Response.Headers.CacheControl = "no-store"; http.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    // 只对租户成员身份有意义（平台身份没有成员修订，409）；返回新修订便于核对。
    private static async Task BumpRevision(HttpContext http, string platformAddress, Func<Principal, CancellationToken, Task<long>> bump)
    {
        if (http.RequestServices.GetRequiredService<ICurrentUser>().Principal is not { Kind: PrincipalKind.User, TenantId: not null } principal || !Guid.TryParseExact(principal.Subject, "D", out _))
        { http.Response.StatusCode = StatusCodes.Status409Conflict; return; }
        var revision = await bump(principal, http.RequestAborted).ConfigureAwait(false);
        if (!await RaiseFloor(http, platformAddress, revision).ConfigureAwait(false)) { http.Response.StatusCode = StatusCodes.Status502BadGateway; return; }
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsJsonAsync(new { identityRevision = revision }, http.RequestAborted).ConfigureAwait(false);
    }

    // 同样只对租户成员身份有意义（409 同上）；返回当前修订与抬高后的下限便于核对。
    private static async Task StaleRevision(HttpContext http, string platformAddress)
    {
        if (http.RequestServices.GetRequiredService<ICurrentUser>().Principal is not { Kind: PrincipalKind.User, TenantId: not null, IdentityRevision: { } revision } principal || !Guid.TryParseExact(principal.Subject, "D", out _))
        { http.Response.StatusCode = StatusCodes.Status409Conflict; return; }
        if (!await RaiseFloor(http, platformAddress, revision + 1).ConfigureAwait(false)) { http.Response.StatusCode = StatusCodes.Status502BadGateway; return; }
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsJsonAsync(new { identityRevision = revision, floor = revision + 1 }, http.RequestAborted).ConfigureAwait(false);
    }

    // 当前会话主体的副本只换 identity_revision（同一 claim 类型与值类型），由网关的 S2S 签发器按平台表面受众签发；PEP 放行即下限已 ≥ revision。
    // 签发器只给「当前已认证请求的主体」签（GatewayPrincipalResolver 按引用比对 http.User），所以签发期间把本请求的 User 换成副本、签完即还原：
    // 仍是同一会话、同一主体与租户，只在这条已认证 + 防伪的开发路由里发生。
    private static async Task<bool> RaiseFloor(HttpContext http, string platformAddress, long revision)
    {
        var principal = http.User.Clone();
        var identity = principal.Identities.FirstOrDefault(item => item.HasClaim(claim => claim.Type == IdentityClaimNames.IdentityRevision));
        if (identity is null) return false;
        foreach (var claim in identity.FindAll(IdentityClaimNames.IdentityRevision).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(IdentityClaimNames.IdentityRevision, revision.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        var original = http.User; string token;
        http.User = principal;
        try { token = await http.RequestServices.GetRequiredService<S2sTokenIssuer>().IssueForAudienceAsync(principal, PlatformSurfaceHost.Audience, http.RequestAborted).ConfigureAwait(false); }
        finally { http.User = original; }
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(platformAddress), "/api/authz/me/permissions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, http.RequestAborted).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private static PluginDefinition Definition(string id, string plugin, IQianPlugin implementation, JsonElement config, params ServiceKey[] provides) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, config), implementation, provides);

    // 第二个 API 前缀：/api-alt/... 改写为 /api/... 再转发，其余（受众、S2S、去 Cookie）与 /api 路由同一套变换。
    private sealed class AlternatePrefixTransform : ITransformProvider
    {
        public void ValidateRoute(TransformRouteValidationContext context) { }
        public void ValidateCluster(TransformClusterValidationContext context) { }
        public void Apply(TransformBuilderContext context)
        {
            if (!context.Route.RouteId.StartsWith("api-alt", StringComparison.Ordinal)) return;
            context.AddPathRemovePrefix("/api-alt"); context.AddPathPrefix("/api");
        }
    }

    private sealed class SecretObserver : ISecretAccessObserver
    {
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

// 网关宿主每次启动生成的材料：S2S 签名钥（私钥 PEM 落状态目录，业务宿主只拿公钥 JWKS）、外壳客户端密钥（按生产形状投射到 Secret 挂载目录，
// Identity.Client 只经 secretRef 读取）、TLS 自签名证书（PEM 落状态目录供 curl --cacert）。目录仅本人可读，全部在 git 忽略的 .qian-dev/ 下。
internal sealed class DevGatewayKeys : IDisposable
{
    private DevGatewayKeys(string directory, string signingKeyPath, string publicKeyPath, string publicJwks, RsaSecurityKey publicKey, string secretRoot, string clientSecret, X509Certificate2 tls, string tlsPath)
    { Directory = directory; SigningKeyPath = signingKeyPath; PublicKeyPath = publicKeyPath; PublicJwks = publicJwks; PublicKey = publicKey; SecretRoot = secretRoot; ClientSecret = clientSecret; Tls = tls; TlsCertificatePath = tlsPath; }

    internal string Directory { get; }
    internal string SigningKeyPath { get; }
    internal string PublicKeyPath { get; }
    internal string PublicJwks { get; }
    /// <summary>只含公钥的校验钥；平台宿主按它验网关 S2S。</summary>
    internal RsaSecurityKey PublicKey { get; }
    internal string SecretRoot { get; }
    internal string ClientSecret { get; }
    internal X509Certificate2 Tls { get; }
    internal string TlsCertificatePath { get; }

    internal static DevGatewayKeys Create(string state)
    {
        var directory = Path.Combine(state, "gateway");
        if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        System.IO.Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var signingKeyPath = Path.Combine(directory, "private.pem"); var publicKeyPath = Path.Combine(directory, "public.pem");
        string jwks; RsaSecurityKey publicKey;
        using (var rsa = RSA.Create(2048))
        {
            WritePrivate(signingKeyPath, rsa.ExportRSAPrivateKeyPem());
            var publicOnly = RSA.Create(); publicOnly.ImportParameters(rsa.ExportParameters(false));
            publicKey = new RsaSecurityKey(publicOnly);
            publicKey.KeyId = Base64UrlEncoder.Encode(JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey).ComputeJwkThumbprint());
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
            jwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } });
        }
        var secretRoot = Path.Combine(directory, "secrets");
        System.IO.Directory.CreateDirectory(Path.Combine(secretRoot, "qian", "identity"));
        var clientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        WritePrivate(Path.Combine(secretRoot, "qian", "identity", "client-secret"), clientSecret);
        var tls = Certificate(); var tlsPath = Path.Combine(directory, "tls.crt");
        File.WriteAllText(tlsPath, tls.ExportCertificatePem() + "\n");
        return new DevGatewayKeys(directory, signingKeyPath, publicKeyPath, jwks, publicKey, secretRoot, clientSecret, tls, tlsPath);
    }

    private static void WritePrivate(string path, string contents)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var writer = new StreamWriter(new FileStream(path, options)); writer.Write(contents);
    }

    // 外壳地址的自签名证书（localhost、127.0.0.1、::1）；macOS 的 SslStream 需要可导出的持久私钥，经 PKCS#12 往返一次。
    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback); names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
    }

    public void Dispose() { Tls.Dispose(); PublicKey.Rsa.Dispose(); }
}
