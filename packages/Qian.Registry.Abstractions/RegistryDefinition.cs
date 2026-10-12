using Qian.Runtime;

namespace Qian.Registry;

/// <summary>平台 Registry seam；管理 API 的权限边界由平台注册表服务负责。</summary>
public static class RegistryDefinition
{
    /// <summary>平台注册表服务键。</summary>
    public static readonly ServiceKey Key = new("Registry");
    /// <summary>取得已挂载的平台注册表。</summary>
    public static IRegistryService Registry(this IPluginContext context) => context.Inject<IRegistryService>(Key);
}

/// <summary>宿主的注册表运行时入口，不是可直接映射 HTTP 的管理写入 API。</summary>
public interface IRegistryService : IAsyncDisposable
{
    /// <summary>整棵初始插件树 Ready 后启动数据库监听；不得在该装载器的 Apply 内等待。</summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    /// <summary>合入当前已验证租户的 scope；当前只接受同租户 scope，应用/会话须另有归属证明。</summary>
    ValueTask SynchronizeAsync(ScopeKey scope, CancellationToken cancellationToken = default);
    /// <summary>停止监听并撤销拥有的局部条目；宿主须在 Runtime.Dispose 之前、在 Apply/effect 之外调用。</summary>
    new ValueTask DisposeAsync();
}
