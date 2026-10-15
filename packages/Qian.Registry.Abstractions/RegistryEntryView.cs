namespace Qian.Registry;

/// <summary>管理面 scope 视图中已挂载且 Ready 的条目；Scope 为所属作用域文本（global 或 tenant:&lt;id&gt;），不含配置原文。</summary>
public sealed record RegistryEntryView(string EntryId, string PluginId, string Version, string Scope, int Layer, int? Revision);

/// <summary>候选制品目录中的一条记录；State 只表示登记状态，不代表已验签或已发布。</summary>
public sealed record RegistryArtifactEntry(string Digest, string PluginId, string Version, string State, string SubmittedBy, DateTimeOffset SubmittedAt);

/// <summary>
/// scope 最近一次装配失败的诊断（本副本的运行时结论，供管理面 /api/lifecycle/diagnostics）：EntryId 为空表示整个 scope 无法装配；
/// Plugin 形如 组件@版本，Revision 为该条目的 scope patch 修订，ReasonCode 为错误码（契约不兼容为 PRECONDITION_FAILED）。不含配置原文或底层异常文本。
/// </summary>
public sealed record RegistryScopeDiagnostic(string Scope, string? EntryId, string? Plugin, int? Revision, string ReasonCode, DateTimeOffset OccurredAt);
