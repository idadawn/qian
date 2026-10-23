using Microsoft.IdentityModel.Tokens;

namespace Qian.Identity.Client;

/// <summary>Client 的固定名称：会话 Cookie、认证方案、防伪请求头与发行方 HTTP 客户端。</summary>
public static class IdentityClientDefaults
{
    /// <summary>浏览器会话 Cookie 名；值是 256 位不透明会话标识，不是令牌。</summary>
    public const string SessionCookieName = "qian.sid";
    /// <summary>Cookie 会话的规范主体方案（配置了 <see cref="IdentityClientOptions.Resource"/> 时为缺省方案）。</summary>
    public const string SessionScheme = "qian.session";
    /// <summary>S2S 令牌校验方案；沿用 Kun.Security 服务认证的固定方案名。</summary>
    public const string S2sScheme = "Bearer";
    /// <summary>防伪令牌请求头；Cookie 认证的非安全方法必须携带。</summary>
    public const string AntiforgeryHeader = "X-CSRF-TOKEN";
    /// <summary>访问发行方（discovery、JWKS、令牌端点）所用的命名 HTTP 客户端；宿主可为它配置代理或受信根证书。</summary>
    public const string HttpClientName = "Qian.Identity.Client";
    /// <summary>注册给 Kun 依赖方辅助类的发行方名称。</summary>
    public const string ProviderName = "qian";
}

/// <summary>依赖方装配参数；不含任何密钥，客户端密钥只经组件配置的 secretRef 在挂载期解析。</summary>
public sealed class IdentityClientOptions
{
    /// <summary>发行方的规范 issuer；discovery 文档位于 <c>{Authority}/.well-known/openid-configuration</c>，其 issuer 必须与之完全一致。</summary>
    public required Uri Authority { get; init; }
    /// <summary>在发行方登记的客户端标识。</summary>
    public required string ClientId { get; init; }
    /// <summary>授权请求的 scope，必须含 openid；需要刷新时加 offline_access。</summary>
    public IReadOnlyList<string> Scopes { get; init; } = ["openid"];
    /// <summary>
    /// 访问令牌的受众（RFC 8707 resource，须是发行方为该客户端登记的资源之一）。配置后授权请求带 resource，发行方据此把访问令牌受众收窄到它；访问令牌按此受众验签，
    /// Cookie 会话的主体取自访问令牌的规范身份声明（对接 qian-identity，网关可直接据此铸 S2S）。不配置时授权请求不带 resource，会话主体只取自 id_token，可对接任意 OIDC 发行方。
    /// </summary>
    public string? Resource { get; init; }
    /// <summary>在发行方登记的公开回调地址，只来自可信配置、不按请求 Host 推导；回调端点按其路径映射。为空时不启用浏览器会话。</summary>
    public Uri? RedirectUri { get; init; }
    /// <summary>登录、登出、防伪令牌与后通道登出端点的路径前缀。</summary>
    public string PathPrefix { get; init; } = "/auth";
    /// <summary>returnTo 缺省或不合法时的同源落点。</summary>
    public string DefaultReturnPath { get; init; } = "/";
    /// <summary>returnTo 允许的同源路径前缀（按路径段匹配）；为空时接受任意同源相对路径。</summary>
    public IReadOnlyList<string> AllowedReturnPaths { get; init; } = [];
    /// <summary>要求授权响应携带 iss（RFC 9207）；无论是否要求，携带时总与发行方比对。qian-identity 总会携带。</summary>
    public bool RequireIssuerParameter { get; init; }
    /// <summary>发行方元数据与端点必须是 HTTPS；只有本机测试发行方可关闭。</summary>
    public bool RequireHttpsMetadata { get; init; } = true;
    /// <summary>discovery 与 JWKS 的最长缓存时间；到期后下次使用时重新获取，遇到未知签名密钥另行提前刷新。</summary>
    public TimeSpan DiscoveryRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>访问令牌在到期前多久用 refresh token 换新。</summary>
    public TimeSpan RefreshBeforeExpiry { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>会话寿命，每次经认证的请求滑动续期。</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromHours(8);
    /// <summary>会话存储的 Redis 键前缀；宿主须登记 IConnectionMultiplexer。多副本共享同一前缀与 Data Protection 密钥。</summary>
    public string SessionKeyPrefix { get; init; } = "qian:identity-client:";
    /// <summary>改用进程内会话存储；只用于单进程开发与测试，生产不得开启。</summary>
    public bool UseInMemorySessionStore { get; init; }
    /// <summary>会话与登录事务 Cookie 只经 HTTPS 发送；只有 http://localhost 开发可关闭。</summary>
    public bool RequireSecureCookies { get; init; } = true;
    /// <inheritdoc />
    public override string ToString() => "IdentityClientOptions(redacted)";

    internal string Issuer => Authority.AbsoluteUri.TrimEnd('/');
    internal string LoginPath => PathPrefix + "/login";
    internal string LogoutPath => PathPrefix + "/logout";
    internal string CsrfPath => PathPrefix + "/csrf";
    internal string SessionPath => PathPrefix + "/session";
    internal string SessionRefreshPath => PathPrefix + "/session/refresh";
    internal string BackchannelPath => PathPrefix + "/backchannel-logout";
    internal string Scope => string.Join(' ', Scopes);

    internal void Validate()
    {
        if (!Authority.IsAbsoluteUri || Authority.Scheme is not ("https" or "http") || RequireHttpsMetadata && Authority.Scheme != "https" ||
            Authority.UserInfo.Length != 0 || Authority.Query.Length != 0 || Authority.Fragment.Length != 0)
            throw new ArgumentException("Authority 必须是不含用户信息、查询串与片段的绝对 HTTPS 地址。", nameof(Authority));
        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.Length > 200 || ClientId.Any(char.IsControl))
            throw new ArgumentException("ClientId 无效。", nameof(ClientId));
        if (!Scopes.Contains("openid", StringComparer.Ordinal) || Scopes.Any(scope => scope.Length == 0 || scope.Any(c => c <= ' ' || c is '"' or '\\' || c > '~')))
            throw new ArgumentException("Scopes 必须含 openid，且每项是合法的 OAuth scope。", nameof(Scopes));
        if (Resource is not null && (!Uri.TryCreate(Resource, UriKind.Absolute, out var resource) || resource.Fragment.Length != 0))
            throw new ArgumentException("Resource 必须是不含片段的绝对 URI（RFC 8707）。", nameof(Resource));
        if (RedirectUri is { } redirect && (!redirect.IsAbsoluteUri || redirect.Scheme is not ("https" or "http") || redirect.UserInfo.Length != 0 ||
            redirect.Query.Length != 0 || redirect.Fragment.Length != 0 || redirect.AbsolutePath.Length < 2))
            throw new ArgumentException("RedirectUri 必须是不含查询串与片段、路径非根的绝对地址。", nameof(RedirectUri));
        if (!IsLocalPath(PathPrefix) || PathPrefix.Length < 2 || PathPrefix.EndsWith('/'))
            throw new ArgumentException("PathPrefix 必须是不以 / 结尾的同源路径。", nameof(PathPrefix));
        if (!IsLocalPath(DefaultReturnPath) || AllowedReturnPaths.Any(path => !IsLocalPath(path)))
            throw new ArgumentException("returnTo 落点与白名单必须是同源相对路径。", nameof(AllowedReturnPaths));
        if (DiscoveryRefreshInterval < TimeSpan.FromMinutes(5) || DiscoveryRefreshInterval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(DiscoveryRefreshInterval), "discovery 缓存时间须在 5 分钟到 1 天之间。");
        if (RefreshBeforeExpiry < TimeSpan.Zero || RefreshBeforeExpiry > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(RefreshBeforeExpiry), "提前刷新时间须在 0 到 10 分钟之间。");
        if (SessionLifetime < TimeSpan.FromMinutes(5) || SessionLifetime > TimeSpan.FromDays(30))
            throw new ArgumentOutOfRangeException(nameof(SessionLifetime), "会话寿命须在 5 分钟到 30 天之间。");
        if (!UseInMemorySessionStore && (string.IsNullOrWhiteSpace(SessionKeyPrefix) || SessionKeyPrefix.Contains('{', StringComparison.Ordinal) || SessionKeyPrefix.Contains('}', StringComparison.Ordinal)))
            throw new ArgumentException("SessionKeyPrefix 不能为空或含 Redis 哈希标签。", nameof(SessionKeyPrefix));
    }

    // 同源相对路径：以单个 / 开头，不含反斜杠与控制字符（与 Kun 登录事务对 returnUrl 的判定一致，另加长度上限）。
    internal static bool IsLocalPath(string? path) => !string.IsNullOrEmpty(path) && path.Length <= 2048 && path[0] == '/' &&
        (path.Length == 1 || path[1] != '/') && !path.Contains('\\', StringComparison.Ordinal) && !path.Any(char.IsControl);
}

/// <summary>下游服务的 S2S 校验参数：只信网关签发器的 issuer 与 JWKS，令牌 aud 必须含本服务受众。</summary>
public sealed class IdentityS2sOptions
{
    /// <summary>网关 S2S 签发器的规范 issuer（每环境一个）；与身份服务的 issuer 不同。</summary>
    public required string Issuer { get; init; }
    /// <summary>本服务的受众标识（qian-platform、qian-identity 或 service 组件 ID）。</summary>
    public required string Audience { get; init; }
    /// <summary>网关内网 JWKS 文档地址（原始 JWKS，不是 discovery）。</summary>
    public Uri? JwksUri { get; init; }
    /// <summary>静态公钥集，用于离线部署或测试；与 <see cref="JwksUri"/> 二选一。</summary>
    public JsonWebKeySet? SigningKeys { get; init; }
    /// <inheritdoc />
    public override string ToString() => "IdentityS2sOptions(redacted)";

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 255 || Issuer.Any(char.IsControl))
            throw new ArgumentException("S2S Issuer 无效。", nameof(Issuer));
        if (string.IsNullOrWhiteSpace(Audience) || Audience.Length > 255 || Audience.Any(char.IsControl) || Audience == "kun-internal")
            throw new ArgumentException("S2S Audience 必须是本服务的显式受众标识。", nameof(Audience));
        if ((JwksUri is null) == (SigningKeys is null) || JwksUri is { } uri && (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http")))
            throw new ArgumentException("JwksUri 与 SigningKeys 必须恰好配置一个。", nameof(JwksUri));
    }
}
