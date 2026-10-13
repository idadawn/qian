using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Kun.Mediator;

namespace Qian.Plugin.Sdk;

/// <summary>代码生成的能力声明；只有元数据，不承担请求认证、授权或安装状态判定。</summary>
public sealed class CapabilityContract
{
    private readonly string _metadata;
    private readonly CapabilityDescriptor _descriptor;
    private readonly string _inputSchema;

    /// <summary>从已编译的代码声明和源生成序列化契约建立只读快照。</summary>
    public CapabilityContract(string componentId, string metadataJson, JsonTypeInfo request, JsonTypeInfo response, IReadOnlyList<string>? sensitiveProperties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(response);
        using var document = JsonDocument.Parse(metadataJson);
        var metadata = document.RootElement;
        if (metadata.ValueKind != JsonValueKind.Object || !metadata.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            throw new ArgumentException("能力声明缺少短名。", nameof(metadataJson));
        ComponentId = componentId; Name = name.GetString()!; Id = componentId + "/" + Name;
        request.MakeReadOnly(); response.MakeReadOnly();
        _metadata = metadata.GetRawText(); _descriptor = new CapabilityDescriptor(Id, request, response);
        var schema = _descriptor.RequestSchema;
        foreach (var fieldName in sensitiveProperties ?? [])
        {
            if (schema["properties"]?[fieldName] is not JsonObject property) throw new ArgumentException("敏感字段必须存在于实际输入 Schema。", nameof(sensitiveProperties));
            property["sensitive"] = true;
        }
        _inputSchema = schema.ToJsonString();
        RequestTypeInfo = request; ResponseTypeInfo = response;
    }
    /// <summary>全局能力标识，component/name。</summary>
    public string Id { get; }
    /// <summary>拥有代码的组件。</summary>
    public string ComponentId { get; }
    /// <summary>组件内短名。</summary>
    public string Name { get; }
    /// <summary>请求的源生成元数据。</summary>
    public JsonTypeInfo RequestTypeInfo { get; }
    /// <summary>响应的源生成元数据。</summary>
    public JsonTypeInfo ResponseTypeInfo { get; }
    /// <summary>不可变声明的独立 JSON 副本。</summary>
    public JsonElement Metadata { get { using var document = JsonDocument.Parse(_metadata); return document.RootElement.Clone(); } }
    /// <summary>由实际序列化器导出的输入 Schema 独立副本。</summary>
    public JsonNode InputSchema => JsonNode.Parse(_inputSchema)!;
    /// <summary>由实际序列化器导出的输出 Schema 独立副本。</summary>
    public JsonNode OutputSchema => _descriptor.ResponseSchema;
}
