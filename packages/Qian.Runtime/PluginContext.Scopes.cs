using Qian.Runtime.Errors;

namespace Qian.Runtime;

internal sealed partial class PluginContext
{
    public IPluginContext Scoped(ScopeKey scope)
    {
        scope.Validate();
        lock (Loader.Sync) { EnsureOpen(); return new PluginContext(this, scope); }
    }

    public IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        scope.Validate();
        if (scope == ScopeKey.Global) throw new ArgumentException("restriction 的目标必须是非全局 scope。", nameof(scope));
        lock (Loader.Sync)
        {
            EnsureOpen();
            var restriction = Loader.Restrict(this, scope, keep);
            return Effects.Track(() => Loader.Unrestrict(restriction));
        }
    }

    public IReadOnlyList<Registration> List(ServiceKey key)
    {
        lock (Loader.Sync) { EnsureOpen(); return Loader.List(key, Scope, this); }
    }

    public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, string name, TService instance) where TService : class
        => Provide(Named(key, name), instance);

    public TService Inject<TService>(ServiceKey key, string name) where TService : class
        => TryInject<TService>(Named(key, name)) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));

    private static ServiceKey Named(ServiceKey key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (key.Name.Contains(':', StringComparison.Ordinal)) throw new ArgumentException("具名注册的 key 不能再含冒号。", nameof(key));
        return new ServiceKey($"{key.Name}:{name}");
    }
}
