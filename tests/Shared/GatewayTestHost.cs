using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Gateway;
using Qian.Identity.Abstractions;
using Qian.Identity.Client;
using Qian.Runtime;
using StackExchange.Redis;

namespace Qian.Testing.Gateway;

// 安全验收用真实 Kestrel 组合：同一份 Gateway / Identity.Client / Config 生产入口，不冒充完整 Apollo/Audit/Data AOT 验收。
internal sealed class GatewayTestHost(WebApplication app, PluginLoader loader, GatewayConfiguration configuration, Uri address, string directory) : IAsyncDisposable
{
    internal GatewayConfiguration Configuration => configuration;
    internal PluginLoader Loader => loader;
    internal Uri Address => address;
    internal Uri HttpAddress => new(app.Urls.Single(url => url.StartsWith("http://", StringComparison.Ordinal)));
    internal IServiceProvider Services => app.Services;
    internal string SigningPath => configuration.SigningKeyPath;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static async Task<GatewayTestHost> Start(LoopbackPort.Reservation reservation, X509Certificate2 tls,
        string identityIssuer, string clientId, string identityAudience, string clientSecret, string redis,
        string issuer, IReadOnlyList<GatewayRoute> routes, IReadOnlyList<GatewayCluster> clusters, RSA signing,
        IReadOnlyList<string>? trustedNetworks = null, TimeProvider? clock = null)
    {
        var address = new Uri($"https://localhost:{reservation.Port}/");
        var directory = Directory.CreateTempSubdirectory("qian-gateway-").FullName;
        const string reference = "secretRef://platform/identity/client-secret";
        Directory.CreateDirectory(Path.Combine(directory, "qian", "identity"));
        var privatePath = Path.Combine(directory, "private.pem");
        await WritePrivate(privatePath, signing.ExportRSAPrivateKeyPem());
        await WritePrivate(Path.Combine(directory, "qian", "identity", "client-secret"), clientSecret);
        var configuration = new GatewayConfiguration(identityIssuer, identityAudience, issuer,
            privatePath, Path.Combine(directory, "public.pem"), trustedNetworks ?? [], routes, clusters);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.ListenLocalhost(address.Port, listen => listen.UseHttps(tls));
            server.Listen(IPAddress.Loopback, 0);
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        if (clock is not null) builder.Services.AddSingleton(clock);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
        builder.Services.AddSingleton<ISecretAccessObserver>(new SecretObserver());
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = directory,
            Bindings = [new(reference, new KubernetesSecretLocation("qian", "identity", "client-secret"))],
            Policies = [new("qian.identity.client", [new("identity/*", "platform")], [new("identity/*", "platform")])]
        });
        builder.Services.AddQianConfiguration(new ConfigurationRoot([]));
        builder.Services.AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = new Uri(identityIssuer), ClientId = clientId, Resource = identityAudience,
            RedirectUri = new Uri(address, "auth/callback"), Scopes = ["openid", "offline_access", "qian.api"],
            RequireHttpsMetadata = false, AllowedReturnPaths = ["/app"], DefaultReturnPath = "/app",
            SessionKeyPrefix = "qian:gateway-tests:" + Guid.NewGuid().ToString("N") + ":"
        });
        builder.Services.AddQianGateway(configuration, IdentityClientDefaults.SessionScheme);
        var app = builder.Build();
        app.UseForwardedHeaders(); app.UseRouting(); app.UseQianIdentityClient(); app.UseQianGateway();
        app.MapQianIdentityClient(); app.MapQianGateway();
        var loader = new PluginLoader(app.Services);
        try
        {
            await loader.MountAsync([
                Definition("config", "qian.config", new ConfigurationPlugin(), JsonSerializer.SerializeToElement(new { }), ConfigDefinition.Key),
                Definition("identity", "qian.identity.client", new IdentityClientComponent(),
                    JsonSerializer.SerializeToElement(new { issuer = identityIssuer, clientId, clientSecret = reference }), IdentityDefinition.Key),
                Definition("gateway", "qian.gateway", new GatewayComponent(),
                    JsonSerializer.SerializeToElement(configuration, JsonSerializerOptions.Web), GatewayDefinition.Key)
            ], Ct);
            Assert.True(loader.GetState("gateway", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("gateway", ScopeKey.Global)?.ToString());
            reservation.Dispose();
            await app.StartAsync(Ct);
            return new(app, loader, configuration, address, directory);
        }
        catch { reservation.Dispose(); await loader.DisposeAsync(); await app.DisposeAsync(); Directory.Delete(directory, true); throw; }
    }

    internal static async Task WritePrivate(string path, string contents)
    {
        var options = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var writer = new StreamWriter(new FileStream(path, options));
        await writer.WriteAsync(contents.AsMemory(), Ct);
    }

    internal static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);
    }

    internal static (HttpClient Client, CookieContainer Cookies) Browser(X509Certificate2 tls)
    {
        var cookies = new CookieContainer();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies,
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                certificate is not null && certificate.GetCertHashString() == tls.GetCertHashString() }
        };
        return (new HttpClient(handler), cookies);
    }

    public async ValueTask DisposeAsync()
    { await loader.DisposeAsync(); await app.DisposeAsync(); Directory.Delete(directory, true); }
    internal async Task CompleteLogin(HttpClient browser)
    {
        using var begin = await browser.GetAsync(new Uri(address, "auth/login?returnTo=%2Fapp"), Ct);
        Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
        using var authorize = await browser.GetAsync(begin.Headers.Location, Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.Equal(address.Host, authorize.Headers.Location!.Host);
        using var callback = await browser.GetAsync(authorize.Headers.Location, Ct);
        Assert.True(callback.StatusCode == HttpStatusCode.Redirect, await callback.Content.ReadAsStringAsync(Ct));
        Assert.Equal("/app", callback.Headers.Location!.OriginalString);
    }
    private static PluginDefinition Definition(string id, string plugin, IQianPlugin implementation, JsonElement config, params ServiceKey[] provides) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, config), implementation, provides);
    private sealed class SecretObserver : ISecretAccessObserver
    {
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
