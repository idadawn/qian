using Qian.Runtime;

namespace Qian.Config;

/// <summary>配置组件的事件与审计词汇；只含稳定标识，不含任何配置值。</summary>
public static class ConfigEvents
{
    /// <summary>
    /// 重载失败告警的跨进程事件 ID（PRD 7.8）。配置重载不在数据事务里，Outbox 写入端契约发布（S1-EVT-003）前只以
    /// <see cref="ConfigRuntimeEvents.ReloadFailed"/> 在进程内发出，载荷形状与此 ID 一致。
    /// </summary>
    public const string ReloadFailedType = "config.reload.failed.v1";
    /// <summary>Apollo 快照被实例接受并生效时写入平台审计链的能力 ID（R23 component/name 写法）。</summary>
    public const string ReleaseAppliedCapabilityId = "qian.config/release.applied";
    /// <summary>Apollo 快照被实例校验拒绝、保留上一份时写入平台审计链的能力 ID。</summary>
    public const string ReleaseRejectedCapabilityId = "qian.config/release.rejected";
}

/// <summary>Config seam 的进程内扩展事件（组件模型规格 §6.4）。</summary>
public static class ConfigRuntimeEvents
{
    /// <summary>强类型配置重载被校验拒绝、已保留上一份有效值；由挂载中的配置组件发出，监听器异常被隔离。</summary>
    [QianEvent(Mode = DispatchMode.Emit)]
    public static readonly QianEvent<ConfigReloadFailed, object?> ReloadFailed =
        QianEvent.Define<ConfigReloadFailed, object?>("config/reload-failed", DispatchMode.Emit);
}

/// <summary>
/// 重载失败告警（<see cref="ConfigEvents.ReloadFailedType"/>）：options 类型与名称、配置节、失败条数与错误码；
/// 不含任何配置值，也不含校验消息原文（自定义校验器的消息可能带值）。
/// </summary>
public sealed record ConfigReloadFailed(string OptionsType, string OptionsName, string Section, int Failures, string Code)
{
    /// <summary>登记该配置的组件 ID；宿主未指明时为 null。</summary>
    public string? Plugin { get; init; }
}
