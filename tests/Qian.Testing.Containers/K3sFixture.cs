using Testcontainers.K3s;

namespace Qian.Testing.Containers;

/// <summary>
/// K3s 单节点集群（特权容器）。<see cref="Kubeconfig"/> 里的 API Server 已映射到宿主端口；容器内自带 kubectl（含 kustomize），
/// 可用 <see cref="KubectlAsync"/> 代替 Argo 应用部署仓库里的片段。
/// </summary>
public sealed class K3sFixture : ContainerFixture<K3sContainer>
{
    /// <summary>管理员 kubeconfig（YAML）。</summary>
    public string Kubeconfig { get; private set; } = string.Empty;

    /// <inheritdoc />
    protected override K3sContainer CreateContainer() => new K3sBuilder(ContainerImages.K3s).Build();

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(K3sContainer container, CancellationToken cancellationToken)
    {
        Kubeconfig = await container.GetKubeconfigAsync().ConfigureAwait(false);
    }

    /// <summary>在容器内执行 kubectl，返回标准输出；退出码非零时抛出并带上标准错误。</summary>
    public async Task<string> KubectlAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var result = await Container.ExecAsync(["kubectl", .. arguments], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) throw new InvalidOperationException($"kubectl {string.Join(' ', arguments)} 失败：{result.Stderr}");
        return result.Stdout;
    }

    /// <summary>把文本写进容器内的文件（如待 kubectl apply 的清单）。</summary>
    public Task CopyAsync(string content, string path, CancellationToken cancellationToken) =>
        Container.CopyAsync(System.Text.Encoding.UTF8.GetBytes(content), path, ct: cancellationToken);
}
