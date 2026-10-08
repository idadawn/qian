namespace Qian.Runtime;

internal sealed class EffectStack
{
    private readonly List<Func<ValueTask>> _effects = [];
    private bool _closed;

    public void Add(Func<ValueTask> effect)
    {
        lock (_effects)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            _effects.Add(effect);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Func<ValueTask>[] effects;
        lock (_effects)
        {
            if (_closed) return;
            _closed = true;
            effects = _effects.ToArray();
            _effects.Clear();
        }
        List<Exception> failures = [];
        foreach (var effect in effects.Reverse())
        {
            try { await effect().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count != 0) throw new AggregateException("副作用撤销失败。", failures);
    }
}

internal sealed class Disposer(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;
    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}
