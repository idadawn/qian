using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qian.Identity.Server;

// 会话中心的后台处理器：处理吊销旁路（协议授权族、后通道登出、Redis 索引、集成事件），并周期性对账租户状态。
// 被吊销的会话已经在数据库里即时失效；这里只负责把「失效」传播出去。进程内的吊销写入后经 SessionSignal 立即唤醒，其余靠 1 秒轮询兜底，
// 多副本之间由行锁 SKIP LOCKED 分担，同一会话的事件只会发布一次。
internal sealed partial class IdentitySessionProcessor(IdentityProtocolGate protocol, IdentityAccountGate accounts, SessionSignal signal, ILogger<IdentitySessionProcessor> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextReconcile = 0L;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (protocol.Active && accounts.Service is { } service)
                {
                    var processed = await service.ProcessRevocationsAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                    if (Environment.TickCount64 >= nextReconcile)
                    {
                        nextReconcile = Environment.TickCount64 + (long)service.ReconcileInterval.TotalMilliseconds;
                        await service.ReconcileTenantsAsync(stoppingToken).ConfigureAwait(false);
                    }
                    if (processed == BatchSize) continue;
                }
                await signal.WaitAsync(Idle, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                CycleFailed(logger, error.GetType().Name);
                await Task.Delay(Idle, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "会话中心的后台处理失败，下一轮重试；ErrorType={ErrorType}")]
    private static partial void CycleFailed(ILogger logger, string errorType);
}
