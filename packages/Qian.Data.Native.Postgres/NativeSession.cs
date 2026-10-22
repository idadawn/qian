using System.Data;
using System.Data.Common;
using Npgsql;
using Qian.Runtime;

namespace Qian.Data.Native.Postgres;

internal sealed class NativeSession : INativeDataSession
{
    private readonly NpgsqlConnection _connection;
    private readonly IPluginContext? _caller;
    private readonly CancellationTokenSource _stop;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly NativeConnection _view;
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private NativeTransaction? _transaction;
    private int _disposed;
    private volatile bool _failed;
    internal NativeSession(NativeDataService owner, OpenBinding binding, NpgsqlConnection connection, string? tenant,
        IPluginContext? caller, CancellationToken lifetime)
    {
        Owner = owner; Binding = binding; _connection = connection; TenantId = tenant; _caller = caller;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(lifetime); _view = new(this, connection);
    }
    internal NativeDataService Owner { get; }
    internal OpenBinding Binding { get; }
    public string? TenantId { get; }
    public DbConnection Connection { get { Guard(); return _view; } }
    public DbTransaction? Transaction { get { Guard(); return _transaction is { Completed: false } ? _transaction : null; } }
    internal NpgsqlTransaction? RawTransaction => _transaction is { Completed: false } active ? active.Raw : null;
    internal void Guard()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_failed) throw NativeErrors.Denied();
        Owner.CheckContext(TenantId, _caller);
    }
    internal async Task InitializeAsync(CancellationToken ct)
    {
        Guard();
        await Security.SetTenantAsync(_connection, null, TenantId, local: false, ct).ConfigureAwait(false);
        await Security.CheckAsync(_connection, null, Binding, TenantId, checkTenant: true, ct).ConfigureAwait(false);
        Guard();
    }
    public async ValueTask ValidateAsync(CancellationToken cancellationToken = default) =>
        await RunAsync(async ct => { await CheckAsync(ct).ConfigureAwait(false); return 0; }, cancellationToken, validate: false).ConfigureAwait(false);
    private async Task CheckAsync(CancellationToken ct)
    {
        Guard();
        await Security.CheckAsync(_connection, RawTransaction, Binding, TenantId, checkTenant: true, ct).ConfigureAwait(false);
        Guard();
    }
    internal async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct, bool validate = true)
    {
        Guard();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _mutex.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Guard(); if (validate) await CheckAsync(linked.Token).ConfigureAwait(false);
            try { return await operation(linked.Token).ConfigureAwait(false); }
            catch { _failed = true; throw; }
        }
        finally { _mutex.Release(); }
    }
    public async ValueTask<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default) =>
        await RunAsync<DbTransaction>(async ct =>
        {
            if (RawTransaction is not null) throw NativeErrors.Denied();
            var transaction = await _connection.BeginTransactionAsync(isolationLevel, ct).ConfigureAwait(false);
            _transaction = new NativeTransaction(this, transaction, isolationLevel);
            await Security.SetTenantAsync(_connection, transaction, TenantId, local: true, ct).ConfigureAwait(false);
            await CheckAsync(ct).ConfigureAwait(false);
            return _transaction;
        }, cancellationToken).ConfigureAwait(false);
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsync(async ct =>
            {
                var transaction = _transaction is { Completed: false } value ? value : throw NativeErrors.Denied();
                await transaction.Raw.CommitAsync(ct).ConfigureAwait(false); transaction.Completed = true;
                await transaction.Raw.DisposeAsync().ConfigureAwait(false); _transaction = null;
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch { _failed = true; throw; } // 等待锁或数据库边界校验阶段取消，也必须封存。
    }
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        // 清理不要求租户上下文仍相同；它只能撤销当前事务，不能继续读写。
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await RollbackCore(cancellationToken).ConfigureAwait(false); }
        finally { _mutex.Release(); }
    }
    private async Task RollbackCore(CancellationToken ct)
    {
        if (_transaction is not { Completed: false } transaction) return;
        try { await transaction.Raw.RollbackAsync(ct).ConfigureAwait(false); }
        catch { _failed = true; throw; }
        finally { transaction.Completed = true; await transaction.Raw.DisposeAsync().ConfigureAwait(false); _transaction = null; }
    }
    internal NpgsqlCommand Command()
    {
        Guard(); return _connection.CreateCommand();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { await _closed.Task.ConfigureAwait(false); return; }
        await _stop.CancelAsync().ConfigureAwait(false);
        await _mutex.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            try
            {
                await RollbackCore(CancellationToken.None).ConfigureAwait(false);
                if (_connection.State == ConnectionState.Open)
                {
                    // 不走公开守卫：卸载或租户已切换时仍必须清理；超时则清池，不能把持锁连接交给下一个租户。
                    await using var cleanup = new NpgsqlCommand("SELECT pg_catalog.pg_advisory_unlock_all(); RESET ALL; RESET ROLE; DISCARD TEMP;", _connection) { CommandTimeout = 5 };
                    await cleanup.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else NpgsqlConnection.ClearPool(_connection);
            }
            catch { NpgsqlConnection.ClearPool(_connection); }
            finally { await _connection.DisposeAsync().ConfigureAwait(false); }
        }
        finally { Owner.Closed(this); _mutex.Release(); _stop.Dispose(); _closed.TrySetResult(); }
    }
}
