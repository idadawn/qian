using System.Text.Json;
using Qian.Runtime;

namespace Qian.Scheduler;

/// <summary>持久调度 Definition，Provider 可由 profile 替换。</summary>
public static class SchedulerDefinition
{
    /// <summary>调度服务键。</summary>
    public static readonly ServiceKey Key = new("Scheduler");
    /// <summary>读取当前 scope 可见的调度器。</summary>
    public static IScheduler Scheduler(this IPluginContext context) => context.Inject<IScheduler>(Key);
}

/// <summary>超过允许延迟的触发如何处理。</summary>
public enum MisfirePolicy
{
    /// <summary>跳过过期发生点。</summary>
    Skip,
    /// <summary>合并过期发生点为本次一次执行。</summary>
    FireOnce,
    /// <summary>按原计划时间串行补齐。</summary>
    CatchUp
}
/// <summary>同一定义的串行执行策略；不并行运行同一定义。</summary>
public enum JobConcurrency
{
    /// <summary>完成后继续处理排队发生点，由 misfire 决定补齐或合并。</summary>
    Queue,
    /// <summary>跳过上次执行期间的重叠发生点。</summary>
    Skip
}
/// <summary>持久化的执行策略；所有时长均为真实经过的时间。</summary>
public sealed record JobOptions
{
    /// <summary>超过此时间向处理器发送取消；处理器须合作式停止。</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>迟到策略，默认补一次。</summary>
    public MisfirePolicy Misfire { get; init; } = MisfirePolicy.FireOnce;
    /// <summary>允许的正常调度延迟。</summary>
    public TimeSpan MisfireGrace { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>同一作业的并发策略。</summary>
    public JobConcurrency Concurrency { get; init; } = JobConcurrency.Queue;
    /// <summary>同一 run 的最大尝试次数，含初次；重试保留 run id。</summary>
    public int MaxAttempts { get; init; } = 3;
    /// <summary>失败后的重试间隔。</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}
/// <summary>租户业务键对应的持久定义标识。</summary>
public sealed record ScheduledJob(Guid Id, string BusinessKey);

/// <summary>可信组件的租户调度端口；caller scope 必须与当前租户一致，不接受请求体租户。</summary>
public interface IScheduler
{
    /// <summary>业务键幂等的一次性任务；相同键不同内容返回 CONFLICT。</summary>
    ValueTask<ScheduledJob> ScheduleOnce(IPluginContext caller, string businessKey, DateTimeOffset runAt, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>UTC 的 6/7 段 cron（秒至星期，可选年份）。</summary>
    ValueTask<ScheduledJob> ScheduleCron(IPluginContext caller, string businessKey, string cron, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>固定间隔；首次时间明确给出。</summary>
    ValueTask<ScheduledJob> ScheduleInterval(IPluginContext caller, string businessKey, DateTimeOffset firstRunAt, TimeSpan interval, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default);
    /// <summary>持久取消待运行及后续发生点，并向正在执行的本地处理器传播取消；返回是否找到业务键。</summary>
    ValueTask<bool> Cancel(IPluginContext caller, string businessKey, CancellationToken cancellationToken = default);
}
