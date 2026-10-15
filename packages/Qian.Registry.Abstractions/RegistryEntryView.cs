namespace Qian.Registry;

/// <summary>管理面 scope 视图中已挂载且 Ready 的条目；Scope 为所属作用域文本（global 或 tenant:&lt;id&gt;），不含配置原文。</summary>
public sealed record RegistryEntryView(string EntryId, string PluginId, string Version, string Scope, int Layer, int? Revision);

/// <summary>候选制品目录中的一条记录；State 见 <see cref="RegistryArtifactStates"/>，Submitted 只表示已登记，不代表已验签或已发布。</summary>
public sealed record RegistryArtifactEntry(string Digest, string PluginId, string Version, string State, string SubmittedBy, DateTimeOffset SubmittedAt);

/// <summary>制品目录记录的状态：Registry 登记为 Submitted，Lifecycle 验签安装后回写其余三种。</summary>
public static class RegistryArtifactStates
{
    /// <summary>已登记（摘要与静态校验通过），尚未验签安装。</summary>
    public const string Submitted = "Submitted";
    /// <summary>已验签安装，无需部署（module 随宿主镜像发布，或声明式组件）。</summary>
    public const string Installed = "Installed";
    /// <summary>service 修订已提交期望状态，工作负载状态与 /health 尚未同时满足。</summary>
    public const string Progressing = "Progressing";
    /// <summary>service 修订的工作负载就绪且 /health 通过。</summary>
    public const string Healthy = "Healthy";
}

/// <summary>
/// scope 最近一次装配失败的诊断（本副本的运行时结论，供管理面 /api/lifecycle/diagnostics）：EntryId 为空表示整个 scope 无法装配；
/// Plugin 形如 组件@版本，Revision 为该条目的 scope patch 修订，ReasonCode 为错误码（契约不兼容为 PRECONDITION_FAILED）。不含配置原文或底层异常文本。
/// </summary>
public sealed record RegistryScopeDiagnostic(string Scope, string? EntryId, string? Plugin, int? Revision, string ReasonCode, DateTimeOffset OccurredAt);
