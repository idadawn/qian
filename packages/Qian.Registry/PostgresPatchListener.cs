using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Data;

namespace Qian.Registry;

// 通知只唤醒重新读库；不把载荷当成新的配置或权限。一次宿主监听由服务分派到其已激活 scope。
internal sealed class PostgresPatchListener : IAsyncDisposable
{
    private static readonly Action<ILogger, string, Exception?> s_failure = LoggerMessage.Define<string>(LogLevel.Warning,
        new EventId(1, "RegistryRefreshFailed"), "注册表监听或重新读取失败，等待恢复；异常类型 {ErrorType}");
    private readonly IDataService _data;
    private readonly ITenantContext _tenant;
    private readonly Func<CancellationToken, ValueTask> _refresh;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _run;
    private readonly Lock _disposeLock = new();
    private Task? _dispose;
    private int _processId;

    private PostgresPatchListener(IDataService data, ITenantContext tenant, Func<CancellationToken, ValueTask> refresh, ILogger logger)
    {
        _data = data; _tenant = tenant; _refresh = refresh; _logger = logger;
        // 后台监听不能继承创建请求的租户或其它 AsyncLocal 状态。
        if (ExecutionContext.IsFlowSuppressed()) _run = Task.Run(RunAsync);
        else { using (ExecutionContext.SuppressFlow()) _run = Task.Run(RunAsync); }
    }

    internal int BackendProcessId => Volatile.Read(ref _processId);

    internal static async Task<PostgresPatchListener> StartAsync(IDataService data, ITenantContext tenant,
        Func<CancellationToken, ValueTask> refresh, ILogger logger, CancellationToken ct)
    {
        var listener = new PostgresPatchListener(data, tenant, refresh, logger);
        try
        {
            var started = await Task.WhenAny(listener._ready.Task, listener._run).WaitAsync(ct).ConfigureAwait(false);
            await started.ConfigureAwait(false);
            if (!listener._ready.Task.IsCompletedSuccessfully) throw new InvalidOperationException("注册表监听在就绪前退出。");
            return listener;
        }
        catch { await listener.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private async Task RunAsync()
    {
        var ct = _stop.Token;
        using var neutral = _tenant.BeginScope(null);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var session = await _data.OpenAsync<RegistryDbContext>(ct).ConfigureAwait(false);
                await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                var connection = (NpgsqlConnection)session.Context.Database.GetDbConnection();
                await session.Context.Database.ExecuteSqlRawAsync("LISTEN " + PostgresPatchRepository.ChangeChannel, ct).ConfigureAwait(false);
                Volatile.Write(ref _processId, connection.ProcessID);
                // LISTEN 生效后读取快照，覆盖连接建立之前和重连期间的所有变更。
                await RefreshAsync(ct).ConfigureAwait(false);
                _ready.TrySetResult();
                while (!ct.IsCancellationRequested)
                {
                    // pg_notify 不保证断线重放；每秒核对也覆盖连接存活但通知遗漏的情况。
                    await connection.WaitAsync(1000, ct).ConfigureAwait(false);
                    await RefreshAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { s_failure(_logger, error.GetType().Name, null); }
            finally { Volatile.Write(ref _processId, 0); }
            try { await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async ValueTask RefreshAsync(CancellationToken ct)
    {
        try
        {
            using var neutral = _tenant.BeginScope(null);
            await _refresh(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            // 配置、数据库连接串和底层错误原文都不进入日志；下一次核对继续尝试。
            s_failure(_logger, error.GetType().Name, null);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock) return new ValueTask(_dispose ??= StopAsync());
    }

    private async Task StopAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _run.ConfigureAwait(false);
        _stop.Dispose();
    }
}
