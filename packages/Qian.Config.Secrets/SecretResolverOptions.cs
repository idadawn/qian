namespace Qian.Config.Secrets;

/// <summary>公开引用到 Kubernetes 挂载文件的可信绑定。</summary>
public sealed record SecretBinding(string Reference, KubernetesSecretLocation Location, string? TenantId = null);

/// <summary>调用方的声明与审批策略，两者都必须允许访问。</summary>
public sealed record SecretAccessPolicy(string Id, IReadOnlyList<SecretGrant> Declared, IReadOnlyList<SecretGrant> Approved);

/// <summary>只来自可信宿主引导，不从请求或模型输入建立授权。</summary>
public sealed class SecretResolverOptions
{
    /// <summary>只读 Secret 投射根目录。</summary>
    public required string MountRoot { get; init; }
    /// <summary>公开引用绑定；tenant 绑定必须使用完整 tenant/id/path 形式及同一个 TenantId。</summary>
    public IReadOnlyList<SecretBinding> Bindings { get; init; } = [];
    /// <summary>插件或独立宿主的已审批策略。</summary>
    public IReadOnlyList<SecretAccessPolicy> Policies { get; init; } = [];
    /// <summary>只有开发宿主显式开启才可访问 dev 引用。</summary>
    public bool AllowDevelopmentReferences { get; init; }
}
