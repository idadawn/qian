using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Qian.Config.Apollo;

internal static class ConfigTelemetry
{
    internal const string MeterName = "Qian.Config";
    private static readonly Meter Meter = new(MeterName);
    // 点号命名；Prometheus 导出后即 qian_config_reload_failed_total。
    internal static readonly Counter<long> ReloadFailed = Meter.CreateCounter<long>("qian.config.reload.failed", description: "强类型配置重载被校验拒绝、保留上一份有效值的次数");
}

internal sealed record OptionsRegistration(string OptionsType, string Section, string? Plugin);

// 变更令牌触发时先构造并校验候选值：失败保留上一份、不通知监听者，计数并发告警；修正后的下一次变更恢复推送（PRD 7.8、ADR-007 D7.6）。
// 与 Kun 的 LastGoodOptionsMonitor 同一语义，但不经 AddKunOptions 的反射绑定入口，且有 Qian 的指标与事件钩子。
internal sealed partial class ValidatedOptionsMonitor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
    IOptionsFactory<T> factory, IEnumerable<IOptionsChangeTokenSource<T>> sources, OptionsRegistration registration,
    ConfigReloadNotifier notifier, ILogger logger) : IOptionsMonitor<T>, IDisposable where T : class
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, T> _values = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _subscriptions = [];
    private Action<T, string?>? _listeners;
    private bool _disposed;

    // DI 工厂显式调用一次，避免主构造器初始化期间捕获尚未就绪的实例。
    internal ValidatedOptionsMonitor<T> Start()
    {
        foreach (var source in sources)
        {
            var name = source.Name ?? Options.DefaultName;
            _subscriptions.Add(ChangeToken.OnChange(source.GetChangeToken, () => Reload(name)));
        }
        return this;
    }

    public T CurrentValue => Get(Options.DefaultName);

    // 首次读取即校验：没有上一份有效值时，校验失败照常抛出 OptionsValidationException。
    public T Get(string? name)
    {
        name ??= Options.DefaultName;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_values.TryGetValue(name, out var value)) _values[name] = value = factory.Create(name);
            return value;
        }
    }

    private void Reload(string name)
    {
        T candidate;
        Action<T, string?>? notify;
        int failures;
        lock (_gate)
        {
            if (_disposed) return;
            try
            {
                candidate = factory.Create(name);
                _values[name] = candidate;
                notify = _listeners;
                failures = 0;
            }
            // 绑定器把无法转换的值报成 InvalidOperationException；配置根已释放（ObjectDisposedException）不算候选被拒。
            catch (Exception error) when (error is OptionsValidationException or FormatException or OverflowException ||
                error is InvalidOperationException and not ObjectDisposedException)
            {
                candidate = null!; notify = null;
                failures = error is OptionsValidationException validation ? Math.Max(1, validation.Failures.Count()) : 1;
            }
        }
        if (failures == 0) { notify?.Invoke(candidate, name); return; }
        var plugin = new KeyValuePair<string, object?>("plugin", registration.Plugin);
        var type = new KeyValuePair<string, object?>("options.type", registration.OptionsType);
        var named = new KeyValuePair<string, object?>("options.name", name);
        if (registration.Plugin is null) ConfigTelemetry.ReloadFailed.Add(1, type, named);
        else ConfigTelemetry.ReloadFailed.Add(1, type, named, plugin);
        LogRejected(logger, registration.OptionsType, name, registration.Section, failures);
        notifier.Publish(new ConfigReloadFailed(registration.OptionsType, name, registration.Section, failures, ConfigErrors.ReloadRejected.Code)
            { Plugin = registration.Plugin }, logger);
    }

    public IDisposable OnChange(Action<T, string?> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (_gate) _listeners += listener;
        return new Subscription(() => { lock (_gate) _listeners -= listener; });
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; _listeners = null; }
        foreach (var subscription in _subscriptions) subscription.Dispose();
    }

    private sealed class Subscription(Action remove) : IDisposable
    {
        private Action? _remove = remove;
        public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
    }

    // 只记类型、节与失败条数；校验消息可能带值，不进日志。
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "配置 {OptionsType}/{OptionsName}（节 {Section}）重载被拒绝：{Failures} 条校验未通过，保留上一份有效值。")]
    private static partial void LogRejected(ILogger logger, string optionsType, string optionsName, string section, int failures);
}

internal sealed record ReloadNotice(ConfigReloadFailed Payload);

// 监视器与挂载中的配置组件之间的进程内转发：组件挂载期间把重载失败作为 config/reload-failed 发出；没有组件挂载时只有指标与日志。
internal sealed partial class ConfigReloadNotifier
{
    private readonly Lock _gate = new();
    private readonly List<Func<ReloadNotice, ValueTask>> _handlers = [];

    internal IDisposable Subscribe(Func<ReloadNotice, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate) _handlers.Add(handler);
        return new Unsubscriber(this, handler);
    }

    // 在变更令牌回调线程上触发，不等待分发：监听器失败只记日志，不影响保留旧值的结论。
    internal void Publish(ConfigReloadFailed failure, ILogger logger)
    {
        Func<ReloadNotice, ValueTask>[] handlers;
        lock (_gate) handlers = [.. _handlers];
        var notice = new ReloadNotice(failure);
        foreach (var handler in handlers) _ = Dispatch(handler, notice, logger);
    }

    private static async Task Dispatch(Func<ReloadNotice, ValueTask> handler, ReloadNotice notice, ILogger logger)
    {
        try { await handler(notice).ConfigureAwait(false); }
        catch (Exception error) { LogDispatchFailed(logger, notice.Payload.Section, error); }
    }

    private sealed class Unsubscriber(ConfigReloadNotifier owner, Func<ReloadNotice, ValueTask> handler) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._gate) owner._handlers.Remove(handler);
        }
    }

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "配置节 {Section} 的重载失败告警未能发出。")]
    private static partial void LogDispatchFailed(ILogger logger, string section, Exception error);
}
