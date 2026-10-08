using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qian.Runtime;

internal sealed partial class PluginContext : IPluginContext, IKeyedServiceProvider
{
    internal readonly PluginLoader Loader;
    internal readonly EffectStack Effects = new();
    internal bool Active;
    internal bool Closed;

    internal PluginContext(PluginLoader loader, PluginEntry entry)
    {
        Loader = loader;
        Entry = entry;
    }

    public PluginEntry Entry { get; }
    public ScopeKey Scope => Entry.Scope;
    public IServiceProvider Services => this;
    public ILogger Logger => Loader.Logger;

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
