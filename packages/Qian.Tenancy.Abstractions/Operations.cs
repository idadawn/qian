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
    /// <summary>目标租户仅由控制面路径给出；复用 UpdateTenant 与 xmin 乐观并发。</summary>
    ValueTask<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken cancellationToken = default);
}
