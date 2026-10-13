namespace Qian.Events;

/// <summary>Outbox 投递的宿主级设置；组件配置 <c>outbox.lock</c> 必须与 <see cref="OutboxLock"/> 一致。</summary>
public sealed class QianEventsOptions
{
    /// <summary>每次领取的最大行数。默认 100。</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>某轮没有领取到消息时的轮询间隔。默认 1 秒。</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>领取后的租约；持有者崩溃时，其他副本在租约到期后接管。默认 30 秒，须明显长于一批的发布耗时。</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>单条消息等待 broker 确认的上限。默认 10 秒。</summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>发布失败的最大尝试次数，达到后行标为失败。默认 5。</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>失败重试的起始间隔，按 2 的幂增长，封顶一分钟。默认 1 秒。</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>领取的互斥方式。默认 PostgreSQL 行锁。</summary>
    public OutboxLockMode OutboxLock { get; set; } = OutboxLockMode.SkipLocked;

    /// <summary>本副本的标识，只用于指标标签与日志。默认机器名加进程号。</summary>
    public string RelayInstance { get; set; } = Environment.MachineName + "-" + Environment.ProcessId;

    internal void Validate()
    {
        if (BatchSize is < 1 or > 1000) throw new QianEventsException(EventsErrors.Misconfigured, "BatchSize 必须在 1 到 1000 之间。");
        if (PollInterval <= TimeSpan.Zero || PublishTimeout <= TimeSpan.Zero || RetryBaseDelay <= TimeSpan.Zero)
            throw new QianEventsException(EventsErrors.Misconfigured, "轮询间隔、发布超时与重试间隔必须为正。");
        if (LeaseDuration <= PublishTimeout) throw new QianEventsException(EventsErrors.Misconfigured, "租约必须长于单条发布超时。");
        if (MaxAttempts is < 1 or > 100) throw new QianEventsException(EventsErrors.Misconfigured, "MaxAttempts 必须在 1 到 100 之间。");
        if (string.IsNullOrWhiteSpace(RelayInstance) || RelayInstance.Length > 128) throw new QianEventsException(EventsErrors.Misconfigured, "RelayInstance 必须为 1 到 128 个字符。");
    }
}

/// <summary>Outbox 领取的互斥方式。</summary>
public enum OutboxLockMode
{
    /// <summary>各副本并行，以 <c>FOR UPDATE SKIP LOCKED</c> 加租约分摊行。</summary>
    SkipLocked,
    /// <summary>在行锁之外再按上下文 schema 取 Redis 租约，同一时刻只有一个副本投递该表；需要宿主登记 IConnectionMultiplexer。</summary>
    Redis
}
