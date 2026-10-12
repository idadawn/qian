using Kun.Authorization;

namespace Qian.Sample.Crm;

/// <summary>参考应用自己的最小只读服务策略：只授予配置中的客户端读取当前租户客户的能力。</summary>
internal sealed class CrmReadPolicy(string clientId) : IPermissionChecker
{
    internal const string Permission = "qian.sample.crm:customer:read";

    public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var allow = request.Permission == Permission && Allowed(request.Subject);
        return ValueTask.FromResult(new PermissionDecision(allow, allow ? "configured-crm-reader" : "not-granted", allow ? ["crm-service-read"] : []));
    }

    public async ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default)
    {
        var results = new List<PermissionDecision>();
        foreach (var request in requests) results.Add(await CheckAsync(request, cancellationToken).ConfigureAwait(false));
        return results;
    }

    public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType,
        string? resourceId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>(Allowed(subject) && resourceType == "customer" ? ["read"] : []);
    }

    public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>(Allowed(subject) ? [Permission] : []);
    }

    public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => CheckAsync(request, cancellationToken);

    private bool Allowed(AuthorizationSubject subject) => subject.Id == clientId && Guid.TryParse(subject.TenantId, out _);
}
