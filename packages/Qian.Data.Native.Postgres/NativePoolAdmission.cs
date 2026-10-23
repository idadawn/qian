using Npgsql;

namespace Qian.Data.Native.Postgres;

// .NET 10 的 Channels 在注册时同步取消可能丢失池归还项（dotnet/runtime#132230）。
// 将容量等待放在每绑定的租约边界，直到原始连接归还后才释放名额，避免进入驱动的取消读队列。
internal sealed class NativePoolAdmission(int capacity, int timeoutSeconds) : IDisposable
{
    private readonly SemaphoreSlim _slots = new(capacity, capacity);

    internal async ValueTask<NativeConnectionLease> OpenAsync(NpgsqlDataSource source, Action<Task> trackCleanup, CancellationToken ct)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeoutSeconds > 0) deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var acquired = false;
        var transferred = false;
        Task<NpgsqlConnection>? opening = null;
        try
        {
            await _slots.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            // 准入和物理打开共享同一个截止时间；驱动 Connecting 阶段并非处处响应取消。
            opening = source.OpenConnectionAsync(deadline.Token).AsTask();
            var connection = await opening.WaitAsync(deadline.Token).ConfigureAwait(false);
            var lease = new NativeConnectionLease(connection, _slots);
            acquired = false; // 名额的所有权随连接交给会话。
            return lease;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            if (opening is not null)
            {
                // 调用可按时返回，但许可、token 和 Service 的打开计数须保留到原始连接清理结束。
                acquired = false;
                transferred = true;
                trackCleanup(ClosePendingAsync(opening, deadline));
            }
            if (ct.IsCancellationRequested) throw;
            throw new NpgsqlException("The connection opening timeout has expired.", new TimeoutException());
        }
        finally
        {
            if (acquired) _slots.Release();
            if (!transferred) deadline.Dispose();
        }
    }

    private async Task ClosePendingAsync(Task<NpgsqlConnection> opening, CancellationTokenSource deadline)
    {
        try
        {
            NpgsqlConnection connection;
            try { connection = await opening.ConfigureAwait(false); }
            catch { return; } // 原始打开已失败；驱动的 OpenConnectionAsync 会等待其异常清理。
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            try { deadline.Dispose(); }
            finally { _slots.Release(); }
        }
    }

    public void Dispose() => _slots.Dispose();
}

internal sealed class NativeConnectionLease(NpgsqlConnection connection, SemaphoreSlim? slots) : IAsyncDisposable
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    internal NpgsqlConnection Connection => connection;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { await _closed.Task.ConfigureAwait(false); return; }
        try { await connection.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { slots?.Release(); }
            finally { _closed.TrySetResult(); }
        }
    }
}
