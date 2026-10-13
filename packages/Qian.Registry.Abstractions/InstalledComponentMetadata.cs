using System.Collections.ObjectModel;
using System.Text.Json;

namespace Qian.Registry;

/// <summary>宿主预装组件的不可变声明快照，不包含租户授权或制品发布结论。</summary>
public sealed class InstalledComponentMetadata
{
    private readonly IReadOnlyDictionary<string, byte[]> _files;
    /// <summary>复制声明内容；DTO 本身不会把组件登记为已安装或已授权。</summary>
    public InstalledComponentMetadata(string pluginId, string version, JsonElement manifest, IReadOnlyDictionary<string, JsonElement> metadata)
        : this(pluginId, version, manifest, metadata, null, new Dictionary<string, ReadOnlyMemory<byte>>()) { }
    /// <summary>复制制品文件快照；DTO 不验证摘要，不授予安装资格，真实绑定须由 Registry 的制品工厂建立。</summary>
    public InstalledComponentMetadata(string pluginId, string version, JsonElement manifest, IReadOnlyDictionary<string, JsonElement> metadata,
        string? artifactDigest, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = new ReadOnlyDictionary<string, byte[]>(files.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal));
        FilePaths = Array.AsReadOnly(_files.Keys.Order(StringComparer.Ordinal).ToArray()); ArtifactDigest = artifactDigest;
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId); ArgumentException.ThrowIfNullOrWhiteSpace(version); ArgumentNullException.ThrowIfNull(metadata);
        PluginId = pluginId; Version = version; Manifest = manifest.Clone();
        Metadata = new ReadOnlyDictionary<string, JsonElement>(metadata.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal));
    }
    /// <summary>组件标识。</summary>
    public string PluginId { get; }
    /// <summary>本次预装绑定的组件版本。</summary>
    public string Version { get; }
    /// <summary>已校验的清单快照。</summary>
    public JsonElement Manifest { get; }
    /// <summary>以组件内文件路径为键的 metadata 文档；业务 Schema 仍由各 seam 校验。</summary>
    public IReadOnlyDictionary<string, JsonElement> Metadata { get; }
    /// <summary>绑定工厂核验的制品目录摘要；旧静态绑定未捕获文件时为 null。</summary>
    public string? ArtifactDigest { get; }
    /// <summary>制品快照中的相对路径，不允许借此读取宿主文件系统。</summary>
    public IReadOnlyList<string> FilePaths { get; }
    /// <summary>读取独立字节副本；未知路径返回 null，不读取源目录或网络。</summary>
    public byte[]? ReadFile(string path) => _files.TryGetValue(path, out var bytes) ? bytes.ToArray() : null;
    /// <summary>默认日志不展开清单与元数据。</summary>
    public override string ToString() => $"InstalledComponentMetadata {{ PluginId = {PluginId}, Version = {Version} }}";
}
