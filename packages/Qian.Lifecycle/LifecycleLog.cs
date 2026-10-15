using Microsoft.Extensions.Logging;

namespace Qian.Lifecycle;

// 生命周期日志：只记引用、工作负载名、钩子与原因摘要，不记秘密、令牌或响应正文。
internal static partial class LifecycleLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "信任根 {Reference} 无法读取，已跳过。")]
    internal static partial void TrustRootUnreadable(ILogger logger, string reference);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "部署仓库推送被拒（第 {Attempt} 次）：{Detail}")]
    internal static partial void PushRejected(ILogger logger, int attempt, string detail);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "部署仓库克隆失败：{Detail}")]
    internal static partial void CloneFailed(ILogger logger, string detail);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "git {Command} 失败：{Detail}")]
    internal static partial void GitFailed(ILogger logger, string? command, string detail);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "部署健康观测失败：{Workload}")]
    internal static partial void ObserveFailed(ILogger logger, Exception error, string workload);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "生命周期钩子 {Hook} 执行失败：{PluginId}@{Version}")]
    internal static partial void HookFailed(ILogger logger, Exception error, string hook, string pluginId, string version);
}
