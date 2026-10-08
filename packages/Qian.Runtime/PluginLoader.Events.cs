namespace Qian.Runtime;

public sealed partial class PluginLoader
{
    private readonly List<Listener> _listeners = [];
    private long _listenerSequence;

    internal Listener Listen(PluginContext context, object definition, string name, Delegate handler, int priority)
    {
        CheckDefinition(definition, name);
        var ordinal = context.Root.ListenerOrdinals.GetValueOrDefault(name);
        context.Root.ListenerOrdinals[name] = ordinal + 1;
        var listener = new Listener(context, definition, name, handler, priority, ++_listenerSequence, $"{context.Entry.Id}/{ordinal}");
        _listeners.Add(listener);
        return listener;
    }

    internal void Unlisten(Listener listener)
    {
        lock (Sync) _listeners.Remove(listener);
    }

    internal Listener[] Listeners(object definition, string name, ScopeKey scope)
    {
        CheckDefinition(definition, name);
        return _listeners.Where(l => l.Name == name && ListenerVisible(l, scope))
            .OrderByDescending(l => l.Priority).ThenBy(l => l.Sequence).ToArray();
    }

    internal bool ListenerVisible(Listener listener, ScopeKey scope)
    {
        if (!_listeners.Contains(listener) || !IsActive(listener.Context)) return false;
        if (listener.Context.Scope == scope && scope != ScopeKey.Global) return true;
        return listener.Context.Scope == ScopeKey.Global && Kept(Describe(listener), scope)
            && !_listeners.Any(l => l.Context.Scope == scope && scope != ScopeKey.Global && IsActive(l.Context)
                && l.Name == listener.Name && l.RegistrationName == listener.RegistrationName);
    }

    private void CheckDefinition(object definition, string name)
    {
        var existing = _listeners.FirstOrDefault(l => l.Name == name);
        if (existing is not null && !ReferenceEquals(existing.Definition, definition)) throw Conflict(name, existing.Context.Entry.Id);
    }

    internal sealed record Listener(PluginContext Context, object Definition, string Name, Delegate Handler, int Priority, long Sequence, string RegistrationName);
}
