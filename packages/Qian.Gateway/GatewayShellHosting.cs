using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kun.Gateway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Gateway;

/// <summary>
/// 外壳的网关侧（S1-YUE-012）：bootstrap 聚合端点与外壳静态托管。外壳是 platform 的 shell 组合包（静态制品），经网关同源提供；
/// 网关代理路由一律要求认证，所以静态外壳、运行时配置与已部署组件的远程制品由网关进程自己从目录提供。
/// </summary>
public static partial class GatewayShellHosting
{
    /// <summary>bootstrap 聚合端点的缺省路径（外壳运行时配置 bootstrapUrl 的缺省值）。</summary>
    public const string BootstrapPath = "/api/shell/bootstrap";
    internal const string ShellCatalogPath = "/api/capabilities/shell";
    internal const string NavigationPath = "/api/capabilities/navigation";
    internal const string PermissionsPath = "/api/authz/me/permissions";
    private const string Theme = "light";
    private const string Locale = "zh-CN";
    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)+$")]
    private static partial Regex ComponentId();

    /// <summary>
    /// 登记 <c>GET {path}</c>：外壳 bootstrap 聚合端点。会话取自 Identity.Client 的 Cookie 会话（字段与 <c>GET /auth/session</c> 相同，不含令牌），
    /// 未认证 401。平台身份（没有租户）不调下游，回代管上下文。租户成员：按本网关的路由表为平台表面的三条路径（外壳目录、能力导航、有效权限）选 cluster，
    /// 换该 cluster 受众的 60 秒 S2S 后并发调用并组合——与浏览器直调同一路由、同一受众；任一下游 401 即 401（客户端刷新会话后重放），
    /// 外壳目录或有效权限 403 即 403，其余失败 502；能力导航失败降级为 null；下游 404（组件未挂载）按空处理。
    /// 权益（entitled）的真实来源归 S1-ENT-002，此前与 mounted 等价。须在 <see cref="GatewayHosting.AddQianGateway"/> 之后、与其它端点一起映射。
    /// </summary>
    public static IEndpointRouteBuilder MapQianShellBootstrap(this IEndpointRouteBuilder endpoints, string path = BootstrapPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!IsLocalPath(path) || path.EndsWith('/')) throw new ArgumentException("bootstrap 路径必须是不含查询串、片段与结尾斜杠的同源相对路径。", nameof(path));
        _ = endpoints.ServiceProvider.GetRequiredService<GatewayRuntime>();
        endpoints.MapGet(path, (RequestDelegate)BootstrapAsync).AllowAnonymous().RequireRateLimiting("kun-default");
        return endpoints;
    }

    /// <summary>
    /// 放在 UseForwardedHeaders 之后、UseRouting 之前：静态托管外壳构建产物与已部署组件的远程制品（<c>/remotes/{组件 id}/</c>）。
    /// HTML <c>no-store</c>、<c>/assets/</c> 下带哈希的资源长缓存、远程制品与其余文件 <c>no-cache</c>；全部带 nosniff、<c>Referrer-Policy: no-referrer</c>
    /// 与 <c>frame-ancestors 'none'</c>（脚本来源由构建产物 index.html 的 meta CSP 声明）。
    /// </summary>
    public static IApplicationBuilder UseQianShell(this IApplicationBuilder app, ShellStaticOptions options)
    {
        ArgumentNullException.ThrowIfNull(app); ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(options.Root)),
            OnPrepareResponse = context => Headers(context.Context.Response, CacheFor(context.Context.Request.Path, context.File.Name))
        });
        foreach (var (id, directory) in options.Remotes)
            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = "/remotes/" + id, FileProvider = new PhysicalFileProvider(Path.GetFullPath(directory)),
                OnPrepareResponse = context => Headers(context.Context.Response, "no-cache")
            });
        return app;
    }

    /// <summary>
    /// 映射运行时配置 <c>GET /app-config.json</c>（取 <see cref="ShellStaticOptions.ConfigFile"/>，即 K8s 里 ConfigMap 挂成的文件，每次现读、<c>no-store</c>）
    /// 与外壳入口兜底：没有命中任何端点、不带扩展名、接受 <c>text/html</c> 的 GET / HEAD（浏览器的页面导航与深链接）回 index.html（<c>no-store</c>）；
    /// 其余（API 前缀下打错的路径、脚本请求、其它方法）一律 404，不拿 HTML 冒充 JSON。与 <see cref="UseQianShell"/> 用同一份选项。
    /// </summary>
    public static IEndpointRouteBuilder MapQianShell(this IEndpointRouteBuilder endpoints, ShellStaticOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(options);
        Validate(options);
        var root = Path.GetFullPath(options.Root);
        if (options.ConfigFile is { } file)
        {
            var config = Path.GetFullPath(file);
            endpoints.MapMethods("/app-config.json", [HttpMethods.Get, HttpMethods.Head], async http =>
            {
                Headers(http.Response, "no-store");
                if (!File.Exists(config)) { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
                http.Response.ContentType = "application/json; charset=utf-8";
                if (HttpMethods.IsHead(http.Request.Method)) return;
                await http.Response.SendFileAsync(config, http.RequestAborted).ConfigureAwait(false);
            }).AllowAnonymous();
        }
        var index = Path.Combine(root, "index.html");
        endpoints.MapFallback("{*path:nonfile}", async http =>
        {
            Headers(http.Response, "no-store");
            if (!(HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) || !AcceptsHtml(http.Request))
            { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            http.Response.ContentType = "text/html; charset=utf-8";
            if (HttpMethods.IsHead(http.Request.Method)) return;
            await http.Response.SendFileAsync(index, http.RequestAborted).ConfigureAwait(false);
        }).AllowAnonymous();
        return endpoints;
    }

    private static bool AcceptsHtml(HttpRequest request) =>
        request.GetTypedHeaders().Accept.Any(value => value.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) && value.Quality is not 0);

    private static void Validate(ShellStaticOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Root) || !File.Exists(Path.Combine(options.Root, "index.html")))
            throw new ArgumentException("外壳目录必须存在且含 index.html。", nameof(options));
        foreach (var (id, directory) in options.Remotes)
            if (!ComponentId().IsMatch(id) || id.Length > 128 || string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                throw new ArgumentException("远程制品必须以组件 id 为键、指向已存在的目录：" + id, nameof(options));
    }

    // 只有 Vite 输出的 /assets/ 下文件名带内容哈希，可以长缓存；入口 HTML 每次重新验证都不够（换版本后旧入口会引用已删除的分块），一律 no-store。
    private static string CacheFor(PathString path, string name) =>
        name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-store"
        : path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache";

    private static void Headers(HttpResponse response, string cacheControl)
    {
        response.Headers.CacheControl = cacheControl; response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer"; response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
    }

    private static bool IsLocalPath(string? value) =>
        value is { Length: > 1 and <= 256 } && value[0] == '/' && value[1] != '/' && value.IndexOfAny(['?', '#', '\\']) < 0 && !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl);

    private static async Task BootstrapAsync(HttpContext http)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var services = http.RequestServices;
        var runtime = services.GetRequiredService<GatewayRuntime>();
        if (!runtime.IsReady) { await Error(http, PlatformErrors.NotFound).ConfigureAwait(false); return; }
        var user = services.GetRequiredService<ICurrentUser>();
        var principal = user.Principal;
        if (http.User.Identity?.IsAuthenticated != true || !user.IsAuthenticated || principal is null || user.UserId != principal.Subject)
        { await Error(http, PlatformErrors.Unauthenticated).ConfigureAwait(false); return; }
        if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service))
        { await Error(http, PlatformErrors.AuthzDenied).ConfigureAwait(false); return; }
        var session = new ShellSessionView(principal.Subject, principal.Kind == PrincipalKind.Service ? "service" : "user", principal.TenantId, principal.MembershipId,
            principal.IdentityRevision, http.User.FindFirst(ClaimTypes.Name)?.Value);
        // 平台身份没有租户表面：不调任何下游，只给代管上下文（平台表面对无租户主体一律拒绝）。
        if (principal.TenantId is not { } tenantId)
        {
            await Write(http, new ShellBootstrapView(session, new ShellContextView(null, null, null, Locale, Theme), [], [], [], [], null, true)).ConfigureAwait(false);
            return;
        }
        var client = services.GetRequiredService<GatewayShellClient>();
        Downstream shell, navigation, permissions;
        try
        {
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            var targets = new[] { ShellCatalogPath, NavigationPath, PermissionsPath }.Select(path => Target(runtime.Configuration, path)).ToArray();
            foreach (var cluster in targets.OfType<GatewayCluster>().DistinctBy(cluster => cluster.Id))
                tokens[cluster.Id] = await services.GetRequiredService<S2sTokenIssuer>().IssueForAudienceAsync(http.User, runtime.AudienceFor(cluster.Id), http.RequestAborted).ConfigureAwait(false);
            var calls = targets.Select((cluster, index) => cluster is null ? Task.FromResult(Downstream.Unrouted)
                : client.GetAsync(http, runtime.Configuration, cluster, tokens[cluster.Id], index switch { 0 => ShellCatalogPath, 1 => NavigationPath, _ => PermissionsPath })).ToArray();
            await Task.WhenAll(calls).ConfigureAwait(false);
            (shell, navigation, permissions) = (calls[0].Result, calls[1].Result, calls[2].Result);
        }
        catch (InvalidOperationException) { await Error(http, PlatformErrors.Unauthenticated).ConfigureAwait(false); return; }
        try
        {
            if (shell.Status == HttpStatusCode.Unauthorized || navigation.Status == HttpStatusCode.Unauthorized || permissions.Status == HttpStatusCode.Unauthorized)
            { await Error(http, PlatformErrors.Unauthenticated).ConfigureAwait(false); return; }
            if (shell.Status == HttpStatusCode.Forbidden || permissions.Status == HttpStatusCode.Forbidden)
            { await Error(http, PlatformErrors.AuthzDenied).ConfigureAwait(false); return; }
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Gateway");
            if (shell.Body is null && shell.Status != HttpStatusCode.NotFound || permissions.Body is null && permissions.Status != HttpStatusCode.NotFound)
            {
                Log.DownstreamFailed(logger, (int)shell.Status, (int)permissions.Status);
                await Error(http, PlatformErrors.BadGateway).ConfigureAwait(false); return;
            }
            if (shell.Status == HttpStatusCode.NotFound || permissions.Status == HttpStatusCode.NotFound) Log.SurfaceMissing(logger, (int)shell.Status, (int)permissions.Status);
            if (!TryCatalog(shell.Body, tenantId, out var tenantName, out var remotes) || !TryPermissions(permissions.Body, out var allowed) || !TryNavigation(navigation.Body, out var items))
            {
                Log.DownstreamInvalid(logger);
                await Error(http, PlatformErrors.BadGateway).ConfigureAwait(false); return;
            }
            var mounted = remotes.Select(remote => remote.Scope + "|" + remote.Id).ToArray();
            var context = new ShellContextView(new ShellTenantView(tenantId, tenantName ?? tenantId),
                principal.MembershipId is { } membership ? new ShellMembershipView(membership, allowed) : null, null, Locale, Theme);
            await Write(http, new ShellBootstrapView(session, context, remotes, mounted, mounted, allowed, items, false)).ConfigureAwait(false);
        }
        finally { shell.Body?.Dispose(); navigation.Body?.Dispose(); permissions.Body?.Dispose(); }
    }

    // 与反向代理同一张路由表与同一优先级规则（字面段优先于参数、参数优先于通配）选目标 cluster；没有路由时视为该平台表面未部署。
    internal static GatewayCluster? Target(GatewayConfiguration configuration, string path)
    {
        GatewayRoute? best = null; var precedence = decimal.MaxValue;
        foreach (var route in configuration.Routes)
        {
            var template = TemplateParser.Parse(route.Path);
            if (!new TemplateMatcher(template, new RouteValueDictionary()).TryMatch(path, new RouteValueDictionary())) continue;
            var value = RoutePrecedence.ComputeInbound(template);
            if (value < precedence) { best = route; precedence = value; }
        }
        return best is null ? null : configuration.Clusters.SingleOrDefault(cluster => cluster.Id == best.Cluster);
    }

    private static bool TryCatalog(JsonDocument? document, string tenantId, out string? name, out IReadOnlyList<ShellRemoteView> remotes)
    {
        name = null; remotes = [];
        if (document is null) return true;
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tenant", out var tenant) || tenant.ValueKind != JsonValueKind.Object ||
            Text(tenant, "id") != tenantId || Text(tenant, "name") is not { Length: > 0 } tenantName || !root.TryGetProperty("remotes", out var items) || items.ValueKind != JsonValueKind.Array)
            return false;
        var result = new List<ShellRemoteView>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "id") is not { } id || !ComponentId().IsMatch(id) || Text(item, "version") is not { Length: > 0 and <= 64 } version ||
                Text(item, "scope") is not { } scope || !item.TryGetProperty("manifest", out var manifest) || manifest.ValueKind != JsonValueKind.Object) return false;
            // 只收当前租户与全局 scope 的条目；别的租户的 scope 出现在这里说明下游有误，整体按无效处理。
            if (scope != "global" && scope != "tenant:" + tenantId) return false;
            result.Add(new ShellRemoteView(id, version, scope, manifest.Clone()));
        }
        name = tenantName; remotes = result.AsReadOnly();
        return true;
    }

    private static bool TryPermissions(JsonDocument? document, out IReadOnlyList<string> allowed)
    {
        allowed = [];
        if (document is null) return true;
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return false;
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "permission") is not { Length: > 0 and <= 384 } permission ||
                !item.TryGetProperty("allowed", out var flag) || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            if (flag.GetBoolean()) result.Add(permission);
        }
        allowed = result.ToArray();
        return true;
    }

    private static bool TryNavigation(JsonDocument? document, out JsonElement? items)
    {
        items = null;
        if (document is null) return true;
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("items", out var value) || value.ValueKind != JsonValueKind.Array) return true;
        items = value.Clone();
        return true;
    }

    private static string? Text(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;

    private static Task Write(HttpContext http, ShellBootstrapView view) =>
        http.Response.WriteAsJsonAsync(view, GatewayShellJson.Default.ShellBootstrapView, cancellationToken: http.RequestAborted);

    private static Task Error(HttpContext http, ErrorCode code) => QianResults.Error(new QianError(code)).ExecuteAsync(http);

    private static partial class Log
    {
        [LoggerMessage(EventId = 60, Level = LogLevel.Warning, Message = "外壳 bootstrap 的平台表面调用失败；shell={Shell} permissions={Permissions}。")]
        internal static partial void DownstreamFailed(ILogger logger, int shell, int permissions);
        [LoggerMessage(EventId = 61, Level = LogLevel.Warning, Message = "外壳 bootstrap 的平台表面返回了不符合契约的响应。")]
        internal static partial void DownstreamInvalid(ILogger logger);
        [LoggerMessage(EventId = 62, Level = LogLevel.Information, Message = "外壳 bootstrap 的平台表面未挂载或未部署（404），按空处理；shell={Shell} permissions={Permissions}。")]
        internal static partial void SurfaceMissing(ILogger logger, int shell, int permissions);
    }
}

/// <summary>外壳静态托管的选项：外壳 Web 根、运行时配置文件与已部署组件的远程制品目录。</summary>
public sealed class ShellStaticOptions
{
    /// <summary>外壳构建产物目录（index.html、assets/）。</summary>
    public required string Root { get; init; }
    /// <summary>运行时配置文件（K8s 里 ConfigMap 挂成的 JSON 文件），以同源 <c>/app-config.json</c> 提供；为 null 时不映射。</summary>
    public string? ConfigFile { get; init; }
    /// <summary>已部署组件的远程制品：组件 id → 目录，以 <c>/remotes/{组件 id}/</c> 提供。</summary>
    public IReadOnlyDictionary<string, string> Remotes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

internal readonly record struct Downstream(HttpStatusCode Status, JsonDocument? Body)
{
    internal static readonly Downstream Unrouted = new(HttpStatusCode.NotFound, null);
}

/// <summary>bootstrap 调平台表面的出站连接：不跟随重定向、不带 Cookie、不走代理，归网关宿主所有。</summary>
internal sealed class GatewayShellClient : IDisposable
{
    private const int MaxBody = 4 * 1024 * 1024;
    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(5), PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = MaxBody };

    internal async Task<Downstream> GetAsync(HttpContext http, GatewayConfiguration configuration, GatewayCluster cluster, string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(cluster.Address.TrimEnd('/') + "/"), path.TrimStart('/')));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // 与反向代理的变换一致：重写已归一的客户端 IP、协议与网关主机名，不透传原始 Cookie / Authorization / Forwarded。
        if (http.Connection.RemoteIpAddress is { } remote) request.Headers.TryAddWithoutValidation("X-Forwarded-For", remote.ToString());
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", http.Request.Scheme);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", new Uri(configuration.Issuer).Authority);
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, http.RequestAborted).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK) return new(response.StatusCode, null);
            if (response.Content.Headers.ContentType?.MediaType is not { } media || !media.Contains("json", StringComparison.OrdinalIgnoreCase)) return new(HttpStatusCode.BadGateway, null);
            await using var stream = await response.Content.ReadAsStreamAsync(http.RequestAborted).ConfigureAwait(false);
            using var limited = new MemoryStream();
            var buffer = new byte[16 * 1024]; int read;
            while ((read = await stream.ReadAsync(buffer, http.RequestAborted).ConfigureAwait(false)) > 0)
            {
                if (limited.Length + read > MaxBody) return new(HttpStatusCode.BadGateway, null);
                limited.Write(buffer, 0, read);
            }
            limited.Position = 0;
            return new(HttpStatusCode.OK, await JsonDocument.ParseAsync(limited, cancellationToken: http.RequestAborted).ConfigureAwait(false));
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or TaskCanceledException && !http.RequestAborted.IsCancellationRequested)
        { return new(HttpStatusCode.BadGateway, null); }
    }

    public void Dispose() => _client.Dispose();
}

internal sealed record ShellSessionView(string Subject, string Kind, string? TenantId, string? MembershipId, long? IdentityRevision, string? DisplayName);
internal sealed record ShellTenantView(string Id, string Name);
internal sealed record ShellMembershipView(string Id, IReadOnlyList<string> Permissions);
internal sealed record ShellAppView(string Id, string Name);
internal sealed record ShellContextView(ShellTenantView? Tenant, ShellMembershipView? Membership, ShellAppView? App, string Locale, string Theme);
internal sealed record ShellRemoteView(string Id, string Version, string Scope, JsonElement Manifest);
/// <summary>bootstrap 响应：会话快照、外壳上下文、已挂载远程、挂载与权益键（<c>scope|组件 id</c>）、已放行权限码、能力导航（可为 null）与是否代管；不含任何令牌。</summary>
internal sealed record ShellBootstrapView(ShellSessionView Session, ShellContextView Context, IReadOnlyList<ShellRemoteView> Remotes, IReadOnlyList<string> Mounted,
    IReadOnlyList<string> Entitled, IReadOnlyList<string> Permissions, JsonElement? Navigation, bool Managed);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(ShellBootstrapView))]
internal sealed partial class GatewayShellJson : JsonSerializerContext;
