using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>审计 v1 的确定性 JSON：键按 ordinal 排序，精确十进制数规整，数组顺序保留，重复键拒绝。</summary>
public static class AuditCanonicalizer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    /// <summary>计算 SHA256(prev_hash 的 UTF-8 文本 || canonical_json(entry))，包含全部 Qian 审计字段和序号。</summary>
    public static string Hash(AuditRecord record, long sequence, string? previousHash)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (sequence <= 0 || (previousHash is not null && (previousHash.Length != 64 || previousHash.Any(c => !(c is >= 'a' and <= 'f' or >= '0' and <= '9'))))) throw Invalid();
        var canonical = Content(record, sequence);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(previousHash ?? "")); hash.AppendData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static string Content(AuditRecord record, long sequence)
    {
        var node = JsonSerializer.SerializeToNode(record, Options)!.AsObject(); node["seq"] = sequence; node["format_version"] = "v1";
        return Canonicalize(JsonSerializer.SerializeToElement(node));
    }
    /// <summary>确定性 JSON 编码；不是将有损浮点数转换作为规范化手段。</summary>
    public static string Canonicalize(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, value, 0);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value, int depth)
    {
        if (depth > 32) throw Invalid();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var fields = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                if (fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw Invalid();
                writer.WriteStartObject(); foreach (var field in fields) { writer.WritePropertyName(field.Name); Write(writer, field.Value, depth + 1); } writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(writer, item, depth + 1); writer.WriteEndArray(); break;
            case JsonValueKind.Number: writer.WriteRawValue(Number(value.GetRawText())); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw Invalid();
        }
    }
    private static string Number(string raw)
    {
        var negative = raw[0] == '-'; var text = negative ? raw[1..] : raw;
        var parts = text.Split(['e', 'E']);
        var exponent = 0;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || exponent is < -1000 or > 1000)) throw Invalid();
        var dot = parts[0].IndexOf('.'); var scale = (dot < 0 ? 0 : parts[0].Length - dot - 1) - exponent;
        if (scale > 16383) throw Invalid();
        var digits = parts[0].Replace(".", "", StringComparison.Ordinal).TrimStart('0'); if (digits.Length == 0) return "0";
        if (digits.Length > 1024) throw Invalid();
        while (digits.EndsWith('0')) { digits = digits[..^1]; scale--; }
        string result;
        if (scale <= 0) result = digits + new string('0', -scale);
        else if (scale >= digits.Length) result = "0." + new string('0', scale - digits.Length) + digits;
        else result = digits.Insert(digits.Length - scale, ".");
        return negative ? "-" + result : result;
    }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}
