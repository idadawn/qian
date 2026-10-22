using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Kun.Identity.Abstractions.Session;
using Kun.Identity.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Qian.Identity.Client;

/// <summary>
/// 当前浏览器会话的访问令牌。到期前用 refresh token 换新：同一进程内同一会话只发一次刷新请求，跨副本用 Kun 会话注册表的一次性标记互斥
/// （发行方开启了 refresh 轮换与复用检测，并发复用会吊销整族）；刷新结果只续写仍然存活的会话，已登出的会话不会因刷新复活；刷新失败即结束会话。
/// </summary>
public sealed partial class IdentitySessionTokens
{
    private const string RefreshPurpose = "qian-identity-client:refresh";
    private static readonly TimeSpan RefreshClaim = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshWait = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, Lazy<Task<SessionData?>>> _refreshing = new(StringComparer.Ordinal);
    private readonly IdentityClientRuntime _runtime;
    private readonly TimeProvider _clock;
    private readonly ILogger<IdentitySessionTokens> _logger;

    internal IdentitySessionTokens(IdentityClientRuntime runtime, TimeProvider clock, ILogger<IdentitySessionTokens> logger)
    {
        _runtime = runtime; _clock = clock; _logger = logger;
    }

    /// <summary>返回当前会话有效的访问令牌；没有会话、会话已结束或刷新失败时返回 null。令牌只在服务端使用，不得写入日志、响应或前端存储。</summary>
    public async ValueTask<string?> GetAccessTokenAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_runtime.Active || _runtime.Options.RedirectUri is null) return null;
        var id = context.Request.Cookies[IdentityClientDefaults.SessionCookieName];
        if (string.IsNullOrEmpty(id)) return null;
        var store = context.RequestServices.GetRequiredService<ISessionStore>();
        var data = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (data is null) return null;
        if (data.ExpiresAt - _clock.GetUtcNow() > _runtime.Options.RefreshBeforeExpiry) return data.AccessToken;
        var key = Hash(id);
        // 同一会话只有一个进行中的刷新；已完成的残留项替换为新的，绝不把旧结果当成本次的刷新。
        // 刷新不随发起请求取消：等候同一刷新的其他请求不能因第一个请求断开而失败。
        var lazy = _refreshing.AddOrUpdate(key, _ => Start(store, id, data), (_, existing) => existing.IsValueCreated && existing.Value.IsCompleted ? Start(store, id, data) : existing);
        var refresh = lazy.Value;
        SessionData? updated;
        try { updated = await refresh.WaitAsync(cancellationToken).ConfigureAwait(false); }
        finally { if (refresh.IsCompleted) _refreshing.TryRemove(KeyValuePair.Create(key, lazy)); }
        if (updated is not null) return updated.AccessToken;
        if (!context.Response.HasStarted) context.Response.Cookies.Delete(IdentityClientDefaults.SessionCookieName, new CookieOptions { Path = "/" });
        return null;
    }

    private Lazy<Task<SessionData?>> Start(ISessionStore store, string id, SessionData data) => new(() => RefreshAsync(store, id, data));

    private async Task<SessionData?> RefreshAsync(ISessionStore store, string id, SessionData data)
    {
        await Task.Yield();
        using var timeout = new CancellationTokenSource(RefreshWait);
        var ct = timeout.Token;
        var registry = store as ISessionRegistry ?? throw new InvalidOperationException("浏览器会话需要原子的 ISessionRegistry。");
        var lifetime = _runtime.Options.SessionLifetime;
        try
        {
            if (data.RefreshToken is null)
            {
                await store.DeleteAsync(id, ct).ConfigureAwait(false);
                var hash = Hash(id);
                Log.RefreshUnavailable(_logger, hash);
                return null;
            }
            if (!await registry.TryUseNonceAsync(RefreshPurpose, Hash(data.RefreshToken), RefreshClaim, ct).ConfigureAwait(false))
            {
                // 另一个副本正在用同一个 refresh token 换新：等它写回，不重复使用（重复使用会触发整族吊销）。
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
                    var current = await store.GetAsync(id, ct).ConfigureAwait(false);
                    if (current is null || current.RefreshToken != data.RefreshToken || current.AccessToken != data.AccessToken) return current;
                }
            }
            var client = await _runtime.ClientAsync(ct).ConfigureAwait(false);
            var tokens = await client.RefreshAsync(data.RefreshToken, data.UserId, ct).ConfigureAwait(false);
            var refreshed = data with { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken ?? data.RefreshToken, ExpiresAt = _clock.GetUtcNow().AddSeconds(tokens.ExpiresIn) };
            if (!await registry.RenewAsync(id, lifetime, ct).ConfigureAwait(false)) return null;
            await store.SetAsync(id, refreshed, lifetime, ct).ConfigureAwait(false);
            return refreshed;
        }
        catch (Exception error) when (error is HttpRequestException or SecurityTokenException or InvalidOperationException or OperationCanceledException or ArgumentException)
        {
            Failed(id, error);
            try { await store.DeleteAsync(id, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { Failed(id, cleanup); }
            return null;
        }
    }

    private void Failed(string id, Exception error)
    {
        var hash = Hash(id); var type = error.GetType().Name;
        Log.RefreshFailed(_logger, hash, type);
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static partial class Log
    {
        [LoggerMessage(EventId = 20, Level = LogLevel.Warning, Message = "Identity Client 刷新会话令牌失败，会话结束；sid_hash={SessionHash}；error_type={ErrorType}。")]
        internal static partial void RefreshFailed(ILogger logger, string sessionHash, string errorType);
        [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Identity Client 会话的访问令牌已到期且没有 refresh token，会话结束；sid_hash={SessionHash}。")]
        internal static partial void RefreshUnavailable(ILogger logger, string sessionHash);
    }
}

/// <summary>
/// qian.session：在 Kun 已验证的 Cookie 会话之上取访问令牌（必要时刷新）并按发行方 JWKS 验签，投影规范身份。
/// 网关因此能把 Cookie 会话与外部 Bearer 统一成同一种主体再铸 S2S。
/// </summary>
internal sealed class IdentitySessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IdentityClientRuntime runtime, IdentitySessionTokens tokens, IdentityAccessTokens validator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!runtime.Active || Context.User.Identity is not ClaimsIdentity { IsAuthenticated: true } session ||
            session.AuthenticationType != KunIdentityAspNetCoreServiceCollectionExtensions.CookieScheme) return AuthenticateResult.NoResult();
        var token = await tokens.GetAccessTokenAsync(Context, Context.RequestAborted).ConfigureAwait(false);
        if (token is null) return AuthenticateResult.Fail("会话已结束。");
        var identity = await validator.ValidateAsync(token, sessionOnly: true, Context.RequestAborted).ConfigureAwait(false);
        if (identity is null || !identity.Matches(session)) return AuthenticateResult.Fail("会话令牌无效。");
        Context.Items[IdentityClientCurrentUser.PrincipalKey] = identity.Snapshot;
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity.Merge(session, Scheme.Name)), Scheme.Name));
    }
}
