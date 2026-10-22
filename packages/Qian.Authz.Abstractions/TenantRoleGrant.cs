using Qian.Runtime;

namespace Qian.Authz.Abstractions;

/// <summary>供可信开通编排及授权管理 API 使用的最小角色授予端口；不构成 HTTP 授权边界。</summary>
public interface ITenantRoleGrant
{
    /// <summary>在当前租户幂等绑定已存在的内置角色；调用方须完成授权，角色不存在时拒绝。</summary>
    ValueTask AssignAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken cancellationToken = default);
    /// <summary>
    /// 以显式绑定描述授予同一内置角色。Scope 为 null 或当前租户本身时等价于上一重载；S1 的权限点全部租户专属，
    /// 绑定到租户之下的子作用域（应用、Agent 会话）以 AUTHZ_SCOPE_MISMATCH 拒绝、不落库。
    /// 给租户最后一个成员管理绑定加有效期上限视为降级，以 AUTHZ_LAST_ADMIN_LOCKOUT 拒绝。
    /// </summary>
    ValueTask BindAsync(RoleBinding binding, CancellationToken cancellationToken = default);
}

/// <summary>一次角色绑定的描述；Scope 为绑定作用域，null 表示租户本身。</summary>
public sealed record RoleBinding(string RoleCode, string SubjectType, string SubjectId, string Actor, ScopeKey? Scope = null,
    DateTimeOffset? ValidFrom = null, DateTimeOffset? ValidTo = null);

/// <summary>租户角色授予 seam。</summary>
public static class TenantRoleGrant
{
    /// <summary>AuthzPlugin 提供；租户组件只依赖此 Definition。</summary>
    public static readonly ServiceKey Key = new("AuthzTenantRoleGrant");
}
