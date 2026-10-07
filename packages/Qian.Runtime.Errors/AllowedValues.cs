using System.Buffers;
using System.Text.Json;

namespace Qian.Runtime.Errors;

/// <summary>构造 <c>allowed</c>（允许的取值，1–100 个 JSON 值）。</summary>
public static class AllowedValues
{
    /// <summary>上限，与 contracts/v0 的 <c>Allowed.maxItems</c> 一致。</summary>
    public const int MaxCount = 100;

    /// <summary>字符串取值。</summary>
    public static IReadOnlyList<JsonElement> Of(params ReadOnlySpan<string> values)
    {
        EnsureCount(values.Length, nameof(values));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                ArgumentNullException.ThrowIfNull(value, nameof(values));
                writer.WriteStringValue(value);
            }
            writer.WriteEndArray();
        }
        return Read(buffer);
    }

    /// <summary>整数取值。</summary>
    public static IReadOnlyList<JsonElement> Of(params ReadOnlySpan<long> values)
    {
        EnsureCount(values.Length, nameof(values));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
                writer.WriteNumberValue(value);
            writer.WriteEndArray();
        }
        return Read(buffer);
    }

    internal static void EnsureValid(IReadOnlyList<JsonElement>? values, string paramName)
    {
        if (values is not null && (values.Count is 0 or > MaxCount))
            throw new ArgumentException($"allowed 须有 1–{MaxCount} 个取值。", paramName);
    }

    private static void EnsureCount(int count, string paramName)
    {
        if (count is 0 or > MaxCount)
            throw new ArgumentException($"allowed 须有 1–{MaxCount} 个取值。", paramName);
    }

    private static JsonElement[] Read(ArrayBufferWriter<byte> buffer)
    {
        using var doc = JsonDocument.Parse(buffer.WrittenMemory);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
    }
}
