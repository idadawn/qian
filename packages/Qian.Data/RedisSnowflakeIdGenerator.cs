using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors;
using StackExchange.Redis;

namespace Qian.Data;

/// <summary>雪花 ID 的宿主设置。</summary>
public sealed class QianSnowflakeOptions
{
    /// <summary>时间戳的起点；41 位毫秒约可用 69 年。部署后不能再改。默认 2026-01-01 UTC。</summary>
    public DateTimeOffset Epoch { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>WorkerId 租约时长；副本崩溃后，其 WorkerId 在租约到期后才能被别的副本取得。默认 30 秒。</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>续租间隔，须明显短于租约时长。默认 10 秒。</summary>
    public TimeSpan RenewInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>可分配的 WorkerId 个数，最多 1024（10 位）。</summary>
    public int MaxWorkers { get; set; } = 1024;

    internal void Validate()
    {
        if (MaxWorkers is < 1 or > 1024) throw new QianDataMisconfiguredException("MaxWorkers 必须在 1 到 1024 之间。");
        if (RenewInterval <= TimeSpan.Zero || LeaseDuration < RenewInterval * 2)
            throw new QianDataMisconfiguredException("雪花租约时长至少是续租间隔的两倍。");
        if (Epoch > DateTimeOffset.UtcNow) throw new QianDataMisconfiguredException("雪花时间起点不能晚于当前时间。");
    }
}

/// <summary>雪花 ID 的宿主装配。</summary>
public static class QianSnowflakeExtensions
{
    /// <summary>登记雪花 ID 生成器：启动时经 Redis 租约 <c>qian:snowflake:worker:{n}</c> 取得 WorkerId，后台续租，停止时释放。宿主须登记 IConnectionMultiplexer。</summary>
    public static IServiceCollection AddQianSnowflakeIds(this IServiceCollection services, Action<QianSnowflakeOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<QianSnowflakeOptions>();
        if (configure is not null) options.Configure(configure);
        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(ErrorCatalog.CreateDefault());
        services.TryAddSingleton(sp => new RedisSnowflakeIdGenerator(sp.GetRequiredService<IConnectionMultiplexer>(), sp.GetRequiredService<IOptions<QianSnowflakeOptions>>(),
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ErrorCatalog>(), sp.GetRequiredService<ILogger<RedisSnowflakeIdGenerator>>()));
        services.TryAddSingleton<ISnowflakeIdGenerator>(sp => sp.GetRequiredService<RedisSnowflakeIdGenerator>());
        services.AddHostedService(sp => sp.GetRequiredService<RedisSnowflakeIdGenerator>());
        return services;
    }
}

/// <summary>
/// WorkerId 经 Redis 租约分配：<c>SET qian:snowflake:worker:{n} 持有者 NX PX</c> 取最小空闲编号，后台按间隔比对持有者后续期。
/// 本地只在「最后一次成功续租的发出时刻 + 租约时长」之前发号，因此 Redis 上租约过期、编号被他人取得时，本副本已经停发。
/// 每个编号另记高水位（<c>…:hwm</c>，本编号已用或可能用到的最大毫秒），新持有者从高水位之后开始，
/// 编号在副本重启、崩溃或时钟不一致时被回收复用也不会产生重复 ID。
/// </summary>
internal sealed partial class RedisSnowflakeIdGenerator(IConnectionMultiplexer redis, IOptions<QianSnowflakeOptions> settings, TimeProvider clock,
    ErrorCatalog catalog, ILogger<RedisSnowflakeIdGenerator> logger) : BackgroundService, ISnowflakeIdGenerator
{
    internal const string KeyPrefix = "qian:snowflake:worker:";
    private const int SequenceBits = 12;
    private const int WorkerBits = 10;
    private const long MaxSequence = (1L << SequenceBits) - 1;

    // 取得：NX 占位成功后返回原高水位，并把高水位推到「现在 + 租约」（本副本最晚可能用到的毫秒）。
    private const string AcquireScript = """
        if redis.call('set', KEYS[1], ARGV[1], 'NX', 'PX', ARGV[2]) then
          local previous = tonumber(redis.call('get', KEYS[2]) or '-1')
          if tonumber(ARGV[3]) > previous then redis.call('set', KEYS[2], ARGV[3]) end
          return previous
        end
        return -2
        """;

    private const string RenewScript = """
        if redis.call('get', KEYS[1]) ~= ARGV[1] then return 0 end
        redis.call('pexpire', KEYS[1], ARGV[2])
        if tonumber(ARGV[3]) > tonumber(redis.call('get', KEYS[2]) or '-1') then redis.call('set', KEYS[2], ARGV[3]) end
        return 1
        """;

    // 释放：高水位从「可能用到」收紧为本副本实际用到的最大毫秒（取得时已不低于上一任的高水位），再删除占位。
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) ~= ARGV[1] then return 0 end
        redis.call('set', KEYS[2], ARGV[2])
        return redis.call('del', KEYS[1])
        """;

    private readonly QianSnowflakeOptions _options = settings.Value;
    private readonly string _owner = Environment.MachineName + "/" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "/" + Guid.NewGuid().ToString("N");
    private readonly Lock _gate = new();
    private IDisposable? _registration;
    private int _workerId = -1;
    private long _validUntil;
    private long _lastMs;
    private long _sequence;

    public int WorkerId
    {
        get
        {
            lock (_gate) return LeaseHeld() ? _workerId : throw Unavailable("尚未取得或已失去 WorkerId 租约。");
        }
    }

    public long NextId()
    {
        lock (_gate)
        {
            if (!LeaseHeld()) throw Unavailable("尚未取得或已失去 WorkerId 租约。");
            var now = (long)(clock.GetUtcNow() - _options.Epoch).TotalMilliseconds;
            long ms;
            if (now > _lastMs) { ms = now; _sequence = 0; }
            else
            {
                // 时钟回拨或同一毫秒：沿用上次的毫秒继续递增序列，序列用尽时逻辑上前进一毫秒，不等待墙钟。
                ms = _lastMs;
                if (++_sequence > MaxSequence) { ms++; _sequence = 0; }
            }
            if (ms >= 1L << 41) throw Unavailable("时间戳超出 41 位。");
            _lastMs = ms;
            return (ms << (WorkerBits + SequenceBits)) | ((long)_workerId << SequenceBits) | _sequence;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _options.Validate();
        _registration ??= catalog.Register(DataErrors.IdWorkerUnavailable);
        if (!await AcquireAsync().ConfigureAwait(false))
            throw new QianDataMisconfiguredException($"没有空闲的雪花 WorkerId（共 {_options.MaxWorkers} 个）。");
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.RenewInterval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    if (await RenewAsync().ConfigureAwait(false)) continue;
                    // 占位已不属于本副本（过期后被别人取得）：立即停发，改取另一个空闲编号。
                    Log.LeaseLost(logger, CurrentWorker());
                    lock (_gate) _workerId = -1;
                    if (!await AcquireAsync().ConfigureAwait(false)) Log.NoWorkerAvailable(logger, _options.MaxWorkers);
                }
                catch (Exception error) when (error is RedisException or TimeoutException)
                {
                    // Redis 不可达时保留当前编号，到本地租约截止时刻自然停发。
                    Log.RenewFailed(logger, error, CurrentWorker());
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        int worker; long last;
        lock (_gate) { worker = _workerId; last = _lastMs; _workerId = -1; }
        if (worker >= 0)
        {
            try
            {
                await redis.GetDatabase().ScriptEvaluateAsync(ReleaseScript, [LeaseKey(worker), HighWaterKey(worker)],
                    [_owner, last]).ConfigureAwait(false);
                Log.Released(logger, worker);
            }
            catch (Exception error) when (error is RedisException or TimeoutException) { Log.ReleaseFailed(logger, error, worker); }
        }
        _registration?.Dispose();
        _registration = null;
    }

    private async Task<bool> AcquireAsync()
    {
        var database = redis.GetDatabase();
        var ttl = (long)_options.LeaseDuration.TotalMilliseconds;
        for (var worker = 0; worker < _options.MaxWorkers; worker++)
        {
            var sentAt = clock.GetTimestamp();
            var bound = WallMilliseconds() + ttl;
            var previous = (long)await database.ScriptEvaluateAsync(AcquireScript, [LeaseKey(worker), HighWaterKey(worker)], [_owner, ttl, bound]).ConfigureAwait(false);
            if (previous == -2) continue;
            lock (_gate)
            {
                _workerId = worker;
                _validUntil = sentAt + (long)(_options.LeaseDuration.TotalSeconds * clock.TimestampFrequency);
                // 上一任持有者可能用到 previous 这一毫秒的任意序列号，本副本从其后的毫秒开始。
                if (previous >= _lastMs) { _lastMs = previous; _sequence = MaxSequence; }
            }
            Log.Acquired(logger, worker, previous);
            return true;
        }
        return false;
    }

    private async Task<bool> RenewAsync()
    {
        var worker = CurrentWorker();
        if (worker < 0) return false;
        var ttl = (long)_options.LeaseDuration.TotalMilliseconds;
        var sentAt = clock.GetTimestamp();
        var bound = WallMilliseconds() + ttl;
        var renewed = (long)await redis.GetDatabase().ScriptEvaluateAsync(RenewScript, [LeaseKey(worker), HighWaterKey(worker)], [_owner, ttl, bound]).ConfigureAwait(false);
        if (renewed != 1) return false;
        lock (_gate)
        {
            if (_workerId == worker) _validUntil = sentAt + (long)(_options.LeaseDuration.TotalSeconds * clock.TimestampFrequency);
        }
        return true;
    }

    private bool LeaseHeld() => _workerId >= 0 && clock.GetTimestamp() < _validUntil;

    private int CurrentWorker()
    {
        lock (_gate) return _workerId;
    }

    private long WallMilliseconds() => (long)(clock.GetUtcNow() - _options.Epoch).TotalMilliseconds;

    internal static RedisKey LeaseKey(int worker) => KeyPrefix + worker.ToString(CultureInfo.InvariantCulture);

    internal static RedisKey HighWaterKey(int worker) => KeyPrefix + worker.ToString(CultureInfo.InvariantCulture) + ":hwm";

    private static QianErrorException Unavailable(string reason) =>
        new(new QianError(DataErrors.IdWorkerUnavailable, new Dictionary<string, string> { ["reason"] = reason }));

    public override void Dispose()
    {
        _registration?.Dispose();
        _registration = null;
        base.Dispose();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "已取得雪花 WorkerId {Worker}（上一任高水位 {HighWater}）。")]
        public static partial void Acquired(ILogger logger, int worker, long highWater);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "已释放雪花 WorkerId {Worker}。")]
        public static partial void Released(ILogger logger, int worker);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "雪花 WorkerId {Worker} 的租约已被他人持有，停止发号并改取空闲编号。")]
        public static partial void LeaseLost(ILogger logger, int worker);

        [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "没有空闲的雪花 WorkerId（共 {Count} 个），发号暂停。")]
        public static partial void NoWorkerAvailable(ILogger logger, int count);

        [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "雪花 WorkerId {Worker} 续租失败，本地租约到期后停发。")]
        public static partial void RenewFailed(ILogger logger, Exception error, int worker);

        [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "释放雪花 WorkerId {Worker} 失败，等租约到期后由他人回收。")]
        public static partial void ReleaseFailed(ILogger logger, Exception error, int worker);
    }
}
