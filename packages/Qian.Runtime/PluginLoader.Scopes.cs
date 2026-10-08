using Microsoft.Extensions.Logging;

namespace Qian.Runtime;

public sealed partial class PluginLoader
{
    private static readonly Action<ILogger, string, Exception?> s_restrictionFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(3, "RestrictionFailed"), "条目 {EntryId} 的 restriction 失败，按不可见处理");
    private readonly List<Restriction> _restrictions = [];

    internal Restriction Restrict(PluginContext owner, ScopeKey scope, Func<Registration, bool> keep)
    {
        var restriction = new Restriction(owner, scope, keep);
        _restrictions.Add(restriction);
        Notify();
        return restriction;
    }

    internal void Unrestrict(Restriction restriction)
    {
        lock (Sync) if (_restrictions.Remove(restriction)) Notify();
    }

    internal Registration[] List(ServiceKey key, ScopeKey scope, PluginContext requester)
        => VisibleServices(scope, requester).Select(Describe)
            .Concat(_listeners.Where(l => ListenerVisible(l, scope)).Select(Describe))
            .Where(r => r.Key == key).ToArray();

    private IEnumerable<ServiceRegistration> VisibleServices(ScopeKey scope, PluginContext? requester)
    {
        var visible = _services.Where(r => IsActive(r.Context, requester)).ToArray();
        var local = visible.Where(r => r.Context.Scope == scope && scope != ScopeKey.Global).ToArray();
        return local.Concat(visible.Where(r => r.Context.Scope == ScopeKey.Global
            && !local.Any(s => s.Key == r.Key) && Kept(Describe(r), scope, requester)));
    }

    private bool Kept(Registration registration, ScopeKey scope, PluginContext? requester = null)
    {
        if (registration.Scope != ScopeKey.Global) return true;
        foreach (var restriction in _restrictions.Where(r => r.Scope == scope && IsActive(r.Context, requester)))
        {
            try { if (!restriction.Keep(registration)) return false; }
            catch (Exception error)
            {
                s_restrictionFailed(Logger, restriction.Context.Entry.Id, error);
                return false;
            }
        }
        return true;
    }

    private static bool IsActive(PluginContext context, PluginContext? requester = null)
        => !context.Closed && (context.Active || ReferenceEquals(context.Root, requester?.Root));

    private static Registration Describe(ServiceRegistration service)
    {
        var parts = service.Key.Name.Split(':', 2);
        return new Registration(new ServiceKey(parts[0]), parts.Length == 2 ? parts[1] : "", service.Context.Scope,
            service.Context.Entry.Id, service.Descriptor.KeyedImplementationInstance!);
    }

    private static Registration Describe(Listener listener)
        => new(new ServiceKey(listener.Name), listener.RegistrationName, listener.Context.Scope, listener.Context.Entry.Id, listener.Handler);

    internal sealed record Restriction(PluginContext Context, ScopeKey Scope, Func<Registration, bool> Keep);
}
