using Qian.Runtime;
using System.Text.Json.Serialization;

namespace Qian.Tenancy;

/// <summary>租户开通端口；调用方必须先完成平台运营授权。</summary>
public static class TenantProvisioning
{
    /// <summary>租户级开通服务键。</summary>
    public static readonly ServiceKey Key = new("Tenancy.Provisioning");
    /// <summary>Identity 的可选成员开户适配器；缺席时保留 skipped 步骤。</summary>
    public static readonly ServiceKey MembershipKey = new("Tenancy.MembershipProvisioner");
    /// <summary>Notify 的可选开通通知适配器；缺席时保留 skipped 步骤。</summary>
    public static readonly ServiceKey NotificationKey = new("Tenancy.ProvisionedNotifier");
}

/// <summary>可信运营方给出的管理员主体及成员指针；不代表 Identity 已创建该成员。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TenantAdministrator(Guid MembershipId, string SubjectId);

/// <summary>持久化步骤表驱动的租户级开通；同号同请求重试幂等，不负责激活租户。</summary>
public interface ITenantProvisioning
{
    /// <summary>创建主档并运行步骤；同号的不同开户输入返回 CONFLICT。</summary>
    ValueTask<TenantDescriptor> ProvisionAsync(CreateTenant request, TenantAdministrator administrator, string actor, CancellationToken cancellationToken = default);
    /// <summary>从已保存的输入续跑失败或 skipped 的步骤；仅供已授权的可信控制面。</summary>
    ValueTask<TenantDescriptor> RetryAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    /// <summary>读取步骤诊断；必须先完成平台运营授权。</summary>
    ValueTask<IReadOnlyList<TenantProvisionStep>> GetStepsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>步骤状态；Reason 只保存稳定原因码，不保存异常或敏感输入。</summary>
public sealed record TenantProvisionStep(string Step, string Status, int Attempts, string? Reason);

/// <summary>Identity 实现的幂等成员创建端口；idempotencyKey 在重试间不变。</summary>
public interface ITenantMembershipProvisioner
{
    /// <summary>在当前租户创建或确认输入指定的成员；不得把缺席组件当作成功。</summary>
    ValueTask ProvisionAsync(TenantAdministrator administrator, string idempotencyKey, CancellationToken cancellationToken = default);
}

/// <summary>Notify 实现的开通通知端口；须按 idempotencyKey 去重。</summary>
public interface ITenantProvisionedNotifier
{
    /// <summary>通知当前租户已开通。</summary>
    ValueTask NotifyAsync(TenantDescriptor tenant, string idempotencyKey, CancellationToken cancellationToken = default);
}
