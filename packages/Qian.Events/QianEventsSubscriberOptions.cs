using System.Text;

namespace Qian.Events;

/// <summary>
/// 消费端的宿主级设置。重试语义与 Outbox 投递侧不同：这里的 <see cref="MaxRetries"/> 是首投之外的重试次数，
/// 默认首投 1 次 + 重试 5 次，第 6 次仍失败即进死信队列。
/// </summary>
public sealed class QianEventsSubscriberOptions
{
    /// <summary>本实例的名字，广播队列 <c>q.&lt;service&gt;.&lt;instance&gt;</c> 与广播幂等记录用它区分实例。默认取规范化后的机器名加进程号。</summary>
    public string Instance { get; set; } = DefaultInstance();

    /// <summary>每个消费通道未确认消息的上限。默认 16。</summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>首投之外的最大重试次数；用尽后再失败即进死信队列。默认 5，即最多投递 6 次。</summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>重试退避起点：第 n 次重试前等待 <c>RetryBaseDelay × 2^(n-1)</c>，封顶一分钟，与 Outbox 投递侧对称。默认 1 秒。</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>幂等记录的保留时长（PRD 7.10、R63）。默认 7 天。</summary>
    public TimeSpan IdempotencyTtl { get; set; } = TimeSpan.FromDays(7);

    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

    internal void Validate(string service)
    {
        if (PrefetchCount == 0) throw Invalid("PrefetchCount 必须大于 0。");
        if (MaxRetries is < 0 or > 20) throw Invalid("MaxRetries 必须在 0 到 20 之间。");
        if (RetryBaseDelay < TimeSpan.FromMilliseconds(1) || RetryBaseDelay > MaxRetryDelay) throw Invalid("RetryBaseDelay 必须在 1 毫秒到 1 分钟之间。");
        if (IdempotencyTtl < TimeSpan.FromSeconds(1)) throw Invalid("IdempotencyTtl 必须至少 1 秒。");
        try { _ = QianEventTopology.BroadcastQueue(service, Instance); }
        catch (ArgumentException error) { throw Invalid("Instance 不能用作广播队列名：" + error.Message); }
    }

    /// <summary>第 <paramref name="attempt"/> 次失败后的退避间隔。</summary>
    internal TimeSpan RetryDelay(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(RetryBaseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt - 1, 16)), MaxRetryDelay.TotalMilliseconds)));

    // macOS / Windows 的机器名常含大写与点，直接拼进队列名会被拒绝：转小写，非 [a-z0-9-] 一律换成短横线，首字符不是字母时补前缀。
    internal static string DefaultInstance() => Normalize(Environment.MachineName + "-" + Environment.ProcessId);

    internal static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        foreach (var c in value.ToLowerInvariant()) builder.Append(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) ? c : '-');
        var name = builder.ToString().Trim('-');
        if (name.Length == 0 || !char.IsAsciiLetterLower(name[0])) name = "i-" + name;
        name = name.Length <= 100 ? name : name[..100].TrimEnd('-');
        // 机器名恰好以保留后缀结尾时补一段，避免与某个订阅的死信或重试队列同名。
        return name.EndsWith("-dlq", StringComparison.Ordinal) || name.Contains("-retry-", StringComparison.Ordinal) ? name + "-i" : name;
    }

    private static QianEventsException Invalid(string reason) => new(EventsErrors.Misconfigured, reason);
}
