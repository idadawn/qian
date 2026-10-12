using System.Text.Json;
using Qian.Runtime;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

/// <summary>宿主把预装 Provider 与同一组件的静态校验结果绑定；不能由管理请求中的可信标志替代。</summary>
public sealed class RegistryComponentBinding
{
    /// <summary>检查预装代码/声明式 Provider 的身份与声明后复制元数据，不运行 Apply。</summary>
    public RegistryComponentBinding(PluginDefinition definition, SurfaceValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(definition); ArgumentNullException.ThrowIfNull(validation);
        if (!validation.IsValid || validation.Component is not { } component || component.Profile != "platform")
            throw new ArgumentException("预装组件必须通过 platform 静态校验。", nameof(validation));
        var manifest = component.Manifest;
        if ((manifest.TryGetProperty("x-skeleton", out var skeleton) && skeleton.ValueKind == JsonValueKind.True) ||
            component.Id != manifest.GetProperty("id").GetString() || definition.Entry.Plugin != component.Id ||
            definition.Entry.Version != manifest.GetProperty("version").GetString())
            throw new ArgumentException("组件骨架或身份不一致的校验结果不能成为预装绑定。", nameof(validation));
        var required = manifest.GetProperty("inject").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal);
        var provided = manifest.GetProperty("provides").EnumerateArray().Select(value => new ServiceKey(value.GetString()!));
        if (!required.SequenceEqual(definition.Plugin.Inject.Select(key => key.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            provided.Any(key => !definition.Provides.Contains(key)))
            throw new ArgumentException("Provider 的依赖与服务声明不符合组件清单。", nameof(definition));
        Definition = definition;
        Metadata = new InstalledComponentMetadata(component.Id, definition.Entry.Version, manifest,
            component.Documents.Where(document => document.Kind == "metadata").ToDictionary(document => document.File, document => document.Data, StringComparer.Ordinal));
    }
    /// <summary>交给同一宿主装载器的预装定义；尚未挂载时不会因此成为 Ready。</summary>
    public PluginDefinition Definition { get; }
    /// <summary>只读声明快照；不含候选制品的发布状态。</summary>
    public InstalledComponentMetadata Metadata { get; }
}
