using Qian.Runtime;

namespace Qian.Tenancy;

/// <summary>本租户摘要的 seam（S1-TEN-021）：租户成员读取所在租户的主档、有效期、用户用量与管理员。</summary>
public static class TenantSummaries
{
    /// <summary>由 TenancyPlugin 提供的只读端口。</summary>
    public static readonly ServiceKey Key = new("TenancySummary");
}
/// <summary>只读当前租户上下文所指的租户：没有租户上下文即未认证，不能按参数读其它租户；平台读租户走运营端口（超管旁路与审计）。</summary>
public interface ITenantSummaries
{
    /// <summary>当前租户的摘要；租户主档不存在时 NOT_FOUND。</summary>
    ValueTask<TenantSummary> CurrentAsync(CancellationToken cancellationToken = default);
}
/// <summary>本租户摘要：字段与 <see cref="TenantOverview"/> 同名同义，只留外壳首页需要的几列（不含隔离策略与并发版本）。</summary>
public sealed record TenantSummary(Guid Id, string Code, string Name, TenantStatus Status)
{
    /// <summary>试用或正式期的截止时间；null 表示不限期。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>用户数上限；null 表示不限。</summary>
    public int? UsersLimit { get; init; }
    /// <summary>当前用户数；身份侧计数端口缺席时为 null，不报 0。</summary>
    public int? UsersUsed { get; init; }
    /// <summary>当前管理员的显示名；没有指针、身份组件缺席或成员已清除时为 null。</summary>
    public string? AdministratorName { get; init; }
}
