namespace Qian.Data;

/// <summary>
/// 需要数值主键处使用的雪花 ID：41 位毫秒时间戳、10 位 WorkerId、12 位序列。WorkerId 经 Redis 租约分配，
/// 副本之间互不相同；默认主键仍是 Guid v7。
/// </summary>
public interface ISnowflakeIdGenerator
{
    /// <summary>当前租约的 WorkerId；没有有效租约时抛 DATA_ID_WORKER_UNAVAILABLE。</summary>
    int WorkerId { get; }

    /// <summary>生成下一个 ID；同一 WorkerId 下严格递增，没有有效租约时抛 DATA_ID_WORKER_UNAVAILABLE。</summary>
    long NextId();
}
