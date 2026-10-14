namespace Qian.Registry;

/// <summary>管理面 scope 视图中已挂载且 Ready 的条目；Scope 为所属作用域文本（global 或 tenant:&lt;id&gt;），不含配置原文。</summary>
public sealed record RegistryEntryView(string EntryId, string PluginId, string Version, string Scope, int Layer, int? Revision);

/// <summary>候选制品目录中的一条记录；State 只表示登记状态，不代表已验签或已发布。</summary>
public sealed record RegistryArtifactEntry(string Digest, string PluginId, string Version, string State, string SubmittedBy, DateTimeOffset SubmittedAt);
