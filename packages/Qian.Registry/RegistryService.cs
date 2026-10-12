using Kun.Core.MultiTenancy;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry;

internal sealed class RegistryService : IRegistryService
{
    private readonly RegistryRuntimeOptions _options;
    private readonly IDataService _data;
    private readonly ITenantContext _tenant;
    private readonly IPluginContext _context;
    private readonly RegistryScopeReconciler _scopes;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _control = new(1, 1);
    private PostgresPatchListener? _listener;
    private bool _disposed;

    internal RegistryService(RegistryRuntimeOptions options, IDataService data, ITenantContext tenant, IPluginContext context)
    {
        _options = options; _data = data; _tenant = tenant; _context = context;
        var repository = new PostgresPatchRepository(data, tenant, options.Surfaces);
        var composer = new RegistryProfileComposer(repository, options.Profiles, options.Profile);
        _scopes = new RegistryScopeReconciler(tenant, composer, options.Runtime, context, options.Resolve);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _control.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_listener is not null) return;
            if (_options.Runtime.DumpConfig().Any(entry => entry.Scope == ScopeKey.Global && _options.Runtime.GetState(entry.Id, entry.Scope) != PluginState.Ready))
                throw new InvalidOperationException("Registry 只能在初始插件树 Ready 后启动监听。");
            _listener = await PostgresPatchListener.StartAsync(_data, _tenant, _scopes.RefreshAllAsync, _context.Logger, linked.Token).ConfigureAwait(false);
            if (_lifetime.IsCancellationRequested) { await _listener.DisposeAsync().ConfigureAwait(false); _listener = null; linked.Token.ThrowIfCancellationRequested(); }
        }
        finally { _control.Release(); }
    }

    public async ValueTask SynchronizeAsync(ScopeKey scope, CancellationToken cancellationToken = default)
    {
        if (_disposed || _lifetime.IsCancellationRequested || _listener is null) throw new ServiceNotReadyException();
        // 不能因为调用方传了 app/session id 就推断归属；这些种类待 Tenancy 的真实 lineage 接入。
        if (!_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (scope.Kind != "tenant" || scope.Id != _tenant.TenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        await _scopes.ActivateAsync(_tenant.TenantId, scope, cancellationToken).ConfigureAwait(false);
    }

    internal void Cancel()
    {
        _lifetime.Cancel(); _listener?.Cancel(); _scopes.Cancel();
    }

    internal async ValueTask CancelAndDrainAsync()
    {
        Cancel();
        await _control.WaitAsync().ConfigureAwait(false);
        try { if (_listener is not null) await _listener.DisposeAsync().ConfigureAwait(false); }
        finally { _control.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        await _control.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_listener is not null) await _listener.DisposeAsync().ConfigureAwait(false);
            await _scopes.DisposeAsync().ConfigureAwait(false);
        }
        finally { _control.Release(); }
    }
}
