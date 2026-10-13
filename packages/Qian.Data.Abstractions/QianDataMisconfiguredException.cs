using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>数据组件的稳定错误码定义。</summary>
public static class DataErrors
{
    /// <summary>宿主未满足数据隔离的必要装配条件。</summary>
    public static readonly ErrorCode Misconfigured = ErrorCode.Protocol(
        "DATA_MISCONFIGURED", 500, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据组件装配错误" },
        new Dictionary<string, string> { ["zh-CN"] = "数据组件未满足安全装配要求：{reason}" });

    /// <summary>序号规则的模式不合法。</summary>
    public static readonly ErrorCode SequenceRuleInvalid = ErrorCode.Business(
        "DATA_SEQUENCE_RULE_INVALID", 422, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "序号规则不合法" },
        new Dictionary<string, string> { ["zh-CN"] = "序号规则不合法：{reason}" });

    /// <summary>当前租户没有定义所请求的序号规则。</summary>
    public static readonly ErrorCode SequenceRuleNotFound = ErrorCode.Protocol(
        "DATA_SEQUENCE_RULE_NOT_FOUND", 404, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "序号规则不存在" },
        new Dictionary<string, string> { ["zh-CN"] = "当前租户没有定义序号规则 {rule}" });

    /// <summary>序号规则在当前周期的流水号已经用尽。</summary>
    public static readonly ErrorCode SequenceExhausted = ErrorCode.Business(
        "DATA_SEQUENCE_EXHAUSTED", 409, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "序号已用尽" },
        new Dictionary<string, string> { ["zh-CN"] = "序号规则 {rule} 在当前周期的流水号已用尽" });

    /// <summary>本副本暂时没有有效的雪花 WorkerId 租约，稍后重试。</summary>
    public static readonly ErrorCode IdWorkerUnavailable = ErrorCode.Protocol(
        "DATA_ID_WORKER_UNAVAILABLE", 503, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "雪花 ID 暂不可用" },
        new Dictionary<string, string> { ["zh-CN"] = "本副本没有有效的雪花 WorkerId 租约：{reason}" },
        retryable: true);
}

/// <summary>在构造上下文或启动宿主时拒绝不安全的数据层配置。</summary>
public sealed class QianDataMisconfiguredException : InvalidOperationException
{
    /// <summary>指定缺失或被覆盖的安全配置。</summary>
    public QianDataMisconfiguredException(string reason) : base($"数据组件未满足安全装配要求：{reason}")
    {
        Error = new QianError(DataErrors.Misconfigured, new Dictionary<string, string> { ["reason"] = reason });
    }

    /// <summary>供宿主记录的统一错误详情。</summary>
    public QianError Error { get; }
}
