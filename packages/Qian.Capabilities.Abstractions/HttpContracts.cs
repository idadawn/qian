using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>实际 HTTP 宿主的契约来源；必须按当前身份、端点策略和真实挂载视图裁剪。</summary>
public interface IHttpContractSource
{
    /// <summary>从实际宿主端点导出一个组件的完整契约，供受信任制品构建使用，不经过用户权限裁剪。</summary>
    ValueTask<JsonElement> CompileDocumentAsync(string pluginId, CancellationToken cancellationToken = default);
    /// <summary>读取当前请求可访问的组件 HTTP 文档；没有可信 HTTP 请求时不得推测端点权限。</summary>
    ValueTask<JsonElement> GetDocumentAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
}

/// <summary>平台运营端点的契约归属；只用于构建完整 OpenAPI，不开放租户业务挂载或授权旁路。</summary>
public sealed record PlatformHttpContract(string PluginId);

/// <summary>动态 JSON 响应的声明式 Schema；配合实际 Produces 元数据导出，不改变授权或运行时行为。</summary>
public sealed class HttpResponseSchema
{
    /// <summary>声明指定状态码与媒体类型的响应 Schema，保留独立文档副本。</summary>
    public HttpResponseSchema(int statusCode, JsonElement schema, string mediaType = "application/json")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, 100);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, 599);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        if (schema.ValueKind != JsonValueKind.Object) throw new ArgumentException("响应 Schema 必须是 JSON 对象。", nameof(schema));
        StatusCode = statusCode; Schema = schema.Clone(); MediaType = mediaType;
    }
    /// <summary>响应状态码。</summary>
    public int StatusCode { get; }
    /// <summary>声明式 JSON Schema。</summary>
    public JsonElement Schema { get; }
    /// <summary>响应媒体类型。</summary>
    public string MediaType { get; }
}
