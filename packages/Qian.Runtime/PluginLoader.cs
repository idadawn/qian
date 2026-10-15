using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Runtime;

/// <summary>按服务依赖装载插件。代码实例由宿主提供，不加载动态程序集、不读数据库。</summary>
public sealed partial class PluginLoader : IAsyncDisposable
{
    private static readonly Action<ILogger, string, Exception?> s_cleanupFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(1, "CleanupFailed"), "插件 {EntryId} 的副作用撤销失败");
    internal readonly object Sync = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly List<Node> _nodes = [];
    private readonly List<ServiceRegistration> _services = [];
    private TaskCompletionSource _changed = NewSignal();
    private Task _pending = Task.CompletedTask;
    private bool _disposed;
    private long _sequence;
    private readonly Timer _pendingMonitor;

    /// <summary>使用宿主 DI 与日志器创建装载器；装载器不拥有宿主 DI 的生命周期。</summary>
    public PluginLoader(IServiceProvider services, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        HostServices = services;
        Logger = logger ?? NullLogger.Instance;
        _pendingMonitor = new Timer(_ => ReportPending(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
    }

    internal IServiceProvider HostServices { get; }
    internal ILogger Logger { get; }
    internal Task Changed => _changed.Task;

    /// <summary>取得当前挂载树的配置快照；已卸载或正在移除的条目不进入管理面 dump-config。</summary>
    public IReadOnlyList<PluginEntry> DumpConfig()
    {
        lock (Sync) return _nodes.Where(n => !n.Removing).Select(n => n.Entry).ToArray();
    }

    /// <summary>查询条目的当前状态。</summary>
    public PluginState GetState(string id, ScopeKey scope)
    {
        lock (Sync) return FindNode(id, scope).State;
    }

    /// <summary>查询 Apply 或清理失败；仅用于管理诊断，不作为业务响应原文。</summary>
    public Exception? GetFailure(string id, ScopeKey scope)
    {
        lock (Sync) return FindNode(id, scope).Failure;
    }

    /// <summary>管理面诊断当前条目等待的服务键。</summary>
    public IReadOnlyList<ServiceKey> GetPendingDependencies(string id, ScopeKey scope)
    {
        lock (Sync) return FindNode(id, scope).Inject.Where(k => Find(k, scope) is null).ToArray();
    }

    /// <summary>取得当前 Apply 的上下文；重新 Apply 后旧上下文作废。</summary>
    public IPluginContext GetContext(string id, ScopeKey scope)
    {
        lock (Sync) return FindNode(id, scope).Context ?? throw new ServiceNotReadyException();
    }

    /// <summary>一次提交完整依赖集合；缺失键与环路在任何 Apply 之前失败。</summary>
    public async ValueTask MountAsync(IEnumerable<PluginDefinition> definitions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        List<Node> added = [];
        try
        {
            lock (Sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                added = definitions.Select(d => new Node(d)).ToList();
                foreach (var node in added)
                {
                    node.Entry.Scope.Validate();
                    ArgumentException.ThrowIfNullOrWhiteSpace(node.Entry.Id);
                    if (_nodes.Concat(added.Where(n => !ReferenceEquals(n, node))).Any(n => n.Entry.Id == node.Entry.Id && n.Entry.Scope == node.Entry.Scope))
                        throw Conflict(node.Entry.Id, node.Entry.Id);
                }
                ValidateGraph(_nodes.Concat(added).ToList(), added);
                _nodes.AddRange(added);
            }
            await ReconcileCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            lock (Sync)
            {
                foreach (var node in added)
                {
                    node.Removing = true;
                    if (node.Context is not null) node.Context.Active = false;
                }
                InvalidateDependents();
            }
            await CleanPendingAsync().ConfigureAwait(false);
            foreach (var node in added.OrderByDescending(n => n.Sequence)) await StopAsync(node).ConfigureAwait(false);
            lock (Sync) { foreach (var node in added) _nodes.Remove(node); Signal(); }
            throw;
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>配置重载：先卸载旧条目，再以新配置 Apply；不支持替换已加载的代码程序集。</summary>
    public async ValueTask ReloadAsync(PluginDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        await UnmountAsync(definition.Entry.Id, definition.Entry.Scope, ct).ConfigureAwait(false);
        await MountAsync([definition], ct).ConfigureAwait(false);
    }

    /// <summary>先撤销依赖者，再逆序撤销条目效果；可用其它 Provider 恢复的依赖者随后重新 Apply。</summary>
    public async ValueTask UnmountAsync(string id, ScopeKey scope, CancellationToken ct = default)
    {
        await _lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Node node;
            lock (Sync)
            {
                node = FindNode(id, scope);
                node.Removing = true;
                if (node.Context is not null) node.Context.Active = false;
                InvalidateDependents();
            }
            await CleanPendingAsync().ConfigureAwait(false);
            await StopAsync(node).ConfigureAwait(false);
            lock (Sync) { _nodes.Remove(node); Signal(); }
            await ReconcileCoreAsync(ct).ConfigureAwait(false);
        }
        finally { _lifecycle.Release(); }
    }

    /// <summary>等待通过 IDisposable 撤销注册后触发的异步级联卸载与恢复。</summary>
    public async ValueTask WhenIdleAsync(CancellationToken ct = default)
    {
        while (true)
        {
            Task pending;
            lock (Sync) pending = _pending;
            await pending.WaitAsync(ct).ConfigureAwait(false);
            lock (Sync) if (ReferenceEquals(pending, _pending)) return;
        }
    }

    /// <summary>按 Apply 顺序逆序卸载整棵树；清理失败记录在日志，继续清理其它条目。</summary>
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            Node[] nodes;
            lock (Sync)
            {
                if (_disposed) return;
                _disposed = true;
                nodes = _nodes.OrderByDescending(n => n.Sequence).ToArray();
                // 整树都将卸载，撤销 Provider 时不能再使已清空 Context 的 Ready 节点失效。
                foreach (var node in nodes) node.Removing = true;
            }
            await _pendingMonitor.DisposeAsync().ConfigureAwait(false);
            foreach (var node in nodes) await StopAsync(node).ConfigureAwait(false);
            lock (Sync) { _nodes.Clear(); Signal(); }
        }
        finally { _lifecycle.Release(); }
        await WhenIdleAsync().ConfigureAwait(false);
    }

    internal ServiceRegistration Provide(PluginContext context, ServiceKey key, Type type, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Name);
        var existing = _services.FirstOrDefault(r => r.Key == key && r.Context.Scope == context.Scope);
        if (existing is not null) throw Conflict(key.Name, existing.Context.Entry.Id);
        var registration = new ServiceRegistration(key, context, ServiceDescriptor.KeyedSingleton(type, key, value));
        _services.Add(registration);
        Notify();
        return registration;
    }

    internal void Remove(ServiceRegistration registration)
    {
        lock (Sync) if (_services.Remove(registration)) Notify();
    }

    // 调用方持有 Sync：在原位换成新注册，读取方不会看到缺失；新身份让依赖者按撤销后再登记的语义失效。
    internal ServiceRegistration Replace(ServiceRegistration current, Type type, object value)
    {
        var index = _services.FindIndex(r => ReferenceEquals(r, current));
        ObjectDisposedException.ThrowIf(index < 0, current);
        var replacement = current with { Descriptor = ServiceDescriptor.KeyedSingleton(type, current.Key, value) };
        _services[index] = replacement;
        Notify();
        return replacement;
    }

    internal ServiceRegistration? Find(ServiceKey key, ScopeKey scope, PluginContext? requester = null)
        => VisibleServices(scope, requester).FirstOrDefault(r => r.Key == key);

    private static void ValidateGraph(List<Node> all, List<Node> added)
    {
        Dictionary<Node, Node[]> edges = [];
        foreach (var node in all)
        {
            List<Node> dependencies = [];
            foreach (var key in node.Inject)
            {
                var provider = all.FirstOrDefault(n => n.Entry.Scope == node.Entry.Scope && n.Provides.Contains(key))
                    ?? all.FirstOrDefault(n => n.Entry.Scope == ScopeKey.Global && n.Provides.Contains(key));
                if (provider is null)
                {
                    if (!added.Contains(node)) continue; // 既有 Pending 条目允许暂时缺少已撤销的 Provider。
                    throw new QianErrorException(new QianError(PlatformErrors.DependencyMissing,
                        new Dictionary<string, string> { ["key"] = key.Name, ["entry"] = node.Entry.Id })
                    { Pointer = ErrorPointer.Input("entries", node.Entry.Id, "inject") });
                }
                dependencies.Add(provider);
            }
            edges[node] = dependencies.ToArray();
        }
        HashSet<Node> visited = [];
        List<Node> path = [];
        void Visit(Node node)
        {
            var index = path.IndexOf(node);
            if (index >= 0)
                throw new QianErrorException(new QianError(PlatformErrors.DependencyCycle,
                    new Dictionary<string, string> { ["cycle"] = string.Join(" → ", path.Skip(index).Append(node).Select(n => n.Entry.Id)) })
                { Pointer = ErrorPointer.Input("entries", node.Entry.Id, "inject") });
            if (!visited.Add(node)) return;
            path.Add(node);
            foreach (var dependency in edges[node]) Visit(dependency);
            path.RemoveAt(path.Count - 1);
        }
        foreach (var node in all) Visit(node);
    }

    private async Task ReconcileCoreAsync(CancellationToken ct)
    {
        while (true)
        {
            await CleanPendingAsync().ConfigureAwait(false);
            Node? node;
            lock (Sync)
            {
                if (_disposed) return;
                // 清理快照之后，其它线程的 Replace、撤销或 restriction 变更可能又让依赖者回到 Pending；
                // 旧上下文撤销完之前不能重新 Apply，否则旧效果与旧注册泄漏，新 Apply 的 Provide 还会撞冲突。
                if (_nodes.Any(n => n.State == PluginState.Pending && n.Context is not null)) continue;
                node = _nodes.FirstOrDefault(n => !n.Removing && n.State == PluginState.Pending && n.Inject.All(k => Find(k, n.Entry.Scope) is not null));
                if (node is null) return;
                ct.ThrowIfCancellationRequested();
                node.Context = new PluginContext(this, node.Entry);
                node.Dependencies = node.Inject.Select(k => Find(k, node.Entry.Scope)!).ToArray();
                node.State = PluginState.Applying;
                node.Sequence = ++_sequence;
            }
            try
            {
                await node.Plugin.ApplyAsync(node.Context, node.Entry.Config, ct).ConfigureAwait(false);
                lock (Sync)
                {
                    if (!node.Provides.All(k => _services.Any(r => r.Key == k && r.Context.Scope == node.Entry.Scope && ReferenceEquals(r.Context.Root, node.Context))))
                        throw new InvalidOperationException($"{node.Entry.Id} 未注册声明的 provides 服务键。");
                    if (!node.Dependencies.All(d => ReferenceEquals(Find(d.Key, node.Entry.Scope, node.Context), d)))
                        throw new InvalidOperationException($"{node.Entry.Id} 在 Apply 期间改变了自身依赖的可见性。");
                    node.State = PluginState.Ready;
                    node.Context.Active = true;
                    Notify();
                }
            }
            catch (Exception error)
            {
                lock (Sync) { node.Failure = error; node.State = PluginState.Failed; }
                await StopAsync(node).ConfigureAwait(false);
                if (error is OperationCanceledException && ct.IsCancellationRequested) throw;
            }
        }
    }

    private async Task CleanPendingAsync()
    {
        Node[] nodes;
        lock (Sync) nodes = _nodes.Where(n => n.State == PluginState.Pending && n.Context is not null).OrderByDescending(n => n.Sequence).ToArray();
        foreach (var node in nodes) await StopAsync(node).ConfigureAwait(false);
    }

    private async Task StopAsync(Node node)
    {
        PluginContext? context;
        lock (Sync)
        {
            context = node.Context;
            if (context is null) return;
            context.Active = false;
            context.Closed = true;
            Signal();
        }
        try { await context.Effects.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            lock (Sync) node.Failure = node.Failure is null ? error : new AggregateException(node.Failure, error);
            s_cleanupFailed(Logger, node.Entry.Id, error);
        }
        finally { lock (Sync) node.Context = null; }
    }

    private void InvalidateDependents()
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var node in _nodes.Where(n => n.State == PluginState.Ready && !n.Removing))
            {
                if (node.Dependencies.All(d => ReferenceEquals(Find(d.Key, node.Entry.Scope), d))) continue;
                node.Context!.Active = false;
                node.State = PluginState.Pending;
                node.PendingSince = DateTimeOffset.UtcNow;
                node.PendingReported = false;
                changed = true;
            }
        } while (changed);
    }

    private void Notify()
    {
        InvalidateDependents();
        Signal();
        if (!_disposed) _pending = ReconcileAfterAsync(_pending);
    }

    private async Task ReconcileAfterAsync(Task previous)
    {
        await Task.Yield(); // 不在注册调用栈中重入 Apply 或撤销异步效果。
        await previous.ConfigureAwait(false);
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try { await ReconcileCoreAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { _lifecycle.Release(); }
    }

    private void Signal()
    {
        var previous = _changed;
        _changed = NewSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Node FindNode(string id, ScopeKey scope) => _nodes.Single(n => n.Entry.Id == id && n.Entry.Scope == scope);
    private static QianErrorException Conflict(string key, string owner) => new(new QianError(PlatformErrors.RegistrationConflict,
        new Dictionary<string, string> { ["key"] = key, ["owner"] = owner }));

    internal sealed record ServiceRegistration(ServiceKey Key, PluginContext Context, ServiceDescriptor Descriptor);

    private sealed class Node(PluginDefinition definition)
    {
        internal PluginEntry Entry { get; } = definition.Entry with { Config = definition.Entry.Config.Clone() };
        internal IQianPlugin Plugin { get; } = definition.Plugin;
        internal ServiceKey[] Inject { get; } = definition.Plugin.Inject.ToArray();
        internal ServiceKey[] Provides { get; } = definition.Provides.ToArray();
        internal PluginState State;
        internal PluginContext? Context;
        internal ServiceRegistration[] Dependencies = [];
        internal Exception? Failure;
        internal bool Removing;
        internal long Sequence;
        internal DateTimeOffset PendingSince = DateTimeOffset.UtcNow;
        internal bool PendingReported;
    }
}
