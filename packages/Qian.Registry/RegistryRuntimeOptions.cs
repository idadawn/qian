using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

/// <summary>平台宿主提供静态装配与已安装 Provider 的解析，不从数据库内容加载程序集。</summary>
public sealed class RegistryRuntimeOptions
{
    /// <summary>当前宿主的真实装载器。</summary>
    public required PluginLoader Runtime { get; init; }
    /// <summary>共用 profile 装配器。</summary>
    public required IProfileLoader Profiles { get; init; }
    /// <summary>已安装组件与 platform 文件配置，不预先携带数据库 scope 层。</summary>
    public required ProfileLoadOptions Profile { get; init; }
    /// <summary>共用表面解析及校验端口。</summary>
    public required ISurfaceValidator Surfaces { get; init; }
    /// <summary>只解析宿主已安装的 Provider 或已校验声明式组件；条目身份与配置必须保持一致。</summary>
    public required Func<ResolvedProfileEntry, PluginDefinition> Resolve { get; init; }
}
