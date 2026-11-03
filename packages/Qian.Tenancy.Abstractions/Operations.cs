using Qian.Runtime;

namespace Qian.Tenancy;

/// <summary>平台运营服务的独立 seam；调用方必须经平台超管旁路授权。</summary>
public static class TenantOperations
{
    /// <summary>由 TenancyPlugin 提供的管理端口。</summary>
    public static readonly ServiceKey Key = new("TenancyOperations");
}
/// <summary>平台租户列表与主档编辑；拒绝租户上下文，不能直接作为无授权接口。</summary>
public interface ITenantOperations
{
    /// <summary>按租户号排序的分页主档投影。</summary>
    ValueTask<IReadOnlyList<TenantDescriptor>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
    /// <summary>按状态和租户号/名称关键字筛选，返回同一快照下的当前页与总数，按租户号和 ID 稳定排序。</summary>
    ValueTask<(IReadOnlyList<TenantDescriptor> Items, int Total)> QueryAsync(int skip, int take, TenantStatus? status = null, string? keyword = null, CancellationToken cancellationToken = default);
    /// <summary>目标租户仅由控制面路径给出；复用 UpdateTenant 与 xmin 乐观并发。</summary>
    ValueTask<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken cancellationToken = default);
    /// <summary>运营列表：在 QueryAsync 的筛选与排序之上带出有效期、创建时间、用户上限与用量、管理员显示名，并给出各状态计数。</summary>
    ValueTask<TenantOverviewPage> OverviewAsync(int skip, int take, TenantStatus? status = null, string? keyword = null, CancellationToken cancellationToken = default);
}
/// <summary>运营列表的一行：字段与 <see cref="TenantDescriptor"/> 同名同义，另加列表页需要的几列。</summary>
public sealed record TenantOverview(Guid Id, string Code, string Name, TenantIsolation Isolation, TenantStatus Status)
{
    /// <summary>与 TenantDescriptor.Revision 相同，编辑时作 ExpectedRevision。</summary>
    public uint Revision { get; init; }
    /// <summary>主档创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>试用或正式期的截止时间；null 表示不限期。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>用户数上限；null 表示不限。</summary>
    public int? UsersLimit { get; init; }
    /// <summary>当前用户数；身份侧计数端口缺席时为 null，不报 0。</summary>
    public int? UsersUsed { get; init; }
    /// <summary>当前管理员的显示名；没有指针、身份组件缺席或成员已清除时为 null。</summary>
    public string? AdministratorName { get; init; }
}
/// <summary>各状态的租户数：受关键字筛选影响、不受状态筛选影响，供列表页签显示。</summary>
public sealed record TenantStatusCounts(int All, int Pending, int Trial, int Active, int Grace, int Suspended);
/// <summary>运营列表的一页：当前页、筛选后总数与各状态计数读自同一数据库快照。</summary>
public sealed record TenantOverviewPage(IReadOnlyList<TenantOverview> Items, int Total, TenantStatusCounts Counts);
