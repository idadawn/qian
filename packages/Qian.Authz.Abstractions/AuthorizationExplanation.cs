using Kun.Authorization;

namespace Qian.Authz.Abstractions;

/// <summary>可解释判定及命中链路；拒绝时不包含其它租户的数据。</summary>
public sealed class AuthorizationExplanation
{
    /// <summary>复制判定与链路快照。</summary>
    public AuthorizationExplanation(PermissionDecision decision, IReadOnlyList<string> matchedRoles, IReadOnlyList<string> matchedGrants, IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(decision); ArgumentNullException.ThrowIfNull(matchedRoles); ArgumentNullException.ThrowIfNull(matchedGrants); ArgumentNullException.ThrowIfNull(missing);
        Decision = decision; MatchedRoles = Array.AsReadOnly(matchedRoles.ToArray()); MatchedGrants = Array.AsReadOnly(matchedGrants.ToArray()); Missing = Array.AsReadOnly(missing.ToArray());
    }
    /// <summary>与 Kun 判定端口一致的结果。</summary>
    public PermissionDecision Decision { get; }
    /// <summary>命中的角色稳定编码。</summary>
    public IReadOnlyList<string> MatchedRoles { get; }
    /// <summary>实际命中的授权记录定位。</summary>
    public IReadOnlyList<string> MatchedGrants { get; }
    /// <summary>缺少的权限点；无跨租户对象明细。</summary>
    public IReadOnlyList<string> Missing { get; }
}
