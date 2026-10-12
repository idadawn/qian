using Qian.Runtime;

namespace Qian.Plugin.Sdk.AspNetCore;

/// <summary>宿主绑定到真实组件注册的端点元数据；值不能从请求输入推导。</summary>
public sealed class PluginEndpointBinding
{
    /// <summary>指定预期条目、组件、服务键和具名贡献；version 非空时同时核对版本。</summary>
    public PluginEndpointBinding(string entryId, string pluginId, ServiceKey key, string name = "", string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId); ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Name); ArgumentNullException.ThrowIfNull(name);
        EntryId = entryId; PluginId = pluginId; Key = key; Name = name; Version = version;
    }
    /// <summary>具体安装条目 ID。</summary>
    public string EntryId { get; }
    /// <summary>组件 ID。</summary>
    public string PluginId { get; }
    /// <summary>实际服务键。</summary>
    public ServiceKey Key { get; }
    /// <summary>具名贡献的名称；普通服务为空。</summary>
    public string Name { get; }
    /// <summary>可选的精确运行版本。</summary>
    public string? Version { get; }
}

/// <summary>宿主提供真实 Registry 所在上下文，避免 SDK 依赖平台 Provider。</summary>
public sealed class PluginEndpointOptions
{
    /// <summary>上下文工厂在每个已认证请求的服务 scope 内调用。</summary>
    public PluginEndpointOptions(Func<IServiceProvider, IPluginContext> registryContext)
    { ArgumentNullException.ThrowIfNull(registryContext); RegistryContext = registryContext; }
    /// <summary>真实 Registry 宿主上下文解析器。</summary>
    public Func<IServiceProvider, IPluginContext> RegistryContext { get; }
}
