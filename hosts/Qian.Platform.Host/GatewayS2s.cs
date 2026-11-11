using System.Text.Json;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Platform.Host;

/// <summary>
/// 网关 S2S 认证：两个 JWT 方案共用网关的签发方与 JWKS，受众分别是 <c>qian-platform</c>（能力目录、导航、外壳目录、有效权限）与 <c>qian-operations</c>（平台运营接口，R144）。
/// 默认方案按路由实际匹配到的端点选其一（<see cref="Select"/>）：运营端点组里的端点只认运营方案，其余端点只认平台方案，受众错配即 401。
/// 只装这两个方案：不认浏览器 Cookie，也不认身份服务签发的外部访问令牌（签发方与受众都不符）。
/// 令牌必须完整校验 issuer、受众、有效期与签名，只接受 RS256；载荷即主体快照（冻结的 S2S claim 集），接受 user / service / agent。
/// 带 <c>act</c> 的委托主体照样交给端点，由能力目录、执行入口、有效权限、运营接口与命令管道各自按不存在或拒绝处理（S1 不开放委托），响应与参考宿主相同。
/// </summary>
public static class GatewayS2s
{
    /// <summary>按匹配到的端点转发的默认方案。</summary>
    public const string Scheme = "qian-s2s";
    /// <summary>受众 <c>qian-platform</c> 的方案。</summary>
    public const string PlatformScheme = "qian-s2s-platform";
    /// <summary>受众 <c>qian-operations</c> 的方案。</summary>
    public const string OperationsScheme = "qian-s2s-operations";
    private static readonly object Snapshot = new();

    internal static void Add(IServiceCollection services, PlatformHostSettings settings, IReadOnlyList<PathString> operations)
    {
        // 两个方案共用一个 JWKS 管理器：网关轮换签名钥时见到未知 kid 即刷新（JwtBearer 的 RefreshOnIssuerKeyNotFound），旧钥在网关侧重叠 10 分钟。
        var jwks = new ConfigurationManager<OpenIdConnectConfiguration>(settings.S2sJwks.AbsoluteUri, new JwksRetriever(settings.S2sIssuer),
            new HttpDocumentRetriever { RequireHttps = settings.S2sJwks.Scheme == "https" })
        { AutomaticRefreshInterval = TimeSpan.FromMinutes(5), RefreshInterval = TimeSpan.FromSeconds(10) };
        services.AddAuthentication(Scheme)
            .AddPolicyScheme(Scheme, "网关 S2S", options => options.ForwardDefaultSelector = http => Select(http, operations))
            .AddJwtBearer(PlatformScheme, options => Configure(options, settings, PlatformHostSettings.PlatformAudience, jwks))
            .AddJwtBearer(OperationsScheme, options => Configure(options, settings, PlatformHostSettings.OperationsAudience, jwks));
    }

    /// <summary>
    /// 按路由实际匹配到的端点选方案，不看路径的写法：端点路由不区分大小写、忽略结尾斜杠，<c>/api/Tenancy/tenants/</c> 与 <c>/api/tenancy/tenants</c> 是同一个端点，受众也必须相同。
    /// 认证在路由之后执行（<see cref="PlatformHost.UseQianPlatform"/> 先调 <c>UseRouting</c>），这里读到的就是将要执行的端点。
    /// 带 <see cref="OperationsAudience"/> 的端点（运营端点组）只认运营方案，其余路由端点只认平台方案。没有匹配到路由端点的请求（404，或方法不符时的 405 拒绝端点，它不是路由端点）
    /// 不会执行任何处理器，按声明的运营前缀、与路由同样不区分大小写地选：网关签运营受众转来的请求得到 404 / 405，而不是会被外壳当成会话失效的 401。
    /// </summary>
    internal static string Select(HttpContext http, IReadOnlyList<PathString> operations) => http.GetEndpoint() switch
    {
        { } endpoint when endpoint.Metadata.GetMetadata<OperationsAudience>() is not null => OperationsScheme,
        RouteEndpoint => PlatformScheme,
        _ => operations.Any(prefix => http.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)) ? OperationsScheme : PlatformScheme
    };

    /// <summary>
    /// 启动时核对运营端点组与声明的运营前缀一致：网关按前缀把请求路由到运营簇（签运营受众），本宿主按匹配到的端点选受众，两边不一致时合法请求会被 401。
    /// 运营端点组里的端点必须在某个运营前缀下，前缀下的端点必须都在运营端点组里（比较与路由一样不区分大小写）；不一致即抛错，宿主不启动。
    /// </summary>
    internal static void CheckOperations(IEndpointRouteBuilder endpoints, IReadOnlyList<PathString> operations)
    {
        foreach (var endpoint in endpoints.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
        {
            var pattern = "/" + (endpoint.RoutePattern.RawText ?? "").TrimStart('/');
            var declared = operations.Any(prefix => new PathString(pattern).StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
            var grouped = endpoint.Metadata.GetMetadata<OperationsAudience>() is not null;
            if (declared != grouped)
                throw new InvalidOperationException(grouped
                    ? "运营端点 " + pattern + " 不在任何运营前缀下：AddOperations 的 map 只能映射它声明的前缀下的端点。"
                    : "端点 " + pattern + " 在运营前缀下，却不在运营端点组里：运营端点经 AddOperations 挂上，平台端点不要放在运营前缀下。");
        }
    }

    /// <summary>当前请求已验证的主体快照；未认证或令牌不合格时为 null。</summary>
    public static Principal? Principal(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.Items[Snapshot] as Principal;
    }

    private static void Configure(JwtBearerOptions options, PlatformHostSettings settings, string audience, ConfigurationManager<OpenIdConnectConfiguration> jwks)
    {
        options.MapInboundClaims = false;
        options.ConfigurationManager = jwks;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = settings.S2sIssuer, ValidateAudience = true, ValidAudience = audience,
            ValidateLifetime = true, RequireExpirationTime = true, ValidateIssuerSigningKey = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = settings.S2sClockSkew
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = context => { Capture(context, audience); return Task.CompletedTask; } };
    }

    // 只消费已验签的令牌；请求头、正文与客户端提供的布尔值都不是身份来源。
    private static void Capture(TokenValidatedContext context, string audience)
    {
        try
        {
            if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind))
            { context.Fail("缺少已验证的主体。"); return; }
            // 网关每跳只绑定一个下游受众：多受众的令牌即使含本方案的受众也不收。
            var audiences = token.Audiences.ToArray();
            if (audiences.Length != 1 || audiences[0] != audience) { context.Fail("受众必须恰好是本宿主的这一个。"); return; }
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
            var payload = document.RootElement;
            if (payload.ValueKind != JsonValueKind.Object ||
                payload.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != payload.EnumerateObject().Count())
            { context.Fail("身份字段重复或无效。"); return; }
            var principal = payload.Deserialize(IdentityJson.PrincipalTypeInfo);
            var principalKind = principal?.Kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", PrincipalKind.Agent => "agent", _ => null };
            // 防御性比较，现有输入触发不到（验收复核 M7 判为等价突变）：快照与声明出自同一份已验签载荷，重复字段在上面已拒绝；快照反序列化只收精确的 snake_case 字段名、
            // 不收未知字段，sub / tenant_id / principal_kind 只收 JSON 字符串（tenant_id 另可为 null），principal_kind 只收 user / service / agent；
            // IdentityModel 8.x 把每个字符串字段原样转成一条同值声明（日期样式的值也不改写），JSON null 转成 JSON_NULL，PrincipalClaims 读作 null，所以两边都读得出时三项必然相同。
            // 保留它是为了在解析规则一侧改变（字段名大小写、声明转换）时仍按 401 拒绝；PEP 的 HttpEnforcement.ReadSubject 在每个受管端点上还会再比一次。
            if (principal is null || principal.Subject != subject || principal.TenantId != tenant || principalKind != kind)
            { context.Fail("主体快照不一致。"); return; }
            context.HttpContext.Items[Snapshot] = principal;
        }
        catch (JsonException) { context.Fail("身份快照无效。"); }
    }

    // 网关只公开 JWKS（/.well-known/jwks.json），不是 OIDC 发现文档：取回后包成配置，签发方固定为配置的网关 issuer。
    private sealed class JwksRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var keys = new JsonWebKeySet(await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false));
            if (keys.Keys.Count == 0 || keys.Keys.Any(key => key.HasPrivateKey || key.Kty != JsonWebAlgorithmsKeyTypes.RSA))
                throw new InvalidOperationException("网关 JWKS 必须只含 RSA 公钥。");
            var configuration = new OpenIdConnectConfiguration { Issuer = issuer, JwksUri = address, JsonWebKeySet = keys };
            foreach (var key in keys.GetSigningKeys()) configuration.SigningKeys.Add(key);
            return configuration;
        }
    }
}

/// <summary>
/// 运营端点组的端点元数据：带它的端点只收受众 <c>qian-operations</c> 的网关 S2S（R144 ①）。<see cref="PlatformHost.MapQianPlatform"/> 把它加在运营端点组上，
/// 租户运营接口与经 <see cref="PlatformHostComposition.AddOperations"/> 挂上的端点都在组里；扩展不直接使用它。
/// </summary>
internal sealed class OperationsAudience
{
    internal static OperationsAudience Instance { get; } = new();
    private OperationsAudience() { }
}

/// <summary>
/// 宿主根容器里唯一的身份适配器：平台表面、运营接口与同进程的 module 应用读同一份已验证快照（S1-YUE-015 裁决 ④）。
/// 生产单宿主里应用的命令管道与平台表面共用这个 DI，不需要参考栈「借用运行时 + 适配器交快照」的做法。
/// 委托主体（带 <c>act</c>）在执行入口按不存在拒绝，到不了命令管道；命令管道的 RBAC 策略对委托主体同样拒绝。
/// </summary>
internal sealed class GatewayCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Principal? Principal => accessor.HttpContext is { } http ? GatewayS2s.Principal(http) : null;
    public string? UserId => Principal?.Subject;
    public IReadOnlyList<string> Roles => [];
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;
}
