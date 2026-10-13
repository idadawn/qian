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

    /// <summary>基准类型不存在。</summary>
    public static readonly ErrorCode TypeNotFound = ErrorCode.Protocol(
        "DATA_TYPE_NOT_FOUND", 404, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "基准类型不存在" },
        new Dictionary<string, string> { ["zh-CN"] = "基准类型 {type} 不存在" });

    /// <summary>系统基准类型不可删除。</summary>
    public static readonly ErrorCode TypeProtected = ErrorCode.Business(
        "DATA_TYPE_PROTECTED", 409, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "系统基准类型不可删除" },
        new Dictionary<string, string> { ["zh-CN"] = "{type} 是系统基准类型，不可删除" });

    /// <summary>所请求的数据库方言没有注册。</summary>
    public static readonly ErrorCode DialectNotFound = ErrorCode.Protocol(
        "DATA_DIALECT_NOT_FOUND", 404, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据库方言不存在" },
        new Dictionary<string, string> { ["zh-CN"] = "数据库方言 {dialect} 没有注册" });

    /// <summary>字段定义不满足方言映射的约束；errors[] 逐项指向出错成员。</summary>
    public static readonly ErrorCode FieldDefinitionInvalid = ErrorCode.Business(
        "DATA_FIELD_DEFINITION_INVALID", 422, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "字段定义不合法" },
        new Dictionary<string, string> { ["zh-CN"] = "{type} 字段定义不满足方言约束" });

    /// <summary>字段的长度、精度、小数位或取值上下限超出方言允许的区间（只用于 errors[]）。</summary>
    public static readonly ErrorCode FieldOutOfRange = ErrorCode.Field(
        "DATA_FIELD_OUT_OF_RANGE", "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "超出方言允许的区间" },
        new Dictionary<string, string> { ["zh-CN"] = "{facet} 须在 {min} 到 {max} 之间" });

    /// <summary>基准类型不接受该约束（只用于 errors[]）。</summary>
    public static readonly ErrorCode FieldFacetUnsupported = ErrorCode.Field(
        "DATA_FIELD_FACET_UNSUPPORTED", "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "约束不适用" },
        new Dictionary<string, string> { ["zh-CN"] = "基准类型 {type} 不接受 {facet}" });

    /// <summary>最小值大于最大值（只用于 errors[]）。</summary>
    public static readonly ErrorCode FieldRangeInverted = ErrorCode.Field(
        "DATA_FIELD_RANGE_INVERTED", "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "取值区间颠倒" },
        new Dictionary<string, string> { ["zh-CN"] = "最小值不得大于最大值" });

    /// <summary>当前租户没有该数据连接。</summary>
    public static readonly ErrorCode ConnectionNotFound = ErrorCode.Protocol(
        "DATA_CONNECTION_NOT_FOUND", 404, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据连接不存在" },
        new Dictionary<string, string> { ["zh-CN"] = "当前租户没有该数据连接" });

    /// <summary>当前租户已有同名数据连接。</summary>
    public static readonly ErrorCode ConnectionNameConflict = ErrorCode.Business(
        "DATA_CONNECTION_NAME_CONFLICT", 409, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据连接重名" },
        new Dictionary<string, string> { ["zh-CN"] = "当前租户已有名为 {name} 的数据连接" });

    /// <summary>连通测试时网络不可达或超时；目标恢复后可重试。</summary>
    public static readonly ErrorCode ConnectionUnreachable = ErrorCode.Business(
        "DATA_CONNECTION_UNREACHABLE", 422, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据库不可达" },
        new Dictionary<string, string> { ["zh-CN"] = "无法连接到数据库：网络不可达或连接超时" },
        retryable: true);

    /// <summary>数据库拒绝了连接；reason 为 authentication、database、schema、permission、parameter 或 server。</summary>
    public static readonly ErrorCode ConnectionRejected = ErrorCode.Business(
        "DATA_CONNECTION_REJECTED", 422, "qian.data",
        new Dictionary<string, string> { ["zh-CN"] = "数据库拒绝连接" },
        new Dictionary<string, string> { ["zh-CN"] = "数据库拒绝了连接，原因：{reason}" });

    /// <summary>基准类型目录与数据连接档案使用的全部错误码，供宿主登记。</summary>
    public static IReadOnlyList<ErrorCode> TypesAndConnections { get; } =
    [
        TypeNotFound, TypeProtected, DialectNotFound, FieldDefinitionInvalid, FieldOutOfRange, FieldFacetUnsupported, FieldRangeInverted,
        ConnectionNotFound, ConnectionNameConflict, ConnectionUnreachable, ConnectionRejected
    ];
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
