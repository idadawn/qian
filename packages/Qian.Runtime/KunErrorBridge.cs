using Kun.Core.Domain;
using Kun.Core.Results;
using Qian.Runtime.Errors;

namespace Qian.Runtime;

/// <summary>把 Kun 的错误分类映射到 Qian 的平台码，不传递任意 Kun 错误码或原始消息。</summary>
public static class KunErrorBridge
{
    /// <summary>按 ErrorType 映射；成功结果的 Error.None 不能作为错误转换。</summary>
    public static QianError ToQianError(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error == Error.None) throw new ArgumentException("成功结果的 Error.None 不能转换为错误。", nameof(error));
        return new QianError(error.Type switch
        {
            ErrorType.Validation => PlatformErrors.InputInvalid,
            ErrorType.NotFound => PlatformErrors.NotFound,
            ErrorType.Conflict => PlatformErrors.Conflict,
            ErrorType.Unauthorized => PlatformErrors.Unauthenticated,
            ErrorType.Forbidden => PlatformErrors.AuthzDenied,
            ErrorType.Unavailable => PlatformErrors.Unavailable,
            _ => PlatformErrors.Internal,
        });
    }

    /// <summary>未提供领域 Definition 错误码的领域异常，使用通用 422 业务码。</summary>
    public static QianError ToQianError(this DomainException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new QianError(PlatformErrors.BusinessRuleViolated);
    }
}
