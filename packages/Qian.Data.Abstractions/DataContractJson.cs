using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Qian.Data;

/// <summary>数据契约的源生成序列化入口；结构校验使用同包 Schema，字段语义使用 FilterContracts。</summary>
public static class DataContractJson
{
    private static DataContractJsonContext Context { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    });
    /// <summary>Filter AST 元数据。</summary>
    public static JsonTypeInfo<FilterExpression> FilterTypeInfo => Context.FilterExpression;
    /// <summary>记录变更完整信封元数据。</summary>
    public static JsonTypeInfo<RecordChangedEvent> RecordChangedEventTypeInfo => Context.RecordChangedEvent;
    /// <summary>记录变更载荷元数据。</summary>
    public static JsonTypeInfo<RecordChangedPayload> RecordChangedPayloadTypeInfo => Context.RecordChangedPayload;
    /// <summary>数据规则元数据。</summary>
    public static JsonTypeInfo<DataRulePolicy> RulePolicyTypeInfo => Context.DataRulePolicy;
}
/// <summary>嵌入 NuGet 的数据契约与目录，源文件位于 contracts/data/v1。</summary>
public static class DataContractSchemas
{
    /// <summary>原样随包分发的冻结 v0 信封 Schema，供离线解析引用。</summary>
    public static string FrozenEventEnvelope => Read("v0/event-envelope.schema.json");
    /// <summary>冻结信封引用的 S2S UUID 定义所在 Schema，供离线解析引用。</summary>
    public static string FrozenS2sClaims => Read("v0/s2s-claims.schema.json");
    /// <summary>Filter AST JSON Schema 2020-12。</summary>
    public static string Filter => Read("filter-ast.schema.json");
    /// <summary>字段类型 × 运算符目录。</summary>
    public static string OperatorMatrix => Read("filter-operators.json");
    /// <summary>矩阵 JSON Schema。</summary>
    public static string OperatorMatrixSchema => Read("filter-operators.schema.json");
    /// <summary>系统变量目录。</summary>
    public static string SystemVariables => Read("system-variables.json");
    /// <summary>数据规则 JSON Schema。</summary>
    public static string RulePolicy => Read("data-rule-policy.schema.json");
    /// <summary>记录变更载荷 Schema。</summary>
    public static string RecordChangedPayload => Read("qian.record.changed.v1.schema.json");
    /// <summary>引用冻结 v0 信封的完整记录事件 Schema。</summary>
    public static string RecordChangedEnvelope => Read("record-changed-envelope.schema.json");

    private static string Read(string name)
    {
        using var stream = typeof(DataContractSchemas).Assembly.GetManifestResourceStream("data/" + name)
            ?? throw new InvalidOperationException("数据契约未打包。");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FilterExpression))]
[JsonSerializable(typeof(RecordChangedEvent))]
[JsonSerializable(typeof(RecordChangedPayload))]
[JsonSerializable(typeof(DataRulePolicy))]
internal sealed partial class DataContractJsonContext : JsonSerializerContext;
