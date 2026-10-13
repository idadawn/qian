using Microsoft.Extensions.Logging;

namespace Qian.Runtime;

internal sealed partial class PluginContext
{
    private static readonly Action<ILogger, string, Exception?> s_observerFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(2, "ObserverFailed"), "事件 {EventName} 的观察监听器失败");

    public IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(handler);
        var valid = mode switch
        {
            DispatchMode.Emit => handler is Func<TPayload, ValueTask>,
            DispatchMode.Waterfall => handler is Func<TPayload, Func<ValueTask<TResult>>, ValueTask<TResult>>,
            DispatchMode.Bail => handler is Func<TPayload, ValueTask<TResult>>,
            _ => false,
        };
        if (!valid) throw new ArgumentException("监听器签名不符合模式；parallel / serial 的事件结果须为 IReadOnlyList<T>。", nameof(handler));
        return Listen(evt, evt.Name, evt.Mode, mode, handler, priority);
    }

    public IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (mode is not (DispatchMode.Parallel or DispatchMode.Serial) || handler is not Func<TPayload, ValueTask<TResult>>)
            throw new ArgumentException("集合分发监听器须为 Func<TPayload, ValueTask<TResult>>。", nameof(handler));
        return Listen(evt, evt.Name, evt.Mode, mode, handler, priority);
    }

    private Disposer Listen(object definition, string name, DispatchMode defined, DispatchMode requested, Delegate handler, int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (defined != requested) throw new ArgumentException("调用点的事件模式与 Definition 不一致。", nameof(requested));
        lock (Loader.Sync)
        {
            EnsureOpen();
            var listener = Loader.Listen(this, definition, name, handler, priority);
            return Effects.Track(() => Loader.Unlisten(listener));
        }
    }

    public async ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.Mode == DispatchMode.Waterfall)
        {
            var terminal = evt.Terminal ?? throw new ArgumentException("waterfall 必须指定链尾。", nameof(evt));
            return await DispatchAsync(evt, payload, () => terminal(payload), ct).ConfigureAwait(false);
        }
        if (evt.Mode is not (DispatchMode.Emit or DispatchMode.Bail))
            throw new ArgumentException("parallel / serial 的事件结果须为 IReadOnlyList<T>。", nameof(evt));
        var listeners = Snapshot(evt, evt.Name, ct);
        foreach (var listener in listeners)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsVisible(listener)) continue;
            if (evt.Mode == DispatchMode.Emit)
            {
                try { await ((Func<TPayload, ValueTask>)listener.Handler)(payload).ConfigureAwait(false); }
                catch (Exception error) { s_observerFailed(Logger, evt.Name, error); }
            }
            else
            {
                var result = await ((Func<TPayload, ValueTask<TResult>>)listener.Handler)(payload).ConfigureAwait(false);
                if (result is not null) return result;
            }
        }
        return default!;
    }

    public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, Func<ValueTask<TResult>> terminal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(terminal);
        if (evt.Mode != DispatchMode.Waterfall) throw new ArgumentException("只有 waterfall 接受链尾。", nameof(evt));
        var listeners = Snapshot(evt, evt.Name, ct);
        ValueTask<TResult> Next(int index)
        {
            ct.ThrowIfCancellationRequested();
            while (index < listeners.Length && !IsVisible(listeners[index])) index++;
            if (index == listeners.Length) return terminal();
            var handler = (Func<TPayload, Func<ValueTask<TResult>>, ValueTask<TResult>>)listeners[index].Handler;
            return handler(payload, () => Next(index + 1));
        }
        return Next(0);
    }

    public async ValueTask<IReadOnlyList<TResult>> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, TPayload payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.Mode is not (DispatchMode.Parallel or DispatchMode.Serial)) throw new ArgumentException("集合结果只用于 parallel / serial。", nameof(evt));
        var listeners = Snapshot(evt, evt.Name, ct);
        if (evt.Mode == DispatchMode.Serial)
        {
            List<TResult> results = [];
            foreach (var listener in listeners)
            {
                ct.ThrowIfCancellationRequested();
                if (IsVisible(listener)) results.Add(await ((Func<TPayload, ValueTask<TResult>>)listener.Handler)(payload).ConfigureAwait(false));
            }
            return results.AsReadOnly();
        }
        async Task<(bool Success, TResult? Value, Exception? Error)> Invoke(PluginLoader.Listener listener)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                if (!IsVisible(listener)) return (false, default, null);
                return (true, await ((Func<TPayload, ValueTask<TResult>>)listener.Handler)(payload).ConfigureAwait(false), null);
            }
            catch (Exception error) { return (false, default, error); }
        }
        var outcomes = await Task.WhenAll(listeners.Select(Invoke)).ConfigureAwait(false);
        var partial = outcomes.Where(o => o.Success).Select(o => o.Value!).ToArray();
        var errors = outcomes.Where(o => o.Error is not null).Select(o => o.Error!).ToArray();
        if (errors.Length != 0) throw new ParallelDispatchException<TResult>(errors, partial);
        return Array.AsReadOnly(partial);
    }

    private PluginLoader.Listener[] Snapshot(object definition, string name, CancellationToken ct)
    {
        lock (Loader.Sync)
        {
            EnsureOpen();
            ct.ThrowIfCancellationRequested();
            return Loader.Listeners(definition, name, Scope);
        }
    }

    private bool IsVisible(PluginLoader.Listener listener)
    {
        lock (Loader.Sync) return Loader.ListenerVisible(listener, Scope);
    }
}
