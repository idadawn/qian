using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.IdentityModel.Tokens;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Lifecycle;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Sample.Crm;
using Qian.Surfaces;
using Qian.Tenancy;

namespace Qian.Dev.TenantOps;

// 运营宿主的前置与启动，照集成测试 TenantManagementTests 的 ManagementHost：Authz / Audit 换成带专用平台登录的条目并登记
// qian.tenancy 与 qian.registry 的权限目录，引导一个平台超管，经 Lifecycle 安装并激活全部 system 组件制品，再启动 TenantOperationsHost。
internal static partial class DevOperations
{
    internal static async Task<WebApplication> StartAsync(PluginLoader loader, IServiceProvider services, DevPlatform platform, RsaSecurityKey key,
        string repository, string url, Action<ILoggingBuilder> logging, CancellationToken ct)
    {
        var tenancy = Catalog(repository, "qian.tenancy", roles: false); var registry = Catalog(repository, "qian.registry", roles: true);
        await loader.UnmountAsync("authz", ScopeKey.Global, ct).ConfigureAwait(false); await loader.UnmountAsync("audit", ScopeKey.Global, ct).ConfigureAwait(false);
        await loader.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { chain = new { platformRole = DevDatabase.AuditPlatformRole } })),
                new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = DevDatabase.AuthzPlatformRole })),
                new AuthzPlugin([CrmPermissions.Compile(), tenancy, registry]), [AuthzDefinition.Key, CommandPolicies.AuthorizationKey])], ct).ConfigureAwait(false);
        DevPlatform.EnsureReady(loader, "audit"); DevPlatform.EnsureReady(loader, "authz");
        // 平台超管只经内部平台角色存储授予（S1-AZ-005 的管理 API 交付前，平台运营流程与测试引导都走这里），且必须在无租户上下文中。
        await new PostgresPlatformRoleStore(services.GetRequiredService<IDataService>(), services.GetRequiredService<ITenantContext>(), DevDatabase.AuthzPlatformRole)
            .AssignSuperadminAsync(DevKeys.Superadmin, "dev-bootstrap", ct).ConfigureAwait(false);
        var lifecycle = loader.GetContext("lifecycle", ScopeKey.Global).Lifecycle();
        foreach (var artifact in platform.SystemArtifacts.Values)
        {
            var revision = await lifecycle.InstallAsync(artifact, DevKeys.Superadmin, ct).ConfigureAwait(false);
            await lifecycle.ActivateAsync(revision.PluginId, revision.Revision, DevKeys.Superadmin, ct).ConfigureAwait(false);
        }
        var app = TenantOperationsHost.Build(() => loader.GetContext("tenancy", ScopeKey.Global), new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = DevKeys.Issuer, ValidateAudience = true, ValidAudience = DevKeys.OperationsAudience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = DevKeys.PublicOnly(key),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero
        }, builder => { builder.WebHost.UseUrls(url); logging(builder.Logging); });
        MapDevelopmentRoutes(app, () => loader.GetContext("tenancy", ScopeKey.Global));
        await app.StartAsync(ct).ConfigureAwait(false);
        return app;
    }

    private static PermissionCatalog Catalog(string repository, string component, bool roles)
    {
        var permissions = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(repository, $"plugins/{component}/metadata/permissions.json")));
        var declared = roles ? JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(repository, $"plugins/{component}/metadata/roles.json")))
            : JsonSerializer.SerializeToElement(Array.Empty<object>());
        return new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(component, "1.0.0", permissions, declared);
    }

    // 开发路由：产品契约里没有对应端点，只在本机手测宿主上存在，前缀 /__dev/ 以示区别。
    // 授权与运营端点同一条路：已验签的平台用户主体（无 tenant_id、无委托）→ 平台超管显式旁路（写 bypass=superadmin 审计）。
    private static void MapDevelopmentRoutes(WebApplication app, Func<IPluginContext> source)
    {
        // 开通步骤表：ITenantProvisioning.GetStepsAsync 是只接受无租户上下文的内部端口，TEN-005 页面之前用它看每步状态。
        app.MapGet("/__dev/tenancy/tenants/{id:guid}/provision-steps", (HttpContext http, Guid id) => Authorized(http, source, "read", id, async ctx =>
        {
            _ = await ctx.Tenancy().GetByIdAsync(id, http.RequestAborted).ConfigureAwait(false) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            return Results.Ok(new { items = await ctx.Inject<ITenantProvisioning>(TenantProvisioning.Key).GetStepsAsync(id, http.RequestAborted).ConfigureAwait(false) });
        })).RequireAuthorization();
        // 到期扫描：qian.tenancy/tenant.scan-expired 只收 global qian.tenancy 或 qian.scheduler 的可信调用。
        // 调度器准点触发归 SCH-002；这里由超管手动代调度器以 global qian.tenancy 身份调用一次。
        app.MapPost("/__dev/tenancy/scan-expired", (HttpContext http, int? limit) => Authorized(http, source, "scan", null, async ctx =>
        {
            var scan = ctx.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, "qian.tenancy/tenant.scan-expired");
            var input = JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), limit = limit ?? 100 });
            return Results.Json(await scan.InvokeAsync(ctx, input, http.RequestAborted).ConfigureAwait(false));
        })).RequireAuthorization();
    }

    private static async Task<IResult> Authorized(HttpContext http, Func<IPluginContext> source, string action, Guid? id, Func<IPluginContext, Task<IResult>> execute)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            // 主体检查与 TenancyEndpoints 的运营端点逐条相同。
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
                http.User.FindFirst("sub")?.Value != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
            if (principal.TenantId is not null || principal.Actor is not null || principal.Kind != PrincipalKind.User ||
                http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)) ||
                http.RequestServices.GetRequiredService<ITenantContext>().TenantId is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            var ctx = source(); var permission = "qian.tenancy:tenant:" + action;
            var decision = await ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey).AuthorizeAsync(new SuperadminAccess(
                new AuthorizationSubject(principal.Subject, null, "user"), permission, null, id is null ? "tenancy/tenants" : "tenancy/tenants/" + id, "api"),
                http.RequestAborted).ConfigureAwait(false);
            if (!decision.Allow)
            {
                LogDenied(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Dev.TenantOps"), decision.Reason, principal.Subject, permission);
                throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            }
            return await execute(ctx).ConfigureAwait(false);
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "开发路由授权拒绝：{Reason}；主体 {Subject}；权限 {Permission}")]
    private static partial void LogDenied(ILogger logger, string? reason, string subject, string permission);
}
