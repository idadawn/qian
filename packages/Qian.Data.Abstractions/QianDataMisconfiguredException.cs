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
