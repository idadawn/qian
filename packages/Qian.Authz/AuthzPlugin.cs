using System.Linq.Expressions;
using System.Text.Json;
using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz;

/// <summary>
/// Authz Provider 的组件入口（global scope）：提供 ctx.Authz 判定服务（真实 RBAC，宿主装配 Kun.Caching 时缓存主体快照）、活动权限登记表与
/// qian.authz.resource-type/v1 扩展点，以及平台超管显式旁路（<see cref="ISuperadminBypass"/>）：未配置 platformRole 时旁路端口一律拒绝。
/// 数据与字段权限编译属 S2，本阶段两个端口一律 fail-closed。
/// </summary>
public sealed class AuthzPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];

    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object) throw Invalid();
        string? role = null; var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw Invalid();
            // advancedMode 是 profile 既有字段；S1 没有高级模式（数据 / 字段权限编译属 S2），只接受 false，不假装支持。
            if (property.Name == "advancedMode" && property.Value.ValueKind == JsonValueKind.False) continue;
            if (property.Name != "platformRole" || property.Value.ValueKind != JsonValueKind.String) throw Invalid();
            role = property.Value.GetString()!; AuthzPlatformAccess.ValidateRoleName(role);
        }
        var services = ctx.Services; var tenant = services.GetRequiredService<ITenantContext>();
        // 宿主已有登记表时共用同一实例（命令 PEP、注册表管理面经宿主服务读取），否则由本组件持有。
        var registry = services.GetService<IPermissionRegistry>() ?? new PermissionRegistry();
        var checker = new RbacPermissionChecker(ctx.Data(), tenant, registry, services.GetService<IAuthzExecutionConstraints>(), services.GetService<IKunCache>());
        ctx.Effect(AuthzErrors.Register(services.GetRequiredService<ErrorCatalog>()));
        PostgresPlatformRoleStore? platform = null;
        if (role is not null)
        {
            platform = new PostgresPlatformRoleStore(ctx.Data(), tenant, role);
            await platform.ValidateAsync(ct).ConfigureAwait(false);
        }
        ctx.Provide<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey, new SuperadminBypassService(ctx.Data(), tenant, registry, platform, ctx));
        ctx.Provide(AuthzDefinition.PermissionsKey, registry);
        ctx.Provide<IResourceTypeRegistry>(AuthzDefinition.ResourceTypesKey, registry);
        ctx.Provide<IAuthzService>(AuthzDefinition.Key, new AuthzService(checker));
    }

    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

// ctx.Authz 的实现：五个判定方法直接委托真实 RBAC；数据与字段端口在 S2 编译器交付前恒拒绝，而不是放行全部。
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
