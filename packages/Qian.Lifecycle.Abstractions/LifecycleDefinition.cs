using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Lifecycle;

/// <summary>平台生命周期控制器 seam（PRD 7.2.1，服务键 ctx.Lifecycle）。</summary>
public static class LifecycleDefinition
{
    /// <summary>平台生命周期控制器服务键。</summary>
    public static readonly ServiceKey Key = new("Lifecycle");
    /// <summary>管理面读取装配诊断的权限点：GET /api/lifecycle/diagnostics；无该权限者与不存在同为 404（R32）。</summary>
    public const string DiagnosticsReadPermission = "qian.lifecycle:diagnostics:read";
    /// <summary>
    /// 管理面批准与读取组件反向权限的权限点：PUT / GET /api/lifecycle/tenants/{tenant}/plugins/{plugin}/versions/{version}/permissions；
    /// 无该权限者与不存在同为 404（R32）。平台运营代批经 Authz 的超管显式旁路并审计（S1 的批准形态，plugin-manifest T5）。
    /// </summary>
    public const string InstallApprovePermission = "qian.lifecycle:install:approve";
    /// <summary>取得已挂载的生命周期控制器。</summary>
    public static IPluginLifecycleController Lifecycle(this IPluginContext context) => context.Inject<IPluginLifecycleController>(Key);
}

/// <summary>
/// 平台级制品修订与租户级安装状态机（PRD 7.2.1）：平台级 Install / Activate / Rollback / Deprecate / Retire，租户级 Entitle / Provision / Upgrade /
/// Uninstall / Purge，Mount / Unmount 写租户 scope patch（取代 Enable / Disable）。写操作都是平台管理动作，由可信调用方（运营工具、宿主管理面）
/// 负责鉴权；actor 写入记录的操作者字段，集成事件的 actor 取调用方打开的事件作用域。租户级方法由控制器自己进入该租户的数据上下文。
/// </summary>
public interface IPluginLifecycleController
{
    /// <summary>
    /// 安装制品修订：先以信任根（Apollo qian.common.trust 引用的 K8s Secret 公钥）验 Ed25519 分离签名，未签名或签名不符直接失败；再经 Registry 登记
    /// （核验完整字节摘要与静态校验）。module 须已随宿主镜像预装同一摘要；service 经部署执行器提交期望状态并记录健康；同一摘要重复安装返回已有修订。
    /// </summary>
    ValueTask<PluginRevisionView> InstallAsync(PluginArtifact artifact, string actor, CancellationToken cancellationToken = default);
    /// <summary>激活修订：原 Active 修订回到 Installed；service 修订须健康；发出 lifecycle.plugin.activated.v1。</summary>
    ValueTask<PluginRevisionView> ActivateAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default);
    /// <summary>回切到更早的未退役修订（平台级）：与激活相同的检查与事件，目标修订号必须小于当前 Active。</summary>
    ValueTask<PluginRevisionView> RollbackAsync(string pluginId, int toRevision, string actor, CancellationToken cancellationToken = default);
    /// <summary>弃用非 Active 修订：不再接受新的开通，已开通租户不受影响。</summary>
    ValueTask<PluginRevisionView> DeprecateAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default);
    /// <summary>退役非 Active 修订：此后不能激活、回切、开通或升级到它。</summary>
    ValueTask<PluginRevisionView> RetireAsync(string pluginId, int revision, string actor, CancellationToken cancellationToken = default);
    /// <summary>重新观测 service 修订的部署健康（工作负载状态 + 组件 /health 双条件）并回写。</summary>
    ValueTask<PluginRevisionView> RefreshDeploymentAsync(string pluginId, int revision, CancellationToken cancellationToken = default);
    /// <summary>某组件的全部修订，按修订号排序。</summary>
    ValueTask<IReadOnlyList<PluginRevisionView>> GetRevisionsAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>记录租户已获授权（Entitled）；qian.entitlement 的授予经此进入状态机。policy.required 的平台插件恒视为已授权。</summary>
    ValueTask<TenantPluginInstallView> EntitleAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default);
    /// <summary>
    /// 开通当前 Active 修订：Entitled → Provisioning → 执行 tenant-provision 钩子 → Installed。钩子以 (tenant, plugin, hook, toVersion) 为幂等键记入
    /// lifecycle_runs，成功后不再执行；已 Installed 时直接返回、无副作用（换版本用 <see cref="UpgradeAsync"/>）；钩子失败时停在 Provisioning，
    /// 重试沿用原目标版本。开通不挂载，挂载用 <see cref="MountAsync"/>。权益之后还有第二道门：修订声明了反向权限（policy.permissions / policy.egress）
    /// 且不是 policy.required 的平台组件时，每一项都须已由 <see cref="ApprovePermissionsAsync"/> 批准，否则 LIFECYCLE_PERMISSIONS_UNAPPROVED 且不写任何记录。
    /// </summary>
    ValueTask<TenantPluginInstallView> ProvisionAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default);
    /// <summary>
    /// 升级或租户级回切：Installed(from) → Upgrading → tenant-upgrade 钩子（由目标版本实现）→ 把本租户 scope 中引用 from 的条目改写为 to 并合入 → Installed(to)。
    /// 钩子失败或新版本挂载失败时条目改回 from、状态回到 Installed(from) 并报告原因（挂载失败的原因同时进入诊断）；成功发出
    /// lifecycle.tenant-plugin.upgraded.v1。目标修订不能已退役，service 修订须健康；数据迁移须前向兼容（扩展 → 迁移 → 收缩）。
    /// 反向权限同开通的第二道门：目标版本没有自己的批准记录时沿用该租户对该组件最近一次的批准，新增任一项即须重批（D7「升级新增即重批」），缺失项写进错误。
    /// </summary>
    ValueTask<TenantPluginInstallView> UpgradeAsync(string tenantId, string pluginId, string fromVersion, string toVersion, string actor, CancellationToken cancellationToken = default);
    /// <summary>卸载：tenant-uninstall 钩子 → 撤下本租户 scope 中引用该组件的条目 → Uninstalled；业务数据保留。</summary>
    ValueTask<TenantPluginInstallView> UninstallAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default);
    /// <summary>清除：只接受 Uninstalled；tenant-purge 钩子删除组件的租户数据 → Purged。</summary>
    ValueTask<TenantPluginInstallView> PurgeAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default);
    /// <summary>租户对某组件的安装记录；没有时返回 null。</summary>
    ValueTask<TenantPluginInstallView?> GetInstallAsync(string tenantId, string pluginId, CancellationToken cancellationToken = default);
    /// <summary>租户对某组件的钩子执行记录（lifecycle_runs），按开始时间排序。</summary>
    ValueTask<IReadOnlyList<LifecycleRunView>> GetRunsAsync(string tenantId, string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 挂载：经 Registry 把 patch/v2 条目写入租户 scope（同事务发 registry.scope.mounted.v1）并立即合入。带 plugin 的条目要求该组件已开通且版本与安装一致
    /// （policy.required 的平台组件除外）；声明契约索引的组件版本经平台契约准入（R76）。装配失败时结果为 Failed：条目恢复为挂载前的 patch，
    /// 原因码持久记入诊断（<see cref="GetDiagnosticsAsync"/>），同一条目之后挂载成功或卸载时清除。
    /// </summary>
    ValueTask<MountResult> MountAsync(ScopeKey scope, JsonElement entry, string actor, CancellationToken cancellationToken = default);
    /// <summary>卸载：经 Registry 写禁用 patch 或撤销本 scope 引入的 patch（同事务发 registry.scope.unmounted.v1）并立即合入；不改安装状态，不改数据库开关。</summary>
    ValueTask<MountResult> UnmountAsync(ScopeKey scope, string entryId, string actor, CancellationToken cancellationToken = default);
    /// <summary>
    /// 管理面装配诊断：调用者须是该租户已验证的用户或服务主体（无委托），并经宿主权限判定端口持有 <see cref="LifecycleDefinition.DiagnosticsReadPermission"/>；
    /// 否则与不存在同为 NOT_FOUND。返回 Registry 合入该 scope 时的当前失败，以及经 Lifecycle 挂载或升级失败、已撤回的条目（组件修订与原因码）。
    /// </summary>
    ValueTask<IReadOnlyList<LifecycleDiagnosticView>> GetDiagnosticsAsync(ScopeKey scope, ICurrentUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 管理面批准某组件版本的反向权限（S1 只有 API，S2 才有 UI）：调用者须是该租户已验证的用户或服务主体（无委托）并持
    /// <see cref="LifecycleDefinition.InstallApprovePermission"/>，或是经 Authz 超管显式旁路并审计的平台主体（运营代批）；否则与不存在同为 NOT_FOUND。
    /// 参数里的目标租户须等于宿主已建立的租户上下文（平台运营宿主先选定已验证的目标租户），否则 NOT_FOUND。grants 逐项列出所批准的内容，
    /// 只能是该版本清单声明过的项（否则 INPUT_INVALID，pointer 指向该项）；
    /// 未安装的版本为 NOT_FOUND；grants 为 null（请求体缺失或读不成）同样在判定之后以 INPUT_INVALID 拒绝。整份替换该版本的批准记录，内容不变时不写入（幂等）；
    /// 批准人取已验证主体，不由调用方指定。
    /// </summary>
    ValueTask<PermissionApprovalView> ApprovePermissionsAsync(string tenantId, string pluginId, string version, PermissionGrants? grants, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>管理面读取某组件版本的批准记录；判定同 <see cref="ApprovePermissionsAsync"/>，没有记录时返回 null。</summary>
    ValueTask<PermissionApprovalView?> GetPermissionApprovalAsync(string tenantId, string pluginId, string version, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>租户对某组件版本的批准记录（permission_approvals）；没有时返回 null。供可信平台插件（如秘密解析的审批来源）调用，调用方负责权限。</summary>
    ValueTask<PermissionApprovalView?> GetApprovedPermissionsAsync(string tenantId, string pluginId, string version, CancellationToken cancellationToken = default);
}
