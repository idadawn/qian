using Qian.Runtime;

namespace Qian.Sample.Crm;

/// <summary>宿主装配入口；业务 Provider 保持同一份代码，宿主负责读取 profile 与解析已安装组件。</summary>
public interface ICrmRuntimeAssembly
{
    /// <summary>在首次 Mount 之前返回实际装配结果；不能在这里执行业务请求。</summary>
    ValueTask<IReadOnlyList<PluginDefinition>> ComposeAsync(PluginLoader runtime, IReadOnlyList<PluginDefinition> businessProviders, CancellationToken cancellationToken);
    /// <summary>全部条目 Ready 后启动宿主自身的监听等工作。</summary>
    ValueTask ReadyAsync(PluginLoader runtime, CancellationToken cancellationToken);
}
