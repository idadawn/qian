using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz;

/// <summary>消费实际授权存储和活动声明的 RBAC 判定端口；主体须由已认证宿主提供，完整策略组合由 Authz Provider 接入。</summary>
public sealed class RbacPermissionChecker(IDataService data, ITenantContext tenant, IPermissionRegistry registry, IAuthzExecutionConstraints? constraints = null) : IPermissionChecker
{
    private readonly PostgresRbacEvaluator _evaluator = new(data, tenant, registry, constraints);
    /// <inheritdoc />
    public async ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (await _evaluator.EvaluateAsync(request, cancellationToken).ConfigureAwait(false)).Decision;
    }
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<PermissionDecision>(requests.Count);
        foreach (var request in requests) results.Add(await CheckAsync(request, cancellationToken).ConfigureAwait(false));
        return results.AsReadOnly();
    }
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var prefix = resourceType + ":"; var actions = new List<string>();
        foreach (var code in await _evaluator.ListPermissionsAsync(subject, cancellationToken).ConfigureAwait(false))
            if (code.StartsWith(prefix, StringComparison.Ordinal) && (await CheckAsync(new PermissionRequest(subject, code, resourceId), cancellationToken).ConfigureAwait(false)).Allow)
                actions.Add(code[prefix.Length..]);
        return actions.AsReadOnly();
    }
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject); return _evaluator.ListPermissionsAsync(subject, cancellationToken);
    }
    /// <inheritdoc />
    public async ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default)
        => (await ExplainDetailedAsync(request, cancellationToken).ConfigureAwait(false)).Decision;
    /// <summary>读取真实命中角色、授权记录和缺失权限。</summary>
    public ValueTask<AuthorizationExplanation> ExplainDetailedAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); return _evaluator.EvaluateAsync(request, cancellationToken);
    }
}
