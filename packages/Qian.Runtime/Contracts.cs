using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors;

namespace Qian.Runtime;

/// <summary>Definition 拥有的服务键；具名注册可用 Key:name 声明就绪依赖。</summary>
public readonly record struct ServiceKey(string Name);

/// <summary>两层扁平作用域，不隐含租户、应用与会话之间的继承关系。</summary>
public readonly record struct ScopeKey(string Kind, string Id)
{
    public static readonly ScopeKey Global = new("global", "");

    internal void Validate()
    {
        if (this != Global && (Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrWhiteSpace(Id)))
            throw new ArgumentException("scope 必须为 global 或非空的 tenant / app / agent-session。 ");
    }
}

/// <summary>已由清单与配置装配器解析的插件条目。</summary>
public sealed record PluginEntry(string Id, string Plugin, string Version, ScopeKey Scope, int Layer, JsonElement Config);

/// <summary>装载输入；Provides 来自清单，不通过试运行 Apply 推测。</summary>
public sealed record PluginDefinition(PluginEntry Entry, IQianPlugin Plugin, IReadOnlyList<ServiceKey> Provides);

/// <summary>插件的最小接口。</summary>
public interface IQianPlugin
{
    IReadOnlyList<ServiceKey> Inject { get; }
    ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct);
}

/// <summary>类型化配置接口；Schema 校验由清单装配器在调用前完成。</summary>
public interface IQianPlugin<TConfig> : IQianPlugin
{
    ValueTask ApplyAsync(IPluginContext ctx, TConfig config, CancellationToken ct);

    ValueTask IQianPlugin.ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        => ApplyAsync(ctx, config.Deserialize<TConfig>() ?? throw new JsonException("配置不能为空。"), ct);
}

/// <summary>插件的服务、效果与作用域入口。</summary>
public interface IPluginContext
{
    PluginEntry Entry { get; }
    ScopeKey Scope { get; }
    IServiceProvider Services { get; }
    ILogger Logger { get; }
    IDisposable Provide<TService>(ServiceKey key, TService instance) where TService : class;
    TService Inject<TService>(ServiceKey key) where TService : class;
    TService? TryInject<TService>(ServiceKey key) where TService : class;
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
    IDisposable Provide<TService>(ServiceKey key, string name, TService instance) where TService : class;
    /// <summary>解析具名 seam 实现；不存在与被过滤时返回相同 NOT_FOUND。</summary>
    TService Inject<TService>(ServiceKey key, string name) where TService : class;
}

/// <summary>目录与执行共用的注册描述；Name 为空表示键本身，具名 seam 使用独立名称。</summary>
public sealed record Registration(ServiceKey Key, string Name, ScopeKey Scope, string OwnerEntryId, object Value);

/// <summary>某次 Apply 的状态；失败保留原异常供管理面查询。</summary>
public enum PluginState { Pending, Applying, Ready, Failed }

/// <summary>当前服务尚不可见。不存在与被过滤的服务返回相同错误。</summary>
public sealed class ServiceNotReadyException : InvalidOperationException
{
    public ServiceNotReadyException() : base("服务不存在或尚未就绪。") { }
    public QianError Error { get; } = new(PlatformErrors.NotFound);
}
