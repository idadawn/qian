using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Qian.Identity.Server;

/// <summary>
/// 会话中心的 Redis 索引（<c>qian:identity:sessions:{person}</c>，有序集合，分值为会话绝对到期的 Unix 秒）
/// 与吊销墓碑（<c>qian:identity:revoked:{sid}</c>，TTL 到会话自然到期）。
/// PostgreSQL 的 <c>sessions</c> 表是唯一事实来源，索引只是派生数据：缺失、过期或 Redis 不可用时一切行为退回 PG，
/// 绝不据索引放行。墓碑只在 PG 已提交吊销之后写入，所以「有墓碑 ⇒ 已吊销」恒成立，会话校验可以先看墓碑省一次库查询。
/// 键里是 <c>N</c> 格式的会话标识（Redis 键不进日志）；日志只记错误类型。
/// </summary>
internal interface ISessionIndex
{
    /// <summary>登记一个新建立的会话。</summary>
    ValueTask TrackAsync(Guid personId, Guid sessionId, DateTimeOffset expiresAt, CancellationToken ct);

    /// <summary>会话已吊销：移出索引并写墓碑，墓碑保留到会话本来的到期时刻。</summary>
    ValueTask ForgetAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> sessions, DateTimeOffset now, CancellationToken ct);

    /// <summary>索引里仍未到期的会话；Redis 不可用时返回 null，调用方退回数据库。</summary>
    ValueTask<IReadOnlyList<Guid>?> LiveAsync(Guid personId, DateTimeOffset now, CancellationToken ct);

    /// <summary>以数据库里的有效会话为准修复索引：补上缺的、去掉多的。</summary>
    ValueTask HealAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> live, DateTimeOffset now, CancellationToken ct);

    /// <summary>是否已有吊销墓碑；Redis 不可用或没有墓碑都返回 false（由数据库裁决）。</summary>
    ValueTask<bool> IsRevokedAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>未配置 Redis 时的实现：全部为空操作，会话中心完全由 PostgreSQL 承担。</summary>
internal sealed class NoSessionIndex : ISessionIndex
{
    internal static readonly NoSessionIndex Instance = new();
    public ValueTask TrackAsync(Guid personId, Guid sessionId, DateTimeOffset expiresAt, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask ForgetAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> sessions, DateTimeOffset now, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask<IReadOnlyList<Guid>?> LiveAsync(Guid personId, DateTimeOffset now, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<Guid>?>(null);
    public ValueTask HealAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> live, DateTimeOffset now, CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask<bool> IsRevokedAsync(Guid sessionId, CancellationToken ct) => ValueTask.FromResult(false);
}

/// <summary>
/// Redis 实现。任何 Redis 故障都吞掉并退回数据库，同时熔断一小段时间：Redis 宕机时每个请求都等一轮命令超时会拖垮登录。
/// </summary>
internal sealed partial class RedisSessionIndex(IConnectionMultiplexer redis, ILogger logger) : ISessionIndex
{
    private const long CooldownMilliseconds = 5000;
    private static readonly TimeSpan TombstoneSkew = TimeSpan.FromMinutes(5);
    private long _retryAt;

    internal static string PersonKey(Guid personId) => "qian:identity:sessions:" + personId.ToString("N");

    internal static string TombstoneKey(Guid sessionId) => "qian:identity:revoked:" + sessionId.ToString("N");

    public async ValueTask TrackAsync(Guid personId, Guid sessionId, DateTimeOffset expiresAt, CancellationToken ct) =>
        await Run(async db =>
        {
            var key = PersonKey(personId); var until = expiresAt.UtcDateTime + TombstoneSkew;
            await db.SortedSetAddAsync(key, sessionId.ToString("N"), expiresAt.ToUnixTimeSeconds()).WaitAsync(ct).ConfigureAwait(false);
            // 键的寿命取其中最晚到期的会话：新键没有 TTL 时只能用 HasNoExpiry 设，已有 TTL 时只往后延。
            await db.KeyExpireAsync(key, until, ExpireWhen.HasNoExpiry).WaitAsync(ct).ConfigureAwait(false);
            await db.KeyExpireAsync(key, until, ExpireWhen.GreaterThanCurrentExpiry).WaitAsync(ct).ConfigureAwait(false);
            return true;
        }, false).ConfigureAwait(false);

    public async ValueTask ForgetAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> sessions, DateTimeOffset now, CancellationToken ct)
    {
        if (sessions.Count == 0) return;
        await Run(async db =>
        {
            await db.SortedSetRemoveAsync(PersonKey(personId), [.. sessions.Select(s => (RedisValue)s.SessionId.ToString("N"))]).WaitAsync(ct).ConfigureAwait(false);
            foreach (var (sessionId, expiresAt) in sessions)
            {
                var ttl = expiresAt - now + TombstoneSkew;
                if (ttl > TimeSpan.Zero) await db.StringSetAsync(TombstoneKey(sessionId), "1", ttl).WaitAsync(ct).ConfigureAwait(false);
            }
            return true;
        }, false).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<Guid>?> LiveAsync(Guid personId, DateTimeOffset now, CancellationToken ct) =>
        await Run<IReadOnlyList<Guid>?>(async db =>
        {
            var members = await db.SortedSetRangeByScoreAsync(PersonKey(personId), now.ToUnixTimeSeconds(), double.PositiveInfinity).WaitAsync(ct).ConfigureAwait(false);
            return [.. members.Select(m => Guid.TryParseExact(m.ToString(), "N", out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty)];
        }, null).ConfigureAwait(false);

    public async ValueTask HealAsync(Guid personId, IReadOnlyList<(Guid SessionId, DateTimeOffset ExpiresAt)> live, DateTimeOffset now, CancellationToken ct)
    {
        var indexed = await LiveAsync(personId, now, ct).ConfigureAwait(false);
        if (indexed is null) return;
        var truth = live.Select(s => s.SessionId).ToHashSet();
        foreach (var missing in live.Where(s => !indexed.Contains(s.SessionId))) await TrackAsync(personId, missing.SessionId, missing.ExpiresAt, ct).ConfigureAwait(false);
        var stale = indexed.Where(id => !truth.Contains(id)).ToArray();
        if (stale.Length == 0) return;
        await Run(async db =>
        {
            await db.SortedSetRemoveAsync(PersonKey(personId), [.. stale.Select(id => (RedisValue)id.ToString("N"))]).WaitAsync(ct).ConfigureAwait(false);
            return true;
        }, false).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsRevokedAsync(Guid sessionId, CancellationToken ct) =>
        await Run(db => db.KeyExistsAsync(TombstoneKey(sessionId)).WaitAsync(ct), false).ConfigureAwait(false);

    private async ValueTask<T> Run<T>(Func<IDatabase, Task<T>> action, T fallback)
    {
        if (Environment.TickCount64 < Volatile.Read(ref _retryAt)) return fallback;
        try { return await action(redis.GetDatabase()).ConfigureAwait(false); }
        catch (Exception error) when (error is RedisException or TimeoutException or ObjectDisposedException or InvalidOperationException)
        {
            Volatile.Write(ref _retryAt, Environment.TickCount64 + CooldownMilliseconds);
            Unavailable(logger, error.GetType().Name);
            return fallback;
        }
    }

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "会话索引（Redis）不可用，已退回数据库并暂停使用 5 秒；ErrorType={ErrorType}")]
    private static partial void Unavailable(ILogger logger, string errorType);
}

/// <summary>进程内的「有吊销待处理」信号：写入吊销的路径提交后通知后台处理器立刻跑一轮，不必等下一次轮询。</summary>
internal sealed class SessionSignal : IDisposable
{
    private readonly SemaphoreSlim _gate = new(0, 1);

    internal void Notify()
    {
        try { _gate.Release(); }
        catch (SemaphoreFullException) { /* 已经有一个待处理的信号 */ }
    }

    internal Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _gate.WaitAsync(timeout, ct);

    public void Dispose() => _gate.Dispose();
}
