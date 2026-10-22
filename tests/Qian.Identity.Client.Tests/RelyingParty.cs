using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing;
using StackExchange.Redis;

namespace Qian.Identity.Client.Tests;

/// <summary>
/// 只装 Identity Client 的 HTTPS 依赖方宿主（会话与登录事务 Cookie 都是 Secure）：Redis 会话存储、Config 组件只用来按 secretRef 解析客户端密钥，
/// 另有两个演示端点——<c>/me</c> 读规范主体，<c>/token</c> 只报告服务端是否拿得到会话访问令牌（不回显令牌）。
/// </summary>
internal sealed class RelyingParty : IAsyncDisposable
{
    internal const string SecretReference = "secretRef://platform/identity/client-secret";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly WebApplication _app;
    private readonly string _secrets;

    private RelyingParty(WebApplication app, PluginLoader loader, Uri address, string secrets, CapturedLogs logs)
    {
        _app = app; Loader = loader; Address = address; _secrets = secrets; Logs = logs;
    }

    internal PluginLoader Loader { get; }
    internal Uri Address { get; }
    internal CapturedLogs Logs { get; }
    internal IServiceProvider Services => _app.Services;
    internal Uri Callback => new(Address, "auth/callback");

    // ReserveAddress 占住的端口（键为端口号），Start 绑定前一刻才释放；没走到 Start 的占位随测试进程退出释放。
    private static readonly ConcurrentDictionary<int, LoopbackPort.Reservation> s_reserved = new();

    /// <summary>
    /// 选一个空闲端口作为 RP 地址并占住它：发行方登记回调时要先知道它，RP 却要等发行方起来才启动，
    /// 回调已登记，绑定失败也没法换端口，所以一直占到 <see cref="Start"/> 绑定前一刻（S1-QA-016）。
    /// </summary>
    internal static Uri ReserveAddress()
    {
        var reservation = LoopbackPort.Reserve();
        s_reserved[reservation.Port] = reservation;
        return new Uri($"https://localhost:{reservation.Port}/");
    }

    private static void Release(Uri address)
    {
        if (s_reserved.TryRemove(address.Port, out var reservation)) reservation.Dispose();
    }

    internal static async Task<RelyingParty> Start(IdentityServerHost identity, string redis, Uri address, X509Certificate2 tls, TimeProvider? clock = null,
        Action<IServiceCollection>? configure = null, object? config = null)
    {
        var secrets = Directory.CreateTempSubdirectory("qian-identity-client-").FullName;
        Directory.CreateDirectory(Path.Combine(secrets, "qian", "identity"));
        await File.WriteAllTextAsync(Path.Combine(secrets, "qian", "identity", "client-secret"), identity.Secret, Ct);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(address.Port, listen => listen.UseHttps(tls)));
        var logs = new CapturedLogs(); builder.Logging.ClearProviders(); builder.Logging.SetMinimumLevel(LogLevel.Information); builder.Logging.AddProvider(logs);
        if (clock is not null) builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        builder.Services.AddSingleton<ISecretAccessObserver>(new SecretAudit());
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = secrets,
            Bindings = [new(SecretReference, new KubernetesSecretLocation("qian", "identity", "client-secret"))],
            Policies = [new("qian.identity.client", [new("identity/*", "platform")], [new("identity/*", "platform")])]
        });
        builder.Services.AddQianConfiguration(new ConfigurationRoot([]));
        builder.Services.AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = new Uri(identity.Issuer), ClientId = IdentityServerHost.ClientId, Scopes = ["openid", "offline_access", "qian.api"],
            Resource = IdentityServerHost.Resource, RedirectUri = new Uri(address, "auth/callback"), RequireHttpsMetadata = false, RequireIssuerParameter = true,
            AllowedReturnPaths = ["/me", "/app"], SessionKeyPrefix = "qian:identity-client-tests:" + Guid.NewGuid().ToString("N") + ":"
        });
        builder.Services.AddAuthorization();
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseRouting(); app.UseQianIdentityClient(); app.UseAuthentication(); app.UseAuthorization();
        app.MapQianIdentityClient();
        app.MapGet("/me", (ICurrentUser user) => user.Principal is { } principal
            ? Results.Json(new { subject = principal.Subject, kind = principal.Kind.ToString(), tenant = principal.TenantId, membership = principal.MembershipId,
                revision = principal.IdentityRevision, session = principal.SessionId, scope = principal.Scope, userId = user.UserId })
            : Results.StatusCode(StatusCodes.Status500InternalServerError)).RequireAuthorization();
        app.MapGet("/token", async (HttpContext http, IdentitySessionTokens tokens) =>
            Results.Json(new { present = await tokens.GetAccessTokenAsync(http, http.RequestAborted) is { Length: > 0 } }));
        // 网关形态的演示：把已认证的请求主体交给 Kun 网关签发器，为路径里的下游受众签发 S2S（只在测试登记了签发器时可用）。
        app.MapGet("/gateway/{audience}", async (HttpContext http, string audience) =>
            http.RequestServices.GetService<Kun.Gateway.S2sTokenIssuer>() is { } issuer
                ? Results.Text(await issuer.IssueForAudienceAsync(http.User, audience, http.RequestAborted)) : Results.NotFound()).RequireAuthorization();
        var loader = new PluginLoader(app.Services);
        try
        {
            await loader.MountAsync([
                new(new PluginEntry("config", "qian.config", "1.0.0", ScopeKey.Global, 0, System.Text.Json.JsonSerializer.SerializeToElement(new { })), new ConfigurationPlugin(), [ConfigDefinition.Key]),
                new(new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0,
                    System.Text.Json.JsonSerializer.SerializeToElement(config ?? new { issuer = identity.Issuer, clientId = IdentityServerHost.ClientId, clientSecret = SecretReference })),
                    new IdentityClientPlugin(), [IdentityDefinition.Key])], Ct);
            Release(address);
            await app.StartAsync(Ct);
        }
        catch { Release(address); await loader.DisposeAsync(); await app.DisposeAsync(); Directory.Delete(secrets, true); throw; }
        return new RelyingParty(app, loader, address, secrets, logs);
    }

    public async ValueTask DisposeAsync()
    {
        await Loader.DisposeAsync(); await _app.DisposeAsync(); Directory.Delete(_secrets, true);
    }

    /// <summary>RP 的自签名 TLS 证书；测试浏览器按指纹信任它。</summary>
    internal static X509Certificate2 CreateTlsCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        // macOS 的 SslStream 需要可导出的持久私钥，经 PKCS#12 往返一次。
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pkcs12), null);
    }

    /// <summary>
    /// 模拟浏览器：自带 Cookie 容器、不自动跟随跳转（每一跳都要断言），按指纹信任 RP 证书。RP 在 localhost、发行方在 127.0.0.1，
    /// 两边的 Cookie 因主机不同而互不串用。
    /// </summary>
    internal static (HttpClient Client, CookieContainer Cookies) Browser(X509Certificate2 tls)
    {
        var cookies = new CookieContainer();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies,
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate is not null && certificate.GetCertHashString() == tls.GetCertHashString() }
        };
        return (new HttpClient(handler), cookies);
    }

    private sealed class SecretAudit : ISecretAccessObserver
    {
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    internal sealed class CapturedLogs : ILoggerProvider
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(Messages);
        public void Dispose() { }
        private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
        }
    }
}
