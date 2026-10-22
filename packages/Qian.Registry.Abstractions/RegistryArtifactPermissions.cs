namespace Qian.Registry;

/// <summary>
/// 组件版本向平台申请的反向权限（组件规格 §3.4）：清单 policy.permissions 的 platform / data / secrets / ui.connectSrc 与 policy.egress 五类，
/// 原样取自注册表保存的已校验清单快照（plugin_artifacts.manifest），按清单中的顺序；缺省的类别为空数组，x- 扩展字段不计入。
/// 这只是声明，不代表任何租户已批准；Data 沿 permissions/v2 Schema 为字符串数组。
/// </summary>
public sealed record RegistryArtifactPermissions(string PluginId, string Version, string ArtifactDigest, IReadOnlyList<string> Platform,
    IReadOnlyList<string> Data, IReadOnlyList<string> Egress, IReadOnlyList<RegistrySecretAccess> Secrets, RegistryUiPermissions Ui);

/// <summary>policy.permissions.secrets 的一项：Name 为精确路径或末尾 /* 前缀，Scope 为 platform、tenant 或 dev；不含任何秘密值。</summary>
public sealed record RegistrySecretAccess(string Name, string Scope);

/// <summary>policy.permissions.ui：外壳加载该组件前端时追加到 CSP connect-src 的目标。</summary>
public sealed record RegistryUiPermissions(IReadOnlyList<string> ConnectSrc);
