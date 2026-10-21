using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kun.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Config.AspNetCore;

/// <summary>
/// 组件配置 Schema 与平台配置值的只读 HTTP 表面（PRD 7.8）。宿主认证后从 global 配置组件上下文解析，该上下文须能注入
/// Registry、Config 与 AuthzEnforcement；端点归属平台契约（PlatformHttpContract），不进入租户可见的组件 HTTP 聚合。
/// </summary>
public static partial class ConfigEndpoints
{
    /// <summary>Schema 响应的媒体类型。</summary>
    public const string SchemaMediaType = "application/schema+json";
    private const string ValuesRoute = "/api/config/values/{plugin}";
    private const int SchemaLimit = 1_048_576;
    private static readonly JsonElement SchemaDocument = JsonElement.Parse("""{"type":"object","description":"组件 config.schema（JSON Schema 2020-12），不含配置值。"}""");

    /// <summary>
    /// 映射 <c>GET /api/config/schemas/{plugin}</c>（Schema 原文，对已认证调用方可读）与 <c>GET /api/config/values/{plugin}</c>
    /// （平台配置节 qian.plugin.&lt;组件 id&gt; 的当前值：只对平台主体开放，经执行端口判定持有 <see cref="ConfigDefinition.ValuesReadPermission"/>
    /// 后返回，敏感键只给键名）。两者只认 Registry 预装集合中由制品捕获的组件文件；未预装、没有文件快照或没有 Schema 的组件为 404。
    /// </summary>
    public static IEndpointRouteBuilder MapQianConfig(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        Bind(endpoints.MapGet("/api/config/schemas/{plugin}", (HttpContext http, string plugin) =>
            Respond(http, source, plugin, false, (_, _, schema) => Results.Bytes(schema, SchemaMediaType))))
            .Produces<JsonElement>(StatusCodes.Status200OK, SchemaMediaType)
            .WithMetadata(new HttpResponseSchema(StatusCodes.Status200OK, SchemaDocument, SchemaMediaType),
                new PermissionEnforcedInHandlerAttribute("配置 Schema 不含配置值，对已认证调用方公开（PRD 7.8）；处理器只返回宿主预装组件的 Schema。"));
        Bind(endpoints.MapGet(ValuesRoute, (HttpContext http, string plugin) =>
            Respond(http, source, plugin, true, (ctx, component, schema) =>
            {
                var section = NamespacePrefix + component.PluginId;
                var values = ctx.Config().ReadSection(section);
                using var document = JsonDocument.Parse(schema);
                var hidden = Sensitive(values, document.RootElement);
                return Results.Json(new ConfigValueList(component.PluginId, section, values.Select(pair => hidden.Contains(pair.Key)
                    ? new ConfigValueItem(pair.Key, true, null) : new ConfigValueItem(pair.Key, false, pair.Value)).ToArray()), ConfigHttpJson.Default.ConfigValueList);
            })))
            .Produces<ConfigValueList>().WithMetadata(new RequiresPermissionAttribute(ConfigDefinition.ValuesReadPermission));
        return endpoints;
    }

    // 平台私有 namespace（PRD 7.8）合并进 IConfiguration 后的配置节名，与 Apollo namespace 骨架的键名约定一致。
    private const string NamespacePrefix = "qian.plugin.";

    private static RouteHandlerBuilder Bind(RouteHandlerBuilder route) => route.RequireAuthorization().WithMetadata(new PlatformHttpContract("qian.config"))
        .Produces<ErrorDetail>(401, ErrorDetailJson.MediaType).Produces<ErrorDetail>(403, ErrorDetailJson.MediaType).Produces<ErrorDetail>(404, ErrorDetailJson.MediaType);

    private static async Task<IResult> Respond(HttpContext http, Func<HttpContext, IPluginContext> source, string plugin, bool values,
        Func<IPluginContext, InstalledComponentMetadata, byte[], IResult> execute)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
                http.User.FindFirst("sub")?.Value != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
            if (!PluginId().IsMatch(plugin)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            var ctx = source(http);
            if (ctx.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            if (values) await AuthorizeAsync(http, ctx, principal, plugin).ConfigureAwait(false);
            // 先判定再查存在：未授权调用方看不到哪些组件已预装。
            var component = ctx.Registry().GetInstalledComponents().SingleOrDefault(item => item.PluginId == plugin)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            var schema = ReadSchema(component) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            return execute(ctx, component, schema);
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
        // 配置、注册表或授权组件尚未就绪、已卸载或未装配时与不存在无法区分。
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException or KeyNotFoundException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }

    // 平台配置（ADR-007：租户级配置不进 Apollo）只对平台主体开放：租户主体在判定前拒绝，不能借平台旁路读取。
    private static async Task AuthorizeAsync(HttpContext http, IPluginContext ctx, Principal principal, string plugin)
    {
        var logger = http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Qian.Config.Http");
        if (principal.TenantId is not null || http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)))
        {
            if (logger is not null) LogDenied(logger, "tenant_principal", principal.Subject, ConfigDefinition.ValuesReadPermission);
            throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        }
        var kind = principal.Kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => "agent" };
        var decision = await ctx.Enforcement().EnforceAsync(new EnforcementRequest(
            new EnforcementSubject(principal.Subject, null, kind, principal.IdentityRevision, principal.Actor is not null),
            ConfigDefinition.ValuesReadPermission, "config/values/" + plugin, "api", AuthzEnforcement.HttpEntryPoint, "GET " + ValuesRoute), http.RequestAborted).ConfigureAwait(false);
        if (decision.Allow) return;
        if (logger is not null) LogDenied(logger, decision.Reason, principal.Subject, ConfigDefinition.ValuesReadPermission);
        // 审计组件已卸载时整个入口不可用，与不存在同为 404（S1-AUD-001）；其余拒绝 403。
        throw new QianErrorException(new QianError(decision.Reason == AuthzEnforcement.AuditUnavailable ? PlatformErrors.NotFound : PlatformErrors.AuthzDenied));
    }

    // 与运行期装配同一路径规则：runtime.configSchema 或缺省 ./schemas/config.schema.json，只读制品快照，不读宿主目录。
    private static byte[]? ReadSchema(InstalledComponentMetadata component)
    {
        var path = "schemas/config.schema.json";
        if (component.Manifest.ValueKind == JsonValueKind.Object && component.Manifest.TryGetProperty("runtime", out var runtime) &&
            runtime.ValueKind == JsonValueKind.Object && runtime.TryGetProperty("configSchema", out var declared))
            path = declared.ValueKind == JsonValueKind.String && declared.GetString() is { } text ? (text.StartsWith("./", StringComparison.Ordinal) ? text[2..] : text) : "";
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal) || path.Split('/').Any(part => part is "" or "." or "..")) return null;
        var bytes = component.ReadFile(path);
        if (bytes is null || bytes.Length is 0 or > SchemaLimit) return null;
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object ? bytes : null;
        }
        catch (JsonException) { return null; }
    }

    // 敏感判定复用发布门禁与 Apollo 快照校验的同一扫描器：先用占位值探测 Schema 标记的敏感路径与疑似密钥键名，
    // 再扫描实际值（疑似密钥值、内嵌凭据）；任一命中的键只给键名。Schema 本身无法解释时全部按敏感处理。
    private static HashSet<string> Sensitive(IReadOnlyDictionary<string, string?> values, JsonElement schema)
    {
        var probe = values.Keys.ToDictionary(key => key, _ => (string?)"probe", StringComparer.OrdinalIgnoreCase);
        var locations = SecretSafety.ScanConfiguration(probe, schema).Concat(SecretSafety.ScanConfiguration(values, schema)).Select(item => item.Location).ToArray();
        if (locations.Contains("#", StringComparer.Ordinal)) return new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase);
        return values.Keys.Where(key =>
        {
            var pointer = "#/" + key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
            return locations.Any(location => location == pointer || location.StartsWith(pointer + "/", StringComparison.Ordinal));
        }).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PluginId();
    [LoggerMessage(EventId = 2121, Level = LogLevel.Warning, Message = "配置值读取拒绝：{Reason}；主体 {Subject}；权限 {Permission}")]
    private static partial void LogDenied(ILogger logger, string reason, string subject, string permission);
}

/// <summary>一个组件在平台配置节 qian.plugin.&lt;组件 id&gt; 下的当前值；条目按键序数排序，敏感键只给键名。</summary>
public sealed record ConfigValueList(string Plugin, string Section, IReadOnlyList<ConfigValueItem> Items)
{
    /// <inheritdoc />
    public override string ToString() => $"ConfigValueList {{ Plugin = {Plugin}, Section = {Section}, Count = {Items.Count} }}";
}

/// <summary>一条配置：Key 为相对配置节的冒号路径；Sensitive 为 true 时 Value 恒为 null。</summary>
public sealed record ConfigValueItem(string Key, bool Sensitive, string? Value)
{
    /// <summary>默认日志不展开配置值。</summary>
    public override string ToString() => $"ConfigValueItem {{ Key = {Key}, Sensitive = {Sensitive} }}";
}

// 线上形状：camelCase 成员，与宿主 JSON 选项无关；保留元数据生成，ASP.NET Core 以异步流写出响应。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ConfigValueList))]
internal sealed partial class ConfigHttpJson : JsonSerializerContext;
