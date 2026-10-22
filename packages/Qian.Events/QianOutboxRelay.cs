using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kun.EventBus;
using Kun.EventBus.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Data;

namespace Qian.Events;

/// <summary>
/// Outbox 投递后台服务：逐个业务上下文领取到期行，发布并等待确认后立即标记。
/// 停机（SIGTERM）时先完成手上这一条，再把本批未发的行交还，其他副本马上可以接手，不会重复发布已确认的消息。
/// </summary>
internal sealed partial class QianOutboxRelay(IServiceScopeFactory scopes, IDataContextCatalog contexts, IIntegrationEventPublisher publisher,
    IOptions<QianEventsOptions> settings, TimeProvider clock, ILogger<QianOutboxRelay> logger, IServiceProvider services) : BackgroundService
{
    private readonly QianEventsOptions _options = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redis = _options.OutboxLock == OutboxLockMode.Redis
            ? new RedisOutboxLockProvider(services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>())
            : null;
        Log.Started(logger, _options.RelayInstance, contexts.ContextTypes.Count);
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            foreach (var type in contexts.ContextTypes)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try { claimed += await RelayAsync(type, redis, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error) { Log.CycleFailed(logger, error, type.Name); }
            }
            if (claimed > 0) continue;
            try { await Task.Delay(_options.PollInterval, clock, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
        Log.Stopped(logger, _options.RelayInstance);
    }

    internal async Task<int> RelayAsync(Type contextType, IOutboxLockProvider? redis, CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = (QianDbContext)scope.ServiceProvider.GetRequiredService(contextType);
        var table = OutboxTable.For(context);
        IAsyncDisposable? lease = null;
        if (redis is not null)
        {
            lease = await redis.TryAcquireAsync("qian-outbox:" + table.Schema, _options.LeaseDuration, stoppingToken).ConfigureAwait(false);
            if (lease is null) return 0;
        }
        await using (lease)
        {
            await context.Database.OpenConnectionAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                var connection = context.Database.GetDbConnection();
                // 本地单调计时从发出领取之前开始，数据库写下的租约只会更晚到期，据此判断余量是保守的。
                var claimedAt = clock.GetTimestamp();
                var batch = await table.ClaimAsync(connection, _options.LeaseDuration, _options.BatchSize, stoppingToken).ConfigureAwait(false);
                for (var index = 0; index < batch.Count; index++)
                {
                    // 停机，或剩余租约不够再等一次确认（broker 变慢时整批发不完）：交还本批未发的行。
                    // 不能拖到租约到期，否则接管的副本会与这里重复发布；交还失败时这些行也会在租约到期后被接管。
                    if (stoppingToken.IsCancellationRequested || clock.GetElapsedTime(claimedAt) + _options.PublishTimeout >= _options.LeaseDuration)
                    {
                        await table.ReleaseAsync(connection, batch.Skip(index).ToArray(), CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    await PublishAsync(table, connection, batch[index]).ConfigureAwait(false);
                }
                return batch.Count;
            }
            finally { await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
        }
    }

    // 已领取的这一条不受停机令牌影响：发布与标记都要完成，否则已确认的消息会被别的副本重发。
    private async Task PublishAsync(OutboxTable table, System.Data.Common.DbConnection connection, ClaimedMessage row)
    {
        var tags = new TagList { { "qian.events.relay", _options.RelayInstance }, { "qian.events.schema", table.Schema }, { "qian.events.type", row.EventTypeId } };
        try
        {
            using var timeout = new CancellationTokenSource(_options.PublishTimeout);
            await publisher.PublishAsync(row.ToMessage(), timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            var attempts = row.AttemptCount + 1;
            // 信封不合法时重试也不会变好，直接标为失败。
            var failed = error is QianEventsException || attempts >= _options.MaxAttempts;
            var delay = TimeSpan.FromMilliseconds(Math.Min(_options.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempts - 1, 16)), 60_000));
            await table.MarkRetryAsync(connection, row.Id, attempts, failed ? null : delay, error.GetType().Name + ": " + error.Message, CancellationToken.None).ConfigureAwait(false);
            (failed ? EventsMetrics.Failed : EventsMetrics.Retried).Add(1, tags);
            if (failed) Log.Failed(logger, error, row.Id, row.EventTypeId, attempts);
            else Log.Retrying(logger, error, row.Id, row.EventTypeId, attempts);
            return;
        }
        EventsMetrics.Confirmed.Add(1, tags);
        await table.MarkPublishedAsync(connection, row.Id, CancellationToken.None).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Outbox 投递已启动：副本 {Relay}，上下文 {Count} 个。")]
        public static partial void Started(ILogger logger, string relay, int count);

        [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Outbox 投递已停止：副本 {Relay}。")]
        public static partial void Stopped(ILogger logger, string relay);

        [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Outbox 投递一轮失败：上下文 {Context}。")]
        public static partial void CycleFailed(ILogger logger, Exception error, string context);

        [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "集成事件 {EventId}（{Type}）第 {Attempt} 次发布失败，将退避重试。")]
        public static partial void Retrying(ILogger logger, Exception error, Guid eventId, string type, int attempt);

        [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "集成事件 {EventId}（{Type}）发布失败 {Attempt} 次，已标记为失败。")]
        public static partial void Failed(ILogger logger, Exception error, Guid eventId, string type, int attempt);
    }
}

/// <summary>
/// 事件组件指标。Outbox 侧 confirmed 只在 broker 确认后计数，是「发布次数 = Outbox 行数」的依据；
/// 消费侧每条消息恰落入一个结果计数，dead_lettered 加一即告警（告警规则归 S1-DEP-006）。
/// </summary>
internal static class EventsMetrics
{
    public const string MeterName = "Qian.Events";
    private static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> Confirmed = Meter.CreateCounter<long>("qian.events.outbox.confirmed", description: "broker 已确认的集成事件条数");
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>("qian.events.outbox.retried", description: "发布失败并安排重试的条数");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("qian.events.outbox.failed", description: "发布失败并标记为失败的条数");
    public static readonly Counter<long> ConsumerProcessed = Meter.CreateCounter<long>("qian.events.consumer.processed", description: "handler 成功、已写幂等记录并确认的消息条数");
    public static readonly Counter<long> ConsumerRetried = Meter.CreateCounter<long>("qian.events.consumer.retried", description: "瞬时失败并转入重试队列退避的条数");
    public static readonly Counter<long> ConsumerDeadLettered = Meter.CreateCounter<long>("qian.events.consumer.dead_lettered", description: "重试用尽或 handler 确定性失败、已转入死信队列的条数");
    public static readonly Counter<long> ConsumerDuplicate = Meter.CreateCounter<long>("qian.events.consumer.duplicate", description: "幂等记录命中、未再执行 handler 的条数");
    public static readonly Counter<long> ConsumerRejected = Meter.CreateCounter<long>("qian.events.consumer.rejected", description: "信封、租户、类型或载荷不满足消费契约、直接拒绝的条数");
}
