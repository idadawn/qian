using System.Buffers;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Qian.Plugin.Sdk.AspNetCore;

/// <summary>v1 <c>/api/kernel/*</c> 别名的响应头（R33，组件规格 §9.5）；S1 内保留别名，S2 移除。</summary>
public static class DeprecatedAlias
{
    /// <summary>别名的弃用时间（RFC 9745 结构化日期，2026-10-07T00:00:00Z）。</summary>
    public const string Deprecation = "@1791331200";

    /// <summary>写入 Deprecation 与指向后继路径的 Link；须在写出响应体（含错误响应）之前调用。</summary>
    public static void Apply(HttpContext http, string successor)
    {
        ArgumentNullException.ThrowIfNull(http); ArgumentException.ThrowIfNullOrWhiteSpace(successor);
        http.Response.Headers["Deprecation"] = Deprecation;
        http.Response.Headers.Link = SuccessorLink(http.Request, successor);
    }

    /// <summary>
    /// 后继带宿主的 PathBase 并保留原查询串，客户端可原样改用新路径。查询串原样来自请求行，可能含 URI 不允许的字符
    /// （如 &lt;、&gt;、引号、空格、非 ASCII）；按 RFC 3986 的 query 字符集保留合法字符与合法的百分号编码，其余按 UTF-8
    /// 百分号编码，保证 Link 的 URI-Reference 合法、不会提前闭合尖括号。
    /// </summary>
    public static string SuccessorLink(HttpRequest request, string successor)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentException.ThrowIfNullOrWhiteSpace(successor);
        var target = new StringBuilder(request.PathBase.Add(new PathString(successor)).ToUriComponent());
        ReadOnlySpan<char> query = request.QueryString.Value;
        Span<byte> bytes = stackalloc byte[4];
        while (!query.IsEmpty)
        {
            // 孤立的代理项按替换字符编码。
            if (Rune.DecodeFromUtf16(query, out var rune, out var consumed) != OperationStatus.Done) rune = Rune.ReplacementChar;
            var c = query[0];
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit(c) || "-._~!$&'()*+,;=:@/?".Contains(c) ||
                (c == '%' && query.Length > 2 && char.IsAsciiHexDigit(query[1]) && char.IsAsciiHexDigit(query[2]))))
                target.Append(c);
            else
                foreach (var b in bytes[..rune.EncodeToUtf8(bytes)]) target.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            query = query[Math.Max(consumed, 1)..];
        }
        return "<" + target + ">; rel=\"successor-version\"";
    }
}
