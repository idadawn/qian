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

    // 宿主装配错误与无权限 / 状态冲突可区分：该租户 scope 看不到审计服务时记这一条（只有操作名与租户，不含载荷），操作按 fail-closed 拒绝。
    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "审计服务不可用：{Operation}（租户 {TenantId}）无法写入租户审计链，已拒绝；请检查宿主是否装配 qian.audit 且该租户 scope 可见。")]
    internal static partial void AuditUnavailable(ILogger logger, string operation, string tenantId);

    // 平台级操作（安装、激活、回切、弃用、退役）须入平台审计链：global 上下文看不到审计服务，或审计端口拒绝本组件为平台生产方（没配
    // chain.platformRole，或审计重挂后本组件持有陈旧句柄；端口不区分原因）时记这一条，操作按 fail-closed 拒绝、状态未改。
    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "平台审计链不可用：{Operation} 无法写入平台审计链，已拒绝（{Reason}）；请检查宿主是否装配 qian.audit 并配置 chain.platformRole 与平台审计登录。")]
    internal static partial void PlatformAuditUnavailable(ILogger logger, string operation, string reason);
}
