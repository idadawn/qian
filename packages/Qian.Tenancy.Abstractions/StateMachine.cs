using System.Text.Json.Serialization;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

/// <summary>状态机 seam 与进程内审批事件；不属于 hooks.json 桥接矩阵。</summary>
public static class TenantLifecycle
{
    /// <summary>真实状态迁移与扫描端口。</summary>
    public static readonly ServiceKey Key = new("Tenancy.Lifecycle");
    /// <summary>顺序审批；任一 false 拒绝，无监听器放行。</summary>
    public static readonly QianEvent<TenantTransitionApproval, IReadOnlyList<bool>> Approving = QianEvent.Define<TenantTransitionApproval, IReadOnlyList<bool>>("tenant/transition-approving", DispatchMode.Serial);
}
/// <summary>控制面已鉴权后的状态操作。</summary>
public interface ITenantLifecycle
{
    /// <summary>激活为 trial 或 active；相同目标重复调用无副作用。</summary>
    ValueTask<TenantDescriptor> ActivateAsync(Guid id, ActivateTenant request, string actor, CancellationToken cancellationToken = default);
    /// <summary>从任意 S1 状态冻结，保留原状态；重复冻结无副作用。</summary>
    ValueTask<TenantDescriptor> SuspendAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    /// <summary>恢复到冻结前状态；重复恢复无副作用。</summary>
    ValueTask<TenantDescriptor> ResumeAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    /// <summary>幂等扫描一批到期租户；供调度器 callback 调用，不代表已配置调度。</summary>
    ValueTask<int> ScanExpiredAsync(int limit, string actor, CancellationToken cancellationToken = default);
}
/// <summary>激活输入，目标租户来自路径。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActivateTenant(bool Trial = false, DateTimeOffset? ValidUntil = null);
/// <summary>不可变审批请求；监听器不得另开同一租户迁移。</summary>
public sealed record TenantTransitionApproval(Guid Id, TenantStatus From, TenantStatus To, string Action, string Actor);
/// <summary>组件状态错误；非法迁移仍使用冻结 ErrorDetail。</summary>
public static class TenancyErrors
{
    /// <summary>状态不支持该迁移。</summary>
    public static readonly ErrorCode StateConflict = ErrorCode.Business("TENANCY_STATE_CONFLICT", 409, "qian.tenancy",
        new Dictionary<string, string> { ["zh-CN"] = "租户状态冲突", ["en"] = "Tenant state conflict" },
        new Dictionary<string, string> { ["zh-CN"] = "当前租户状态不允许该操作。", ["en"] = "The tenant state does not allow this operation." });
}
