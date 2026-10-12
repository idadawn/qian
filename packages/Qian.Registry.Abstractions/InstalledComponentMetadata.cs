using System.Collections.ObjectModel;
using System.Text.Json;

namespace Qian.Registry;

/// <summary>宿主预装组件的不可变声明快照，不包含租户授权或制品发布结论。</summary>
public sealed class InstalledComponentMetadata
{
    /// <summary>复制声明内容；DTO 本身不会把组件登记为已安装或已授权。</summary>
    public InstalledComponentMetadata(string pluginId, string version, JsonElement manifest, IReadOnlyDictionary<string, JsonElement> metadata)
    {
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
    /// <summary>默认日志不展开清单与元数据。</summary>
    public override string ToString() => $"InstalledComponentMetadata {{ PluginId = {PluginId}, Version = {Version} }}";
}
