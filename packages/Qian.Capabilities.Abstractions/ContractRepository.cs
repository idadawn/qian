using System.Security.Cryptography;

namespace Qian.Capabilities;

/// <summary>版本化制品中的不可变契约；读取内容返回副本，不携带发布或授权结论。</summary>
public sealed class ContractDocument
{
    private readonly byte[] _content;
    /// <summary>复制契约原始字节并计算摘要。</summary>
    public ContractDocument(string pluginId, string pluginVersion, string id, string kind, string path, string artifactDigest, ReadOnlyMemory<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId); ArgumentException.ThrowIfNullOrWhiteSpace(pluginVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(id); ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentException.ThrowIfNullOrWhiteSpace(artifactDigest);
        PluginId = pluginId; PluginVersion = pluginVersion; Id = id; Kind = kind; Path = path; ArtifactDigest = artifactDigest;
        _content = content.ToArray(); Sha256 = Convert.ToHexStringLower(SHA256.HashData(_content));
    }
    /// <summary>契约所属组件。</summary>
    public string PluginId { get; }
    /// <summary>组件制品版本。</summary>
    public string PluginVersion { get; }
    /// <summary>带 /vN 的稳定契约族。</summary>
    public string Id { get; }
    /// <summary>openapi、proto 或 json-schema。</summary>
    public string Kind { get; }
    /// <summary>制品内的相对文件路径。</summary>
    public string Path { get; }
    /// <summary>经 Registry 绑定工厂核验的目录摘要。</summary>
    public string ArtifactDigest { get; }
    /// <summary>契约原始内容的 SHA-256。</summary>
    public string Sha256 { get; }
    /// <summary>返回独立字节副本。</summary>
    public byte[] ReadContent() => _content.ToArray();
}

/// <summary>平台安装制品的契约仓库；按组件版本查询，不用当前用户过滤后的目录充当兼容基线。</summary>
public interface IContractRepository
{
    /// <summary>读取某个预装组件版本的契约，只返回已捕获的不可变制品。</summary>
    IReadOnlyList<ContractDocument> GetContracts(string pluginId, string version);
    /// <summary>读取指定版本的契约族；不存在返回 null。</summary>
    ContractDocument? Find(string pluginId, string version, string contractId);
    /// <summary>读取 proto 契约所在制品的封闭源文件集合，包含其本地导入。</summary>
    ProtoContractSet GetProtoSource(string pluginId, string version, string contractId);
    /// <summary>比较两个完整制品的 proto 协议；语法错误或依赖缺失会失败。</summary>
    ValueTask<ContractCompatibility> CheckProtoAsync(string pluginId, string baselineVersion, string candidateVersion, string contractId, CancellationToken cancellationToken = default);
    /// <summary>用两个完整制品中的同一 OpenAPI 契约族执行真实兼容检查。</summary>
    ValueTask<ContractCompatibility> CheckOpenApiAsync(string pluginId, string baselineVersion, string candidateVersion, string contractId, CancellationToken cancellationToken = default);
}
