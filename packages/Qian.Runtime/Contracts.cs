using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors;

namespace Qian.Runtime;

/// <summary>Definition 拥有的服务键；具名注册可用 Key:name 声明就绪依赖。</summary>
public readonly record struct ServiceKey(string Name);

/// <summary>两层扁平作用域，不隐含租户、应用与会话之间的继承关系。</summary>
public readonly record struct ScopeKey(string Kind, string Id)
{
    /// <summary>全局注册的作用域。</summary>
    public static readonly ScopeKey Global = new("global", "");

    internal void Validate()
    {
        if (this != Global && (Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrWhiteSpace(Id)))
            throw new ArgumentException("scope 必须为 global 或非空的 tenant / app / agent-session。");
    }
}

/// <summary>已由清单与配置装配器解析的插件条目。</summary>
public sealed record PluginEntry(string Id, string Plugin, string Version, ScopeKey Scope, int Layer, JsonElement Config);

/// <summary>装载输入；Provides 来自清单，不通过试运行 Apply 推测。</summary>
public sealed record PluginDefinition(PluginEntry Entry, IQianPlugin Plugin, IReadOnlyList<ServiceKey> Provides);

/// <summary>插件的最小接口。</summary>
public interface IQianPlugin
{
    /// <summary>Apply 前必须就绪的服务键。</summary>
    IReadOnlyList<ServiceKey> Inject { get; }
    /// <summary>注册服务、监听器与效果；失败时全部逆序撤销。</summary>
    ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct);
}

/// <summary>类型化配置接口；Schema 校验由清单装配器在调用前完成。</summary>
public interface IQianPlugin<TConfig> : IQianPlugin
{
    /// <summary>以类型化配置执行 Apply。</summary>
    ValueTask ApplyAsync(IPluginContext ctx, TConfig config, CancellationToken ct);

    ValueTask IQianPlugin.ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        => ApplyAsync(ctx, config.Deserialize<TConfig>(JsonSerializerOptions.Web) ?? throw new JsonException("配置不能为空。"), ct);
}

/// <summary>插件的服务、效果与作用域入口。</summary>
public interface IPluginContext
{
    /// <summary>拥有当前效果栈的条目；Scoped 不改变条目身份。</summary>
    PluginEntry Entry { get; }
    /// <summary>当前解析与注册所在的作用域。</summary>
    ScopeKey Scope { get; }
    /// <summary>只读宿主服务入口；以 ServiceKey 解析的 keyed service 经过当前 scope 过滤。</summary>
    IServiceProvider Services { get; }
    /// <summary>宿主提供的日志器。</summary>
    ILogger Logger { get; }
    /// <summary>独占注册服务键，返回可重复撤销、可原子替换实例的注册句柄；不自动 Dispose 服务对象。</summary>
    IRegistrationHandle<TService> Provide<TService>(ServiceKey key, TService instance) where TService : class;
    /// <summary>解析当前可见的服务；未就绪时抛出 ServiceNotReadyException。</summary>
    TService Inject<TService>(ServiceKey key) where TService : class;
    /// <summary>解析可选服务；缺失与不可见均返回 null。</summary>
    TService? TryInject<TService>(ServiceKey key) where TService : class;
    /// <summary>等待当前 scope 的服务就绪，可由 ct 取消；上下文卸载后结束等待。</summary>
    ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class;
    /// <summary>将第三方资源加入当前 Apply 的效果栈。</summary>
    void Effect(IDisposable disposer);
    /// <summary>加入同步撤销动作；卸载时逆序执行。</summary>
    void Effect(Action dispose);
    /// <summary>加入异步撤销动作；逐项 await 后才继续撤销。</summary>
    void Effect(Func<ValueTask> disposeAsync);
    /// <summary>注册 emit、waterfall 或 bail 监听器，模式必须与 Definition 一致。</summary>
#pragma warning disable CA1716 // 规格 §5.2 的公共 API 名称固定为 On。
    IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority);
    /// <summary>以默认优先级 0 注册监听器。</summary>
    IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler) => On(evt, mode, handler, 0);
    /// <summary>注册 parallel 或 serial 监听器；单个监听器返回 TResult。</summary>
    IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler, int priority);
    /// <summary>以默认优先级 0 注册集合分发监听器。</summary>
    IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler) => On(evt, mode, handler, 0);
#pragma warning restore CA1716
    /// <summary>发起一次 emit、waterfall 或 bail 分发。</summary>
    ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct);
    /// <summary>发起 parallel 或 serial 分发，返回监听器顺序的结果集合。</summary>
    ValueTask<IReadOnlyList<TResult>> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, TPayload payload, CancellationToken ct);
    /// <summary>发起 waterfall 分发并指定链尾。</summary>
    ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, Func<ValueTask<TResult>> terminal, CancellationToken ct);
    /// <summary>返回指定 scope 的上下文，与当前 Apply 共享效果栈，不继承当前 scope。</summary>
    IPluginContext Scoped(ScopeKey scope);
    /// <summary>过滤该 scope 可见的全局注册；多个纯函数谓词取交集。</summary>
    IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep);
    /// <summary>返回已应用遮蔽与 restrict 的注册快照。</summary>
    IReadOnlyList<Registration> List(ServiceKey key);
    /// <summary>注册具名 seam 实现，等价于提供 Key:name 服务键。</summary>
    IRegistrationHandle<TService> Provide<TService>(ServiceKey key, string name, TService instance) where TService : class;
    /// <summary>解析具名 seam 实现；不存在与被过滤时返回相同 NOT_FOUND。</summary>
    TService Inject<TService>(ServiceKey key, string name) where TService : class;
}

/// <summary>Provide 返回的注册句柄：Dispose 撤销注册，可重复调用；Replace 原子替换同一注册的实例。</summary>
public interface IRegistrationHandle<TService> : IDisposable where TService : class
{
    /// <summary>
    /// 在原注册位原子替换实例：解析方只会看到旧实例或新实例，不会看到注册缺失；scope、注册顺序与独占性不变。
    /// 替换等价于在同一临界区内撤销后再登记，以 inject 依赖该键的条目同样回到 Pending 并重新 Apply。
    /// 句柄已撤销或上下文已卸载时抛出 ObjectDisposedException。
    /// </summary>
    void Replace(TService instance);
}

/// <summary>目录与执行共用的注册描述；Name 为空表示键本身，具名 seam 使用独立名称。</summary>
public sealed record Registration(ServiceKey Key, string Name, ScopeKey Scope, string OwnerEntryId, object Value);

/// <summary>某次 Apply 的状态；失败保留原异常供管理面查询。</summary>
public enum PluginState
{
    /// <summary>等待全部必需服务。</summary>
    Pending,
    /// <summary>正在执行 Apply，注册尚未对其它插件公开。</summary>
    Applying,
    /// <summary>Apply 成功且依赖保持就绪。</summary>
    Ready,
    /// <summary>Apply 失败且已尝试撤销全部效果。</summary>
    Failed,
}

/// <summary>当前服务尚不可见。不存在与被过滤的服务返回相同错误。</summary>
public sealed class ServiceNotReadyException : InvalidOperationException
{
    /// <summary>构造不暴露不可见原因的异常。</summary>
    public ServiceNotReadyException() : base("服务不存在或尚未就绪。") { }
    /// <summary>供宿主转换的统一 NOT_FOUND 错误。</summary>
    public QianError Error { get; } = new(PlatformErrors.NotFound);
}
