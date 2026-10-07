using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Qian.Runtime.Errors;

/// <summary>
/// 错误响应体（contracts/v0/error-detail.schema.json）：RFC 9457 problem+json 的 Qian 配置文件。
/// 由 <see cref="ErrorCatalog.CreateDetail"/> 生成；序列化用 <see cref="ErrorDetailJson"/>。
/// </summary>
public sealed class ErrorDetail
{
    /// <summary>问题类型 URI：<c>https://qian.dev/problems/&lt;protocol|business&gt;/&lt;code&gt;</c>。</summary>
    [JsonPropertyName("type")]
    [JsonPropertyOrder(0)]
    public required string Type { get; init; }

    /// <summary>问题类型的简短摘要（已本地化）。</summary>
    [JsonPropertyName("title")]
    [JsonPropertyOrder(1)]
    public required string Title { get; init; }

    /// <summary>HTTP 状态码。</summary>
    [JsonPropertyName("status")]
    [JsonPropertyOrder(2)]
    public required int Status { get; init; }

    /// <summary>RFC 9457 通用客户端读取的说明，与 <see cref="Message"/> 相同。</summary>
    [JsonPropertyName("detail")]
    [JsonPropertyOrder(3)]
    public string? Detail { get; init; }

    /// <summary>出错的请求路径。</summary>
    [JsonPropertyName("instance")]
    [JsonPropertyOrder(4)]
    public string? Instance { get; init; }

    /// <summary>稳定错误码。</summary>
    [JsonPropertyName("code")]
    [JsonPropertyOrder(5)]
    public required string Code { get; init; }

    /// <summary>说明（已本地化，由文案模板渲染）。</summary>
    [JsonPropertyName("message")]
    [JsonPropertyOrder(6)]
    public required string Message { get; init; }

    /// <summary>W3C trace-id（32 位小写十六进制）。</summary>
    [JsonPropertyName("trace_id")]
    [JsonPropertyOrder(7)]
    public required string TraceId { get; init; }

    /// <summary>出错的输入位置。</summary>
    [JsonPropertyName("pointer")]
    [JsonPropertyOrder(8)]
    public string? Pointer { get; init; }

    /// <summary>该位置允许的取值。</summary>
    [JsonPropertyName("allowed")]
    [JsonPropertyOrder(9)]
    public IReadOnlyList<JsonElement>? Allowed { get; init; }

    /// <summary>逐项的字段错误。</summary>
    [JsonPropertyName("errors")]
    [JsonPropertyOrder(10)]
    public IReadOnlyList<FieldError>? Errors { get; init; }
}

/// <summary><see cref="ErrorDetail.Errors"/> 中的一项。</summary>
public sealed class FieldError
{
    /// <summary>错误码。</summary>
    [JsonPropertyName("code")]
    [JsonPropertyOrder(0)]
    public required string Code { get; init; }

    /// <summary>说明（已本地化）。</summary>
    [JsonPropertyName("message")]
    [JsonPropertyOrder(1)]
    public required string Message { get; init; }

    /// <summary>出错位置。</summary>
    [JsonPropertyName("pointer")]
    [JsonPropertyOrder(2)]
    public required string Pointer { get; init; }

    /// <summary>该位置允许的取值。</summary>
    [JsonPropertyName("allowed")]
    [JsonPropertyOrder(3)]
    public IReadOnlyList<JsonElement>? Allowed { get; init; }
}

/// <summary><see cref="ErrorDetail"/> 的 JSON 序列化（源生成，AOT 可用；空成员不输出；中文不转义）。</summary>
public static class ErrorDetailJson
{
    /// <summary>错误响应的媒体类型。</summary>
    public const string MediaType = "application/problem+json";

    private static readonly ErrorDetailJsonContext s_context = new(new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    /// <summary>序列化元数据，供 <c>HttpResponse.WriteAsJsonAsync</c> 等使用。</summary>
    public static JsonTypeInfo<ErrorDetail> TypeInfo => s_context.ErrorDetail;

    /// <summary>序列化为 JSON 文本。</summary>
    public static string Serialize(ErrorDetail detail) => JsonSerializer.Serialize(detail, TypeInfo);

    /// <summary>从 JSON 文本反序列化。</summary>
    public static ErrorDetail? Deserialize(string json) => JsonSerializer.Deserialize(json, TypeInfo);
}

[JsonSerializable(typeof(ErrorDetail))]
internal sealed partial class ErrorDetailJsonContext : JsonSerializerContext;
