using System.Text.Json;
using Qian.Runtime;

namespace Qian.Lifecycle;

/// <summary>租户级钩子名。</summary>
public static class LifecycleHooks
{
    /// <summary>开通：from 为空、to 为开通版本。</summary>
    public const string TenantProvision = "tenant-provision";
    /// <summary>升级或租户级回切：由目标版本的实现执行。</summary>
    public const string TenantUpgrade = "tenant-upgrade";
    /// <summary>卸载：清理临时数据，保留业务数据。</summary>
    public const string TenantUninstall = "tenant-uninstall";
    /// <summary>清除：删除组件的租户数据。</summary>
    public const string TenantPurge = "tenant-purge";
}

/// <summary>
/// module 形态组件的进程内生命周期钩子（扩展点 qian.lifecycle.hook/v1；service 形态对应 gRPC qian.lifecycle.v1.Lifecycle）。宿主按已安装的组件版本
/// 把实现交给 Lifecycle，钩子不要求组件已挂载。同一幂等键成功后不再调用；失败可重试，实现必须幂等（可用 <see cref="LifecycleHookContext.IdempotencyKey"/> 去重）。
/// </summary>
public interface IPluginLifecycleHooks
{
    /// <summary>执行一个钩子；不处理的钩子返回 null。返回值作为执行结果记入 lifecycle_runs，不得包含秘密或个人信息。</summary>
    ValueTask<JsonElement?> InvokeAsync(LifecycleHookContext context, CancellationToken cancellationToken);
}

/// <summary>一次钩子调用的输入；租户数据上下文已由 Lifecycle 进入 <see cref="TenantId"/>。</summary>
public sealed class LifecycleHookContext
{
    /// <summary>钩子名，见 <see cref="LifecycleHooks"/>。</summary>
    public required string Hook { get; init; }
    /// <summary>目标租户（小写规范 UUID）。</summary>
    public required string TenantId { get; init; }
    /// <summary>组件 ID。</summary>
    public required string PluginId { get; init; }
    /// <summary>升级或回切的原版本；开通时为 null。</summary>
    public string? FromVersion { get; init; }
    /// <summary>目标版本。</summary>
    public required string ToVersion { get; init; }
    /// <summary>由 (tenant, plugin, hook, toVersion) 确定的 UUID，重试时不变。</summary>
    public required Guid IdempotencyKey { get; init; }
    /// <summary>Lifecycle 在该租户 scope 的上下文视图：解析该 scope 可见的服务（如数据组件），不代表组件已挂载。</summary>
    public required IPluginContext Context { get; init; }
    /// <inheritdoc />
    public override string ToString() => $"LifecycleHookContext {{ Hook = {Hook}, PluginId = {PluginId}, ToVersion = {ToVersion} }}";
}

/// <summary>
/// 部署执行器（扩展点 qian.lifecycle.deployment-executor/v1）：为 runtime.form: service 的组件修订产出期望状态，并以工作负载状态 + 组件 /health
/// 双条件观测健康。GitOps 实现提交 Kustomize 片段到部署仓库由 Argo 收敛；Local 实现供开发态使用，语义相同。
/// </summary>
public interface IDeploymentExecutor
{
    /// <summary>执行器名：gitops 或 local。</summary>
    string Name { get; }
    /// <summary>提交期望状态；同一修订重复提交是幂等的。</summary>
    ValueTask<DeploymentHandle> ApplyAsync(DeploymentRequest request, CancellationToken cancellationToken);
    /// <summary>观测一次健康；任何读取失败都按未就绪处理，不能当作健康。</summary>
    ValueTask<DeploymentHealth> ObserveAsync(DeploymentHandle handle, CancellationToken cancellationToken);
}

/// <summary>部署输入：Image 为清单 runtime.image 去掉 oci:// 后的 digest 引用；Port 与 HealthPath 取自 runtime.service（缺省 8080、/health）。</summary>
public sealed record DeploymentRequest(string PluginId, string Version, int Revision, string ArtifactDigest, string Image, int Port, string HealthPath);

/// <summary>
/// 已提交的部署：Workload 为工作负载名（DNS-1123 标签，含版本，同一组件的多个版本可同时运行），Reference 为执行器侧的提交凭据
/// （GitOps 为部署仓库提交 SHA，Local 为期望状态的内容摘要）；Port 与 HealthPath 供健康观测。
/// </summary>
public sealed record DeploymentHandle(string Executor, string PluginId, string Version, string Workload, string Reference, int Port, string HealthPath);

/// <summary>一次健康观测：两个条件都满足才算健康；Detail 只含状态摘要，不含凭据或响应正文。</summary>
public sealed record DeploymentHealth(bool WorkloadReady, bool HealthEndpointOk, string Detail)
{
    /// <summary>工作负载就绪且 /health 通过。</summary>
    public bool Healthy => WorkloadReady && HealthEndpointOk;
}
