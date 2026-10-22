using System.Text.Json.Serialization;
using Kun.EventBus;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

/// <summary>生命周期控制器发布的集成事件（PRD 7.2.1「插件化定位」），随状态写入同一事务进入 Outbox。</summary>
public static class LifecycleEvents
{
    /// <summary>平台修订已激活（含平台级回切）；平台级事件不带租户。</summary>
    public const string PluginActivated = "lifecycle.plugin.activated.v1";
    /// <summary>租户已升级或回切到另一版本。</summary>
    public const string TenantPluginUpgraded = "lifecycle.tenant-plugin.upgraded.v1";

    /// <summary>登记生命周期的全部集成事件及其源生成 JsonTypeInfo；平台宿主与契约测试共用这一处。</summary>
    public static IEventTypeRegistrar RegisterLifecycleEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        return registrar.Register(PluginActivated, LifecycleEventJson.Default.LifecyclePluginActivated)
            .Register(TenantPluginUpgraded, LifecycleEventJson.Default.LifecycleTenantPluginUpgraded);
    }
}

/// <summary><see cref="LifecycleEvents.PluginActivated"/> 的载荷；PreviousRevision 为此前的 Active 修订（没有时为 null）。</summary>
[IntegrationEventId(LifecycleEvents.PluginActivated)]
public sealed record LifecyclePluginActivated(string PluginId, int Revision, string Version, int? PreviousRevision);

/// <summary><see cref="LifecycleEvents.TenantPluginUpgraded"/> 的载荷。</summary>
[IntegrationEventId(LifecycleEvents.TenantPluginUpgraded)]
public sealed record LifecycleTenantPluginUpgraded(string PluginId, string FromVersion, string ToVersion);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(LifecyclePluginActivated))]
[JsonSerializable(typeof(LifecycleTenantPluginUpgraded))]
internal sealed partial class LifecycleEventJson : JsonSerializerContext;

/// <summary>生命周期控制器拥有的错误码。</summary>
public static class LifecycleErrors
{
    /// <summary>制品没有分离签名。</summary>
    public static readonly ErrorCode ArtifactUnsigned = Rule("LIFECYCLE_ARTIFACT_UNSIGNED", "制品未签名", "Artifact unsigned",
        "制品没有分离签名，安装被拒绝。", "The artifact has no detached signature and cannot be installed.");
    /// <summary>签名与摘要不符，或签名公钥不在当前有效的信任根中。</summary>
    public static readonly ErrorCode SignatureInvalid = Rule("LIFECYCLE_SIGNATURE_INVALID", "签名无效", "Signature invalid",
        "签名与制品摘要不符，或签名公钥不在当前有效的信任根中。", "The signature does not match the artifact digest, or the key is not a currently trusted root.");
    /// <summary>module 修订没有随宿主镜像发布。</summary>
    public static readonly ErrorCode RevisionNotDeployed = Rule("LIFECYCLE_REVISION_NOT_DEPLOYED", "修订未随宿主发布", "Revision not deployed",
        "宿主没有预装与该制品摘要一致的组件版本；module 修订须随宿主镜像发布。", "The host has no preinstalled component version with this artifact digest.");
    /// <summary>租户未获授权。</summary>
    public static readonly ErrorCode NotEntitled = Rule("LIFECYCLE_NOT_ENTITLED", "租户未获授权", "Tenant not entitled",
        "租户尚未获得该组件的授权。", "The tenant is not entitled to this component.");
    /// <summary>组件声明的反向权限尚未全部批准（开通或升级的第二道门，在权益之后）；missing 列出缺少批准的项。</summary>
    public static readonly ErrorCode PermissionsUnapproved = Rule("LIFECYCLE_PERMISSIONS_UNAPPROVED", "反向权限未批准", "Permissions not approved",
        "组件声明的反向权限尚未经租户管理员批准：{missing}。", "The permissions requested by the component are not approved by the tenant administrator: {missing}.");
    /// <summary>部署执行器不可用或部署失败。</summary>
    public static readonly ErrorCode DeploymentUnavailable = Rule("LIFECYCLE_DEPLOYMENT_UNAVAILABLE", "部署不可用", "Deployment unavailable",
        "{reason}", "{reason}");
    /// <summary>钩子执行失败；执行记录可重试。</summary>
    public static readonly ErrorCode HookFailed = Rule("LIFECYCLE_HOOK_FAILED", "生命周期钩子失败", "Lifecycle hook failed",
        "钩子 {hook} 执行失败，原因码 {reasonCode}；可重试。", "Hook {hook} failed with reason code {reasonCode}; it can be retried.");
    /// <summary>新版本挂载失败，已改回原版本。</summary>
    public static readonly ErrorCode MountFailed = Rule("LIFECYCLE_MOUNT_FAILED", "挂载失败", "Mount failed",
        "条目 {entryId} 挂载失败，原因码 {reasonCode}；原因见 /api/lifecycle/diagnostics。", "Entry {entryId} failed to mount with reason code {reasonCode}; see /api/lifecycle/diagnostics.");
    /// <summary>当前状态不允许该操作。</summary>
    public static readonly ErrorCode StateConflict = ErrorCode.Business("LIFECYCLE_STATE_CONFLICT", 409, "qian.lifecycle",
        new Dictionary<string, string> { ["zh-CN"] = "生命周期状态冲突", ["en"] = "Lifecycle state conflict" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
    /// <summary>生命周期输入或组件配置的字段错误；放在 INPUT_INVALID 的 errors 中，pointer 指向出错的字段。</summary>
    public static readonly ErrorCode Invalid = ErrorCode.Field("LIFECYCLE_INPUT_INVALID", "qian.lifecycle",
        new Dictionary<string, string> { ["zh-CN"] = "生命周期输入无效", ["en"] = "Invalid lifecycle input" },
        new Dictionary<string, string> { ["zh-CN"] = "{reason}", ["en"] = "{reason}" });
    /// <summary>同一租户同一组件的另一个操作正在进行。</summary>
    public static readonly ErrorCode InProgress = ErrorCode.Business("LIFECYCLE_IN_PROGRESS", 409, "qian.lifecycle",
        new Dictionary<string, string> { ["zh-CN"] = "操作进行中", ["en"] = "Operation in progress" },
        new Dictionary<string, string> { ["zh-CN"] = "同一组件的另一个生命周期操作正在进行，请稍后重试。", ["en"] = "Another lifecycle operation for this component is in progress. Retry later." },
        retryable: true);
    /// <summary>
    /// 该租户 scope 看不到审计服务（宿主没有装配 qian.audit）：生命周期操作必须入审计链（ADR-009 D9.8），按 fail-closed 拒绝且不改状态，宿主日志另有告警；
    /// operation 为被拒绝的操作名。管理面批准 API 缺审计时仍与不存在同为 404（R32），只记告警。
    /// </summary>
    public static readonly ErrorCode AuditUnavailable = ErrorCode.Protocol("LIFECYCLE_AUDIT_UNAVAILABLE", 503, "qian.lifecycle",
        new Dictionary<string, string> { ["zh-CN"] = "审计服务不可用", ["en"] = "Audit service unavailable" },
        new Dictionary<string, string>
        {
            ["zh-CN"] = "生命周期操作 {operation} 必须写入租户审计链，但宿主没有为该租户装配审计服务；操作已拒绝、状态未改，请检查宿主装配。",
            ["en"] = "Lifecycle operation {operation} must be written to the tenant audit chain, but the host has no audit service for this tenant; the operation was rejected without changing state. Check the host assembly."
        });

    /// <summary>登记全部组件错误，释放时逆序撤销。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var code in new[] { ArtifactUnsigned, SignatureInvalid, RevisionNotDeployed, NotEntitled, PermissionsUnapproved, DeploymentUnavailable, HookFailed, MountFailed, StateConflict, Invalid, InProgress, AuditUnavailable })
                registrations.Add(catalog.Register(code));
            return new Registration(registrations);
        }
        catch { foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose(); throw; }
    }

    private static ErrorCode Rule(string code, string zhTitle, string enTitle, string zhMessage, string enMessage) => ErrorCode.Business(code, 422, "qian.lifecycle",
        new Dictionary<string, string> { ["zh-CN"] = zhTitle, ["en"] = enTitle }, new Dictionary<string, string> { ["zh-CN"] = zhMessage, ["en"] = enMessage });

    private sealed class Registration(List<IDisposable> registrations) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose();
        }
    }
}
