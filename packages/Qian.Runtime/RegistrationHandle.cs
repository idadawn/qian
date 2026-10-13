namespace Qian.Runtime;

// 当前注册只在装载器锁内读写；撤销经效果栈登记，主动撤销与卸载都只执行一次。
internal sealed class RegistrationHandle<TService> : IRegistrationHandle<TService> where TService : class
{
    private readonly PluginContext _owner;
    private readonly Disposer _revoke;
    private PluginLoader.ServiceRegistration _current;
    private bool _revoked;

    internal RegistrationHandle(PluginContext owner, PluginLoader.ServiceRegistration registration)
    {
        _owner = owner;
        _current = registration;
        _revoke = owner.Effects.Track(Revoke);
    }

    public void Replace(TService instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        lock (_owner.Loader.Sync)
        {
            _owner.EnsureOpen();
            ObjectDisposedException.ThrowIf(_revoked, this);
            _current = _owner.Loader.Replace(_current, typeof(TService), instance);
        }
    }

    public void Dispose() => _revoke.Dispose();

    private void Revoke()
    {
        lock (_owner.Loader.Sync)
        {
            _revoked = true;
            _owner.Loader.Remove(_current);
        }
    }
}
