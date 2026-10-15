using System.Collections.ObjectModel;

namespace Qian.Lifecycle;

/// <summary>待安装的制品：组件文件夹的完整字节、D4 目录摘要（sha256:…）与分离签名；签名不写回清单。</summary>
public sealed class PluginArtifact
{
    private readonly ReadOnlyDictionary<string, ReadOnlyMemory<byte>> _files;
    /// <summary>复制文件字节；摘要与签名在安装时核验，构造本身不代表可信。</summary>
    public PluginArtifact(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, string digest, ArtifactSignature? signature)
    {
        ArgumentNullException.ThrowIfNull(files); ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        _files = new ReadOnlyDictionary<string, ReadOnlyMemory<byte>>(files.ToDictionary(item => item.Key, item => new ReadOnlyMemory<byte>(item.Value.ToArray()), StringComparer.Ordinal));
        Digest = digest; Signature = signature;
    }
    /// <summary>以组件内相对路径为键的文件字节副本。</summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Files => _files;
    /// <summary>D4 目录摘要：文件「路径\t字节数\tSHA-256」清单的 SHA-256，形如 sha256:&lt;64 位小写十六进制&gt;。</summary>
    public string Digest { get; }
    /// <summary>分离签名；为 null 表示未签名，安装直接失败。</summary>
    public ArtifactSignature? Signature { get; }
    /// <inheritdoc />
    public override string ToString() => $"PluginArtifact {{ Digest = {Digest}, Files = {_files.Count} }}";
}

/// <summary>
/// Ed25519 分离签名：载荷是 D4 目录摘要的 32 字节原始值（与市场文件签名「对内容的 SHA-256 签名」同一做法），
/// KeyId 是签名公钥 SubjectPublicKeyInfo（DER）的 SHA-256 指纹，形如 sha256:&lt;64 位小写十六进制&gt;。
/// </summary>
public sealed record ArtifactSignature(string KeyId, ReadOnlyMemory<byte> Value)
{
    /// <summary>默认日志不展开签名字节。</summary>
    public override string ToString() => $"ArtifactSignature {{ KeyId = {KeyId} }}";
}

/// <summary>平台修订状态。</summary>
public static class PluginRevisionStates
{
    /// <summary>已安装，可激活或供租户开通。</summary>
    public const string Installed = "Installed";
    /// <summary>当前激活修订，同一组件至多一个。</summary>
    public const string Active = "Active";
    /// <summary>已弃用，不再接受新的开通。</summary>
    public const string Deprecated = "Deprecated";
    /// <summary>已退役，不能再激活、回切或开通。</summary>
    public const string Retired = "Retired";
}

/// <summary>service 修订的部署健康（工作负载状态 + 组件 /health 双条件）。</summary>
public static class DeploymentHealthStates
{
    /// <summary>期望状态已提交，尚未同时满足两个条件。</summary>
    public const string Progressing = "Progressing";
    /// <summary>工作负载就绪且 /health 通过。</summary>
    public const string Healthy = "Healthy";
}

/// <summary>租户安装状态（R34：Enabled / Disabled 已删除，挂载状态只在 scope_patches）。</summary>
public static class TenantInstallStates
{
    /// <summary>已获授权，可开通。</summary>
    public const string Entitled = "Entitled";
    /// <summary>开通中；失败可重试，从失败的钩子继续。</summary>
    public const string Provisioning = "Provisioning";
    /// <summary>已开通到当前版本。</summary>
    public const string Installed = "Installed";
    /// <summary>升级或回切中。</summary>
    public const string Upgrading = "Upgrading";
    /// <summary>已卸载，业务数据保留。</summary>
    public const string Uninstalled = "Uninstalled";
    /// <summary>已清除组件的租户数据。</summary>
    public const string Purged = "Purged";
}

/// <summary>钩子执行记录的状态。</summary>
public static class LifecycleRunStates
{
    /// <summary>正在执行；租约过期后可由重试接管。</summary>
    public const string Running = "Running";
    /// <summary>已成功，同一幂等键不再执行。</summary>
    public const string Succeeded = "Succeeded";
    /// <summary>已失败，可重试。</summary>
    public const string Failed = "Failed";
}

/// <summary>挂载结论：Ready、Failed（ReasonCode 为原因码）或 Unmounted。</summary>
public static class MountStates
{
    /// <summary>条目已在该 scope 就绪。</summary>
    public const string Ready = "Ready";
    /// <summary>条目装配失败，已撤下；原因见诊断。</summary>
    public const string Failed = "Failed";
    /// <summary>条目已从该 scope 卸载。</summary>
    public const string Unmounted = "Unmounted";
}

/// <summary>平台修订：组件 × 修订号 × 制品摘要 × 状态；Form 为 runtime.form（无代码组件为 declarative），SignedBy 为验签公钥指纹。</summary>
public sealed record PluginRevisionView(string PluginId, int Revision, string Version, string ArtifactDigest, string Form, string State, string SignedBy,
    string? Health, string? DeploymentReference, DateTimeOffset InstalledAt, DateTimeOffset? ActivatedAt);

/// <summary>租户安装记录：Version 为已开通版本，TargetVersion 为进行中的开通或升级目标。</summary>
public sealed record TenantPluginInstallView(string TenantId, string PluginId, string State, string? Version, string? TargetVersion, string UpdatedBy, DateTimeOffset UpdatedAt);

/// <summary>钩子执行记录（lifecycle_runs）：(TenantId, PluginId, Hook, ToVersion) 即幂等键；ReasonCode 为失败的错误码。</summary>
public sealed record LifecycleRunView(string TenantId, string PluginId, string Hook, string? FromVersion, string ToVersion, string Status, int Attempts,
    string? ReasonCode, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

/// <summary>挂载或卸载的结论；Revision 为写入后的 scope patch 修订。</summary>
public sealed record MountResult(string Scope, string EntryId, int Revision, string State, string? ReasonCode);

/// <summary>管理面装配诊断：EntryId 为空表示整个 scope 无法装配；Plugin 形如 组件@版本；ReasonCode 为错误码（契约不兼容为 PRECONDITION_FAILED）。</summary>
public sealed record LifecycleDiagnosticView(string Scope, string? EntryId, string? Plugin, int? Revision, string ReasonCode, DateTimeOffset OccurredAt);
