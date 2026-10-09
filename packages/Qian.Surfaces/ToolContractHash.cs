using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qian.Surfaces;

/// <summary>冻结契约规定的工具哈希：{description,inputSchema,name} 的 RFC 8785/JCS 与 SHA-256。</summary>
public static class ToolContractHash
{
    /// <summary>使用 ECMAScript 的 IEEE-754 数值域、UTF-16 属性顺序及字符串转义。</summary>
    public static string Compute(JsonElement tool)
    {
        var text = new StringBuilder("{\"description\":");
        Write(text, tool.GetProperty("description"));
        text.Append(",\"inputSchema\":"); Write(text, tool.GetProperty("inputSchema"));
        text.Append(",\"name\":"); Write(text, tool.GetProperty("name"));
        text.Append('}');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static void Write(StringBuilder text, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                text.Append('{'); var first = true;
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) text.Append(','); first = false;
                    String(text, property.Name); text.Append(':'); Write(text, property.Value);
                }
                text.Append('}'); break;
            case JsonValueKind.Array:
                text.Append('['); var index = 0;
                foreach (var item in value.EnumerateArray()) { if (index++ > 0) text.Append(','); Write(text, item); }
                text.Append(']'); break;
            case JsonValueKind.String: String(text, value.GetString()!); break;
            case JsonValueKind.Number: text.Append(Number(value.GetDouble())); break;
            case JsonValueKind.True: text.Append("true"); break;
            case JsonValueKind.False: text.Append("false"); break;
            default: text.Append("null"); break;
        }
    }
    private static string Number(double number)
    {
        if (!double.IsFinite(number)) throw new JsonException("JCS 数值必须有限。");
        if (number == 0) return "0";
        var sign = number < 0 ? "-" : "";
        var parts = Math.Abs(number).ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant().Split('e');
        var mantissa = parts[0];
        var point = mantissa.IndexOf('.');
        var position = (point < 0 ? mantissa.Length : point) + (parts.Length == 2 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0);
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal);
        var leading = digits.Length - digits.TrimStart('0').Length;
        position -= leading; digits = digits[leading..].TrimEnd('0');
        if (position > 0 && position <= 21)
            return sign + (digits.Length <= position ? digits + new string('0', position - digits.Length) : digits.Insert(position, "."));
        if (position <= 0 && position > -6) return sign + "0." + new string('0', -position) + digits;
        var exponent = position - 1;
        return sign + digits[0] + (digits.Length > 1 ? "." + digits[1..] : "") + "e" + (exponent >= 0 ? "+" : "") + exponent.ToString(CultureInfo.InvariantCulture);
    }
    private static void String(StringBuilder text, string value)
    {
        text.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\f': text.Append("\\f"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                default:
                    if (c < 0x20) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else if (char.IsHighSurrogate(c))
                    {
                        if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])) throw new JsonException("JCS 不接受不成对的代理码元。");
                        text.Append(c).Append(value[++i]);
                    }
                    else if (char.IsLowSurrogate(c)) throw new JsonException("JCS 不接受不成对的代理码元。");
                    else text.Append(c);
                    break;
            }
        }
        text.Append('"');
    }
}
