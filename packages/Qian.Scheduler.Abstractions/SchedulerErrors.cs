using Qian.Runtime.Errors;

namespace Qian.Scheduler;

/// <summary>调度组件的错误词汇。</summary>
public static class SchedulerErrors
{
    /// <summary>缺少安全数据库装配或专用 worker 登录。</summary>
    public static readonly ErrorCode Misconfigured = ErrorCode.Protocol("SCHEDULER_MISCONFIGURED", 500, "qian.scheduler",
        new Dictionary<string, string> { ["zh-CN"] = "调度器装配错误" },
        new Dictionary<string, string> { ["zh-CN"] = "调度器需要 Data 安全装配与专用受限 worker 登录。" });
}
