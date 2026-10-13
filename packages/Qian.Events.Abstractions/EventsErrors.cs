using Qian.Runtime.Errors;

namespace Qian.Events;

/// <summary>事件组件的稳定错误码定义。</summary>
public static class EventsErrors
{
    /// <summary>宿主或组件配置不满足事件发布的装配要求。</summary>
    public static readonly ErrorCode Misconfigured = ErrorCode.Protocol(
        "EVENTS_MISCONFIGURED", 500, "qian.events",
        new Dictionary<string, string> { ["zh-CN"] = "事件组件装配错误" },
        new Dictionary<string, string> { ["zh-CN"] = "事件组件未满足装配要求：{reason}" });

    /// <summary>无法为集成事件生成合法信封，事件不会写入 Outbox。</summary>
    public static readonly ErrorCode PublishRejected = ErrorCode.Protocol(
        "EVENTS_PUBLISH_REJECTED", 500, "qian.events",
        new Dictionary<string, string> { ["zh-CN"] = "集成事件发布被拒绝" },
        new Dictionary<string, string> { ["zh-CN"] = "集成事件发布被拒绝：{reason}" });
}

/// <summary>事件装配或发布被拒绝；携带可渲染为 ErrorDetail 的错误。</summary>
public sealed class QianEventsException : InvalidOperationException
{
    /// <summary>以指定错误码和原因创建异常。</summary>
    public QianEventsException(ErrorCode code, string reason) : base(code.Code + "：" + reason)
    {
        ArgumentNullException.ThrowIfNull(code);
        Error = new QianError(code, new Dictionary<string, string> { ["reason"] = reason });
    }

    /// <summary>供宿主记录的统一错误详情。</summary>
    public QianError Error { get; }

    internal static QianEventsException Misconfigured(string reason) => new(EventsErrors.Misconfigured, reason);
    internal static QianEventsException Rejected(string reason) => new(EventsErrors.PublishRejected, reason);
}
