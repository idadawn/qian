using System.Text.RegularExpressions;
using Kun.Validation;

namespace Qian.Runtime.Errors.AspNetCore;

/// <summary>Kun 字段校验异常的封闭契约桥接；不输出异常原文与自由扩展成员。</summary>
public static partial class KunValidationErrorBridge
{
    /// <summary>按输入顺序保留至多 100 项；已知字段码保留，其它码映射到 VALUE_NOT_ALLOWED。</summary>
    public static QianError ToQianError(this ValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new QianError(PlatformErrors.InputInvalid)
        {
            Errors = exception.Errors.Take(100).Select(error => new QianFieldError(FieldCode(error.Code), Pointer(error.PropertyName))).ToArray(),
        };
    }

    private static ErrorCode FieldCode(string code) => code.ToUpperInvariant() switch
    {
        "REQUIRED" => PlatformErrors.Required,
        "TYPE_MISMATCH" => PlatformErrors.TypeMismatch,
        "OUT_OF_RANGE" => PlatformErrors.OutOfRange,
        "PATTERN_MISMATCH" => PlatformErrors.PatternMismatch,
        "FORMAT_INVALID" => PlatformErrors.FormatInvalid,
        "UNKNOWN_PROPERTY" => PlatformErrors.UnknownProperty,
        _ => PlatformErrors.ValueNotAllowed,
    };

    private static string Pointer(string property)
    {
        if (property.Length == 0) return ErrorPointer.Input();
        // Kun 常见的 Property.Child[0].Name；特殊属性名保留为一个段，不丢失字符。
        var matches = PropertySegments().Matches(property);
        var complete = matches.Sum(m => m.Length) == property.Length;
        var segments = complete ? matches.Select(m => (object)(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)).ToArray() : [property];
        var pointer = ErrorPointer.Input(segments);
        return ErrorPointer.IsValid(pointer) ? pointer : ErrorPointer.Input();
    }

    [GeneratedRegex(@"\G(?:(?:^|\.)([^.\[\]]+)|\[([0-9]+)\])", RegexOptions.CultureInvariant)]
    private static partial Regex PropertySegments();
}
