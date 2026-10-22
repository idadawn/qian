using System.Linq.Expressions;
using System.Text.Json;
using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Authz;

/// <summary>
/// Authz Provider 的组件入口（global scope）：提供 ctx.Authz 判定服务（真实 RBAC，宿主装配 Kun.Caching 时缓存主体快照）、活动权限登记表与
/// qian.authz.resource-type/v1 扩展点、平台超管显式旁路（<see cref="ISuperadminBypass"/>：未配置 platformRole 时旁路端口一律拒绝），
/// 以及三处 PEP 共用的执行端口（<see cref="IAuthzEnforcement"/>：判定、identity_revision 比对、决策审计）。
/// Apply 时持久化并登记组件的权限目录、注册命令 PEP，并监听 tenant/provisioned 为新租户实例化内置角色与各组件的默认策略（组件规格 §6.3）。
/// 宿主须经 AddKunEventBus 登记 authz.grant.changed.v1 / authz.role.changed.v1（<see cref="AuthzEventRegistration.RegisterAuthzEvents"/>），否则 Apply 失败。
/// 数据与字段权限编译属 S2，本阶段两个端口一律 fail-closed。
/// </summary>
public sealed class AuthzPlugin : IQianPlugin
{
    private const string Actor = "qian.authz";
    private static readonly JsonElement Nothing = JsonDocument.Parse("null").RootElement.Clone();
    private readonly IReadOnlyList<PermissionCatalog> _catalogs;
    private readonly bool _installed;

    /// <summary>不预置目录：组件经 <c>AuthzPermissions</c> 自行登记活动声明。</summary>
    public AuthzPlugin() : this([], false) { }
    /// <summary>独立宿主：目录由宿主从 PermissionAttribute 或随包 metadata 编译后传入（ADR-019 D19.4）。</summary>
    public AuthzPlugin(IReadOnlyList<PermissionCatalog> catalogs) : this(catalogs, false) { }
    private AuthzPlugin(IReadOnlyList<PermissionCatalog> catalogs, bool installed)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        if (catalogs.Any(catalog => catalog is null) || catalogs.Select(catalog => catalog.PluginId).Distinct(StringComparer.Ordinal).Count() != catalogs.Count)
            throw new ArgumentException("每个组件只能有一份权限目录。", nameof(catalogs));
        _catalogs = Array.AsReadOnly(catalogs.ToArray()); _installed = installed;
    }
    /// <summary>平台宿主：Apply 时从 ctx.Registry 的预装组件读取 metadata/permissions.json 与 roles.json（PRD 7.5）；宿主须登记 ISurfaceReader。</summary>
    public static AuthzPlugin FromInstalledComponents() => new([], true);

    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => _installed ? [DataDefinition.Key, RegistryDefinition.Key] : [DataDefinition.Key];

    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object) throw Invalid();
        string? role = null; var sampling = 1d; var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw Invalid();
            // advancedMode 是 profile 既有字段；S1 没有高级模式（数据 / 字段权限编译属 S2），只接受 false，不假装支持。
            if (property.Name == "advancedMode" && property.Value.ValueKind == JsonValueKind.False) continue;
            // decisionLogSampling：放行判定写入决策审计的比例（0–1，缺省 1 全记）；拒绝不受采样影响、必记。
            if (property.Name == "decisionLogSampling")
            {
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out sampling) || double.IsNaN(sampling) || sampling is < 0 or > 1) throw Invalid();
                continue;
            }
            if (property.Name != "platformRole" || property.Value.ValueKind != JsonValueKind.String) throw Invalid();
            role = property.Value.GetString()!; AuthzPlatformAccess.ValidateRoleName(role);
        }
        var services = ctx.Services; var tenant = services.GetRequiredService<ITenantContext>();
        // 授权变更事件随角色写入同事务进 Outbox；宿主漏登记会让每次写入失败，所以在装载时就拒绝。
        var events = services.GetService<IEventTypeRegistry>();
        if (events is null || events.GetEventType(typeof(AuthzGrantChangedEvent)) != AuthzEventIds.GrantChanged || events.GetEventType(typeof(AuthzRoleChangedEvent)) != AuthzEventIds.RoleChanged)
            throw new InvalidOperationException("宿主未登记授权事件：AddKunEventBus(registrar => registrar.RegisterAuthzEvents()) 必须在装载 qian.authz 前完成。");
        // 宿主已有登记表时共用同一实例（命令 PEP、注册表管理面经宿主服务读取），否则由本组件持有。
        var registry = services.GetService<IPermissionRegistry>() ?? new PermissionRegistry();
        var cache = services.GetService<IKunCache>();
        var checker = new RbacPermissionChecker(ctx.Data(), tenant, registry, services.GetService<IAuthzExecutionConstraints>(), cache);
        ctx.Effect(AuthzErrors.Register(services.GetRequiredService<ErrorCatalog>()));
        PostgresPlatformRoleStore? platform = null;
        if (role is not null)
        {
            platform = new PostgresPlatformRoleStore(ctx.Data(), tenant, role);
            await platform.ValidateAsync(ct).ConfigureAwait(false);
        }
        // 目录按组件版本不可变（同版本内容不同返回 CONFLICT，Apply 失败）；活动登记随本组件卸载撤销，持久目录与租户授予保留。
        var catalogs = _installed ? Installed(ctx) : _catalogs;
        var store = new PostgresPermissionCatalogStore(ctx.Data());
        foreach (var catalog in catalogs)
        {
            await store.PutAsync(catalog, ct).ConfigureAwait(false);
            ctx.Effect(registry.Register(catalog.Version, catalog.Permissions));
        }
        var roles = RoleStore(ctx, tenant);
        ctx.Provide<ITenantRoleGrant>(TenantRoleGrant.Key, new TenantRoleGrantService(roles));
        var components = Array.AsReadOnly(catalogs.Select(catalog => (catalog.PluginId, catalog.Version)).ToArray());
        ctx.On(SurfaceHookEvents.TenantProvisioned, DispatchMode.Serial, (Func<HookNotification, ValueTask<JsonElement>>)(notification => ProvisionAsync(notification)), priority: 1000);
        var bypass = new SuperadminBypassService(ctx.Data(), tenant, registry, platform, ctx);
        // 三处 PEP 共用的执行核心：HTTP / gRPC 适配器经 AuthzEnforcement.Key 取用，命令 PEP 在此直接接线；判定仍只来自 checker 与显式旁路。
        var enforcement = new AuthorizationEnforcer(ctx, checker, bypass, tenant, new IdentityRevisionGuard(ctx.Data(), tenant), sampling);
        CommandPolicies.Register(ctx, CommandPolicyKind.Authorization, new RbacCommandPolicy(enforcement).Handle);
        ctx.Provide<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey, bypass);
        ctx.Provide<IAuthzEnforcement>(AuthzEnforcement.Key, enforcement);
        ctx.Provide(AuthzDefinition.PermissionsKey, registry);
        ctx.Provide<IResourceTypeRegistry>(AuthzDefinition.ResourceTypesKey, registry);
        ctx.Provide<IAuthzService>(AuthzDefinition.Key, new AuthzService(checker));

        // 只接受 qian.tenancy 在目标租户 scope 派发、主体即该租户的通知（与 hooks 桥接同一校验）；在该租户上下文中一个事务完成内置角色与默认策略。
        async ValueTask<JsonElement> ProvisionAsync(HookNotification notification)
        {
            var source = notification.Source;
            if (source.Entry.Plugin != "qian.tenancy" || source.Scope.Kind != "tenant" || notification.Subject != source.Scope.Id) throw Invalid();
            using var scope = tenant.BeginScope(notification.Subject);
            await roles.ProvisionAsync(components, Actor, notification.CancellationToken).ConfigureAwait(false);
            return Nothing;
        }
    }

    // 角色存储与授权变更后的身份修订传播（S1-DEC-027）只在这里组装：组件入口与验收用例取同一个组合。
    // 身份组件与本组件同进程时，授予变更提交后经 ctx 解析的 IIdentityAccounts 递增修订；不在本进程即跳过。
    internal static PostgresTenantRoleStore RoleStore(IPluginContext ctx, ITenantContext tenant) =>
        new(ctx.Data(), tenant, new IdentityRevisionPropagation(ctx, ctx.Data()));

    private static PermissionCatalog[] Installed(IPluginContext ctx)
    {
        var compiler = new PermissionCatalogCompiler(ctx.Services.GetRequiredService<ISurfaceReader>());
        return ctx.Registry().GetInstalledComponents()
            .Where(component => component.Metadata.Keys.Any(path => path.Split('/')[^1] == "permissions.json"))
            .Select(compiler.FromInstalled).ToArray();
    }

    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

// ctx.Authz 的实现：Kun 的五个判定方法、有效权限列表与完整解释直接委托真实 RBAC；数据与字段端口在 S2 编译器交付前恒拒绝，而不是放行全部。
internal sealed class AuthzService(RbacPermissionChecker checker) : IAuthzService
{
    public IDataPermissionFilter DataPermissions { get; } = new DenyAllDataPermissions();
    public IFieldPolicy Fields { get; } = new DenyAllFields();
    public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default) => checker.CheckAsync(request, cancellationToken);
    public ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default) =>
        checker.BatchCheckAsync(requests, cancellationToken);
    public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default) =>
        checker.ListAllowedActionsAsync(subject, resourceType, resourceId, cancellationToken);
    public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) =>
        checker.ListPermissionsAsync(subject, cancellationToken);
    public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => checker.ExplainAsync(request, cancellationToken);
    public ValueTask<IReadOnlyList<EffectiveGrant>> ListEffectivePermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) =>
        checker.ListEffectivePermissionsAsync(subject, cancellationToken);
    public ValueTask<AuthorizationExplanation> ExplainDetailedAsync(PermissionRequest request, CancellationToken cancellationToken = default) =>
        checker.ExplainDetailedAsync(request, cancellationToken);

    private sealed class DenyAllDataPermissions : IDataPermissionFilter
    {
        public ValueTask<Expression<Func<T, bool>>> GetExpressionAsync<T>(PermissionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Expression<Func<T, bool>>>(_ => false);
        // 谓词 AST 还没有「恒假」的约定词汇，返回任何谓词都可能被编译器当作放宽；直接拒绝。
        public ValueTask<DataPermissionPredicate> GetPredicateAsync(PermissionRequest request, CancellationToken cancellationToken = default) =>
            throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
    }

    private sealed class DenyAllFields : IFieldPolicy
    {
        public ValueTask<FieldDecision> EvaluateAsync(PermissionRequest request, IReadOnlyList<string> fields, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new FieldDecision([], [], []));
    }
}
