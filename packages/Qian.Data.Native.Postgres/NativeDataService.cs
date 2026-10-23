using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Kun.Core.MultiTenancy;
using Npgsql;
using Qian.Runtime;

namespace Qian.Data.Native.Postgres;

internal sealed class NativeDataService(ScopeKey ownerScope, ScopeKey ownerEntryScope, ITenantContext tenant) : INativeDataService, IAsyncDisposable
{
    private readonly Dictionary<string, OpenBinding> _bindings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<NativeSession, byte> _sessions = new();
    private readonly ConcurrentDictionary<Task, byte> _openingCleanup = new();
    private readonly ConcurrentQueue<Exception> _openingCleanupErrors = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lifecycle = new();
    private readonly TaskCompletionSource _opensDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _opening;
    private int _disposed;

    internal async Task InitializeAsync(IEnumerable<NativeDataBinding> bindings, CancellationToken ct)
    {
        var lifetime = Enter();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime);
            foreach (var binding in bindings)
            {
                NpgsqlConnectionStringBuilder settings;
                try { settings = new(await binding.ConnectionString(linked.Token).ConfigureAwait(false)); }
                catch (Exception error) when (error is not OperationCanceledException) { throw NativeErrors.Invalid(); }
                if (settings.NoResetOnClose || settings.Multiplexing || !string.IsNullOrEmpty(settings.Options) || settings.Username != binding.Role ||
                    string.IsNullOrEmpty(settings.Database) || string.IsNullOrEmpty(settings.Host)) throw NativeErrors.Invalid();
                settings.Enlist = false; // 原生会话只允许显式事务，不能被 TransactionScope 隐式接管。
                var source = new NpgsqlSlimDataSourceBuilder(settings.ConnectionString).Build();
                var open = new OpenBinding(binding, source, settings.Host, settings.Port, settings.Database,
                    settings.Pooling ? new NativePoolAdmission(settings.MaxPoolSize, settings.Timeout) : null);
                _bindings.Add(binding.Name, open);
                await using var connection = await source.OpenConnectionAsync(linked.Token).ConfigureAwait(false);
                await Security.CheckAsync(connection, null, open, null, checkTenant: false, linked.Token).ConfigureAwait(false);
            }
            var tenantRoles = _bindings.Values.Where(item => item.Binding.Access == NativeDataAccess.Tenant).Select(item => item.Binding.Role).ToHashSet(StringComparer.Ordinal);
            if (_bindings.Values.Any(item => item.Binding.Access == NativeDataAccess.Platform && tenantRoles.Contains(item.Binding.Role))) throw NativeErrors.Invalid();
        }
        finally { Exit(); }
    }

    public ValueTask<INativeDataSession> OpenTenantAsync(string binding, CancellationToken cancellationToken = default) =>
        OpenAsync(binding, NativeDataAccess.Tenant, null, cancellationToken);
    public ValueTask<INativeDataSession> OpenPlatformAsync(string binding, IPluginContext caller, CancellationToken cancellationToken = default) =>
        OpenAsync(binding, NativeDataAccess.Platform, caller ?? throw new ArgumentNullException(nameof(caller)), cancellationToken);

    private async ValueTask<INativeDataSession> OpenAsync(string name, NativeDataAccess access, IPluginContext? caller, CancellationToken ct)
    {
        var binding = Find(name, access); var id = access == NativeDataAccess.Tenant ? Tenant() : null;
        CheckContext(id, caller);
        var lifetime = Enter();
        NativeConnectionLease? connection = null; NativeSession? session = null; Task? pendingCleanup = null;
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime);
            connection = binding.Admission is { } admission
                ? await admission.OpenAsync(binding.Source, cleanup => pendingCleanup = cleanup, cancellation.Token).ConfigureAwait(false)
                : new NativeConnectionLease(await binding.Source.OpenConnectionAsync(cancellation.Token).ConfigureAwait(false), null);
            CheckContext(id, caller);
            session = new NativeSession(this, binding, connection, id, caller, lifetime);
            _sessions.TryAdd(session, 0);
            await session.InitializeAsync(cancellation.Token).ConfigureAwait(false);
            return session;
        }
        catch
        {
            if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
            else if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (pendingCleanup is null) Exit();
            else TrackOpeningCleanup(pendingCleanup);
        }
    }

    private void TrackOpeningCleanup(Task cleanup)
    {
        _openingCleanup.TryAdd(cleanup, 0);
        // 此任务接管当前 Enter 计数；卸载仍在 _opensDrained 上等待，不能在 API 早退时 Exit。
        _ = cleanup.ContinueWith(completed =>
        {
            if (completed.Exception is { } error) _openingCleanupErrors.Enqueue(error);
            else if (completed.IsCanceled) _openingCleanupErrors.Enqueue(new TaskCanceledException(completed));
            _openingCleanup.TryRemove(completed, out _);
            Exit();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public async ValueTask ValidateTenantTransactionAsync(string binding, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var declared = Find(binding, NativeDataAccess.Tenant); var id = Tenant(); CheckContext(id, null);
        if (transaction.IsolationLevel != IsolationLevel.Serializable) throw NativeErrors.Denied();
        if (transaction is NativeTransaction guarded)
        {
            if (guarded.Session.Owner != this || guarded.Session.Binding != declared || guarded.Completed || guarded.Session.TenantId != id) throw NativeErrors.Denied();
            await guarded.Session.ValidateAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        if (transaction is not NpgsqlTransaction actual || actual.Connection is not { } connection ||
            System.Transactions.Transaction.Current is not null) throw NativeErrors.Denied();
        var endpoint = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (endpoint.Host != declared.Host || endpoint.Port != declared.Port || endpoint.Database != declared.Database || endpoint.Username != declared.Binding.Role ||
            endpoint.NoResetOnClose || endpoint.Multiplexing) throw NativeErrors.Denied();
        await Security.CheckAsync(connection, actual, declared, id, checkTenant: true, cancellationToken, serializable: true).ConfigureAwait(false);
        CheckContext(id, null);
    }

    internal void CheckContext(string? capturedTenant, IPluginContext? caller)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (ownerScope != ScopeKey.Global || ownerEntryScope != ScopeKey.Global || System.Transactions.Transaction.Current is not null) throw NativeErrors.Denied();
        if (capturedTenant is not null)
        {
            if (caller is not null || tenant.TenantId != capturedTenant) throw NativeErrors.Denied();
        }
        else if (tenant.TenantId is not null || caller is null || caller.Scope != ScopeKey.Global || caller.Entry.Scope != ScopeKey.Global ||
            !ReferenceEquals(caller.TryInject<INativeDataService>(NativeDataDefinition.Key), this)) throw NativeErrors.Denied();
    }
    private string Tenant() => tenant.TenantId is { } id && Guid.TryParseExact(id, "D", out var parsed) && parsed != Guid.Empty && id == parsed.ToString("D")
        ? id : throw NativeErrors.Denied();
    private OpenBinding Find(string name, NativeDataAccess access)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _bindings.TryGetValue(name, out var binding) && binding.Binding.Access == access ? binding : throw NativeErrors.Denied();
    }
    internal void Closed(NativeSession session) => _sessions.TryRemove(session, out _);
    private CancellationToken Enter()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _opening++;
            return _stop.Token;
        }
    }
    private void Exit()
    {
        lock (_lifecycle) { if (--_opening == 0 && _disposed != 0) _opensDrained.TrySetResult(); }
    }
    public async ValueTask DisposeAsync()
    {
        bool alreadyClosed; Task opening;
        lock (_lifecycle)
        {
            alreadyClosed = _disposed != 0;
            Volatile.Write(ref _disposed, 1);
            opening = _opening == 0 ? Task.CompletedTask : _opensDrained.Task;
        }
        if (alreadyClosed) { await _closed.Task.ConfigureAwait(false); return; }
        try
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            await opening.ConfigureAwait(false);
            foreach (var session in _sessions.Keys) await session.DisposeAsync().ConfigureAwait(false);
            foreach (var binding in _bindings.Values)
            {
                try { await binding.Source.DisposeAsync().ConfigureAwait(false); }
                finally { binding.Admission?.Dispose(); }
            }
            if (!_openingCleanupErrors.IsEmpty) throw new AggregateException("原生连接打开取消后的清理失败。", _openingCleanupErrors);
        }
        finally { _stop.Dispose(); _closed.TrySetResult(); }
    }
}

internal sealed record OpenBinding(NativeDataBinding Binding, NpgsqlDataSource Source, string Host, int Port, string Database, NativePoolAdmission? Admission);
