using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qian.Runtime;

internal sealed partial class PluginContext : IPluginContext, IKeyedServiceProvider
{
    internal readonly PluginLoader Loader;
    private readonly EffectStack _effects = new();
    private bool _active;
    private bool _closed;
    internal PluginContext Root { get; }
    internal EffectStack Effects => Root._effects;
    internal bool Active { get => Root._active; set => Root._active = value; }
    internal bool Closed { get => Root._closed; set => Root._closed = value; }

    internal PluginContext(PluginLoader loader, PluginEntry entry)
    {
        Loader = loader;
        Entry = entry;
        Scope = entry.Scope;
        Root = this;
    }

    private PluginContext(PluginContext parent, ScopeKey scope)
    {
        Loader = parent.Loader;
        Entry = parent.Entry;
        Root = parent.Root;
        Scope = scope;
    }

    public PluginEntry Entry { get; }
    public ScopeKey Scope { get; }
    public IServiceProvider Services => this;
    public ILogger Logger => Loader.Logger;

    public void Effect(IDisposable disposer)
    {
        ArgumentNullException.ThrowIfNull(disposer);
        Effect(disposer.Dispose);
    }

    public void Effect(Action dispose)
    {
        ArgumentNullException.ThrowIfNull(dispose);
        Effect(() => { dispose(); return ValueTask.CompletedTask; });
    }

    public void Effect(Func<ValueTask> disposeAsync)
    {
        ArgumentNullException.ThrowIfNull(disposeAsync);
        lock (Loader.Sync)
        {
            EnsureOpen();
            Effects.Add(disposeAsync);
        }
    }

    public IDisposable Provide<TService>(ServiceKey key, TService instance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (Loader.Sync)
        {
            EnsureOpen();
            var registration = Loader.Provide(this, key, typeof(TService), instance);
            var disposer = new Disposer(() => Loader.Remove(registration));
            Effects.Add(() => { disposer.Dispose(); return ValueTask.CompletedTask; });
            return disposer;
        }
    }

    public TService Inject<TService>(ServiceKey key) where TService : class
        => TryInject<TService>(key) ?? throw new ServiceNotReadyException();

    public TService? TryInject<TService>(ServiceKey key) where TService : class
    {
        lock (Loader.Sync)
        {
            EnsureOpen();
            return Loader.Find(key, Scope, this)?.Descriptor.KeyedImplementationInstance as TService;
        }
    }

    public async ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class
    {
        while (true)
        {
            Task changed;
            lock (Loader.Sync)
            {
                ct.ThrowIfCancellationRequested();
                var value = TryInject<TService>(key);
                if (value is not null) return value;
                changed = Loader.Changed;
            }
            await changed.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public object? GetService(Type serviceType) => Loader.HostServices.GetService(serviceType);

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        lock (Loader.Sync)
        {
            EnsureOpen();
            if (serviceKey is not ServiceKey key) return null;
            var value = Loader.Find(key, Scope, this)?.Descriptor.KeyedImplementationInstance;
            return value is not null && serviceType.IsInstanceOfType(value) ? value : null;
        }
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey) ?? throw new ServiceNotReadyException();

    internal void EnsureOpen() => ObjectDisposedException.ThrowIf(Closed, this);
}
