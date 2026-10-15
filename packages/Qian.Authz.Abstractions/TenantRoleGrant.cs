using Qian.Runtime;

namespace Qian.Authz.Abstractions;

/// <summary>供可信开通编排及授权管理 API 使用的最小角色授予端口；不构成 HTTP 授权边界。</summary>
public interface ITenantRoleGrant
{
    /// <summary>在当前租户幂等绑定已存在的内置角色；调用方须完成授权，角色不存在时拒绝。</summary>
    ValueTask AssignAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken cancellationToken = default);
}

/// <summary>租户角色授予 seam。</summary>
public static class TenantRoleGrant
{
    /// <summary>AuthzPlugin 提供；租户组件只依赖此 Definition。</summary>
    public static readonly ServiceKey Key = new("Authz.TenantRoleGrant");
}
