using Qian.Runtime;

namespace Qian.Registry;

/// <summary>业务可见性检查使用的非敏感运行时绑定快照；不包含配置、服务对象或管理面原因。</summary>
public sealed record RegistryScopeEntry(string EntryId, string PluginId, string Version, ScopeKey Scope, ServiceKey Key, string Name);
