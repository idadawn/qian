using Kun.Authorization;

namespace Qian.Authz.Abstractions;

/// <summary>平台级角色与旁路的稳定词汇。</summary>
public static class PlatformRoles
{
    /// <summary>平台超管：只经 <see cref="ISuperadminBypass"/> 生效，不进入任何租户的角色、授予或成员视图（ADR-006 D6.11）。</summary>
    public const string Superadmin = "platform.superadmin";
    /// <summary>旁路放行写入审计 payload 的 bypass 值。</summary>
    public const string SuperadminBypass = "superadmin";
}

/// <summary>
/// 平台超管旁路（ADR-006 D6.3「在策略中显式声明并审计」）。调用方以平台主体（Subject.TenantId 为 null）显式请求旁路，普通 Check 对平台主体一律拒绝；
/// 只有持有效 platform.superadmin 的主体可被放行，且放行前先写审计（payload.bypass = superadmin），审计写不成即拒绝。
/// 租户资源在目标租户的上下文中判定并记入该租户的审计链；平台资源在无租户上下文中判定并记入平台审计链。
/// </summary>
public interface ISuperadminBypass
{
    /// <summary>判定一次显式旁路；拒绝时不写审计，原因只给出稳定码，不泄露其它主体或租户的信息。</summary>
    ValueTask<PermissionDecision> AuthorizeAsync(SuperadminAccess access, CancellationToken cancellationToken = default);
}

/// <summary>
/// 一次显式旁路请求。Permission 须是已登记的权限点；TenantId 为目标租户（平台资源为 null），须与当前租户上下文一致；
/// Resource 为可选资源引用（写入审计）；Channel 为调用通道（ui / api / mcp / a2a / job / event / hook）。
/// </summary>
public sealed record SuperadminAccess(AuthorizationSubject Subject, string Permission, string? TenantId, string? Resource, string Channel);
