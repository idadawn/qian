#pragma warning disable RS0026 // 与 Kun 的 IOidcClient 保持同一组重载。
using Kun.Identity.Abstractions;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Qian.Config;
using Qian.Runtime;

namespace Qian.Identity.Client;

/// <summary>
/// 组件挂载状态。挂载只记录按本组件身份解析 secretRef 的委托；Kun 的 OIDC 客户端在第一次需要时才创建并解析密钥，
/// 所以不要求 Config 先于本组件挂载，也让「只装 Client」的公开客户端宿主完全不经 Config。卸载即释放客户端与元数据缓存。
/// </summary>
internal sealed partial class IdentityClientRuntime(IdentityClientOptions options, IHttpClientFactory http, TimeProvider clock, ILogger<IdentityClientRuntime> logger) : IDisposable
{
    private Mount? _mount;

    internal IdentityClientOptions Options => options;
    internal bool Active => Volatile.Read(ref _mount) is not null;

    internal void Activate(Func<CancellationToken, ValueTask<SecretValue>>? resolveSecret)
    {
        var retriever = new HttpDocumentRetriever(http.CreateClient(IdentityClientDefaults.HttpClientName)) { RequireHttps = options.RequireHttpsMetadata };
        // 自动刷新取 discovery 缓存时间；遇到未知 kid 时 RequestRefresh，两次强制刷新至少间隔 30 秒，免得伪造 kid 打满发行方。
        var metadata = new ConfigurationManager<OpenIdConnectConfiguration>(options.Issuer + "/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(), retriever)
            { AutomaticRefreshInterval = options.DiscoveryRefreshInterval, RefreshInterval = TimeSpan.FromSeconds(30) };
        var mount = new Mount(resolveSecret, metadata);
        if (Interlocked.CompareExchange(ref _mount, mount, null) is not null) { mount.Dispose(); throw new InvalidOperationException("同一宿主只允许一个 Identity Client。"); }
    }

    internal void Deactivate() => Interlocked.Exchange(ref _mount, null)?.Dispose();

    internal ConfigurationManager<OpenIdConnectConfiguration> Metadata => Current.Metadata;

    internal async ValueTask<KunOidcClient> ClientAsync(CancellationToken ct)
    {
        var mount = Current;
        if (Volatile.Read(ref mount.Client) is { } ready) return ready;
        await mount.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (mount.Client is { } existing) return existing;
            var secret = "";
            if (mount.ResolveSecret is { } resolve)
            {
                // 每个挂载只解析一次：Kun 的客户端在自身生命周期内持有密钥；轮换密钥 = 重新挂载组件。
                using var value = await resolve(ct).ConfigureAwait(false);
                secret = value.RevealUtf8();
            }
            var client = new KunOidcClient(new KunOidcClientOptions
            {
                Name = IdentityClientDefaults.ProviderName, Authority = options.Authority.AbsoluteUri, ClientId = options.ClientId, ClientSecret = secret,
                DiscoveryRefreshInterval = options.DiscoveryRefreshInterval, RequireHttpsMetadata = options.RequireHttpsMetadata
            }, new NamedClients(http), clock);
            if (!ReferenceEquals(Volatile.Read(ref _mount), mount)) { client.Dispose(); throw new ServiceNotReadyException(); }
            Volatile.Write(ref mount.Client, client);
            return client;
        }
        finally { mount.Gate.Release(); }
    }

    // 健康检查：discovery 与 JWKS 都取得到才算就绪（网关 /readyz 的「JWKS 可取」）。异常只记类型，不记地址以外的内容。
    internal async Task<HealthCheckResult> CheckAsync(CancellationToken ct)
    {
        if (!Active) return HealthCheckResult.Unhealthy("Identity Client 未挂载。");
        try
        {
            await (await ClientAsync(ct).ConfigureAwait(false)).DiscoverAsync(ct).ConfigureAwait(false);
            var metadata = await Metadata.GetConfigurationAsync(ct).ConfigureAwait(false);
            return metadata.SigningKeys.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("发行方没有可用的签名密钥。");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Log.MetadataUnavailable(logger, error.GetType().Name);
            return HealthCheckResult.Unhealthy("发行方元数据不可用。");
        }
    }

    public void Dispose() => Deactivate();

    private Mount Current => Volatile.Read(ref _mount) ?? throw new ServiceNotReadyException();

    private sealed class Mount(Func<CancellationToken, ValueTask<SecretValue>>? resolveSecret, ConfigurationManager<OpenIdConnectConfiguration> metadata) : IDisposable
    {
        internal KunOidcClient? Client;
        internal Func<CancellationToken, ValueTask<SecretValue>>? ResolveSecret => resolveSecret;
        internal ConfigurationManager<OpenIdConnectConfiguration> Metadata => metadata;
        internal SemaphoreSlim Gate { get; } = new(1, 1);
        public void Dispose() { Interlocked.Exchange(ref Client, null)?.Dispose(); }
    }

    // Kun 按发行方名称取命名客户端；统一落到 Client 的公开命名客户端，宿主只需配置这一个。
    private sealed class NamedClients(IHttpClientFactory inner) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => inner.CreateClient(IdentityClientDefaults.HttpClientName);
    }

    internal static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Identity Client 取发行方元数据失败；error_type={ErrorType}。")]
        internal static partial void MetadataUnavailable(ILogger logger, string errorType);
    }
}

/// <summary>把 Kun 依赖方辅助类（登录事务、后通道登出）接到当前挂载的客户端；未挂载时抛 ServiceNotReady。</summary>
internal sealed class ForwardingOidcClient(IdentityClientRuntime runtime) : IOidcClient
{
    public string Name => IdentityClientDefaults.ProviderName;

    public async Task<OidcConfiguration> DiscoverAsync(CancellationToken cancellationToken = default) =>
        await (await runtime.ClientAsync(cancellationToken).ConfigureAwait(false)).DiscoverAsync(cancellationToken).ConfigureAwait(false);

    [Obsolete("Pass an OidcCodeExchange with PKCE and nonce.", DiagnosticId = "KUNOIDC001")]
    public Task<OidcTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("授权码换取必须带原始 PKCE verifier 与 nonce。");

    public async Task<OidcTokenResponse> ExchangeCodeAsync(OidcCodeExchange request, CancellationToken cancellationToken = default) =>
        await (await runtime.ClientAsync(cancellationToken).ConfigureAwait(false)).ExchangeCodeAsync(request, cancellationToken).ConfigureAwait(false);

    public async Task<OidcTokenResponse> RefreshAsync(string refreshToken, string expectedSubject, CancellationToken cancellationToken = default) =>
        await (await runtime.ClientAsync(cancellationToken).ConfigureAwait(false)).RefreshAsync(refreshToken, expectedSubject, cancellationToken).ConfigureAwait(false);

    public async Task<OidcTokenResponse> ClientCredentialsAsync(string scope, CancellationToken cancellationToken = default) =>
        await (await runtime.ClientAsync(cancellationToken).ConfigureAwait(false)).ClientCredentialsAsync(scope, cancellationToken).ConfigureAwait(false);

    public async Task<OidcLogoutToken> ValidateLogoutTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await (await runtime.ClientAsync(cancellationToken).ConfigureAwait(false)).ValidateLogoutTokenAsync(token, cancellationToken).ConfigureAwait(false);
}

internal sealed class IdentityClientHealthCheck(IdentityClientRuntime runtime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) => runtime.CheckAsync(cancellationToken);
}
