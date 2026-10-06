using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Qian.Runtime.Errors;

/// <summary>
/// 构造 <c>pointer</c>：RFC 6901 JSON Pointer 的 URI 片段形式，可带文档前缀。
/// 请求输入写 <c>#/items/0/qty</c>；文件写 <c>.qian-plugin/plugin.json#/runtime/nuget/0</c>。
/// </summary>
public static partial class ErrorPointer
{
    /// <summary>指向请求输入（能力的输入文档）的指针；无参数时指向整个输入（<c>#</c>）。</summary>
    /// <param name="segments">逐级的属性名或数组下标。</param>
    public static string Input(params ReadOnlySpan<object> segments) => Build(string.Empty, segments);

    /// <summary>指向某个文件内位置的指针，如清单与表面校验错误。</summary>
    /// <param name="relativePath">以 <c>/</c> 分隔的相对路径，不含空白与 <c>#</c>。</param>
    /// <param name="segments">逐级的属性名或数组下标。</param>
    public static string InFile(string relativePath, params ReadOnlySpan<object> segments)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (relativePath.Any(c => c == '#' || char.IsWhiteSpace(c)))
            throw new ArgumentException($"文件路径 '{relativePath}' 不能含空白或 '#'。", nameof(relativePath));
        return Build(relativePath, segments);
    }

    /// <summary>是否符合 contracts/v0 的 <c>Pointer</c> 形式。</summary>
    public static bool IsValid(string? pointer) => pointer is not null && pointer.Length <= 1024 && PointerPattern().IsMatch(pointer);

    private static string Build(string prefix, ReadOnlySpan<object> segments)
    {
        var sb = new StringBuilder(prefix).Append('#');
        foreach (var segment in segments)
        {
            ArgumentNullException.ThrowIfNull(segment, nameof(segments));
            var text = segment is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : segment.ToString() ?? string.Empty;
            var escaped = text.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
            sb.Append('/').Append(Uri.EscapeDataString(escaped));
        }
        return sb.ToString();
    }

    [GeneratedRegex("^[^#\\s]*#(/([^~/]|~[01])*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex PointerPattern();
}
