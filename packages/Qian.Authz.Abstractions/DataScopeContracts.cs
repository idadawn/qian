using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Qian.Authz.Abstractions;

/// <summary>指定资源的数据范围；AllData=true 时 Filter 必须缺席，false 时必须存在。该 DTO 不执行鉴权。</summary>
public sealed record DataScopeRule(string Resource, bool AllData, JsonElement? Filter = null);
/// <summary>某角色的数据规则集合；同一角色的多份集合在合并时仍属于一个 AND 分支。</summary>
public sealed record RoleDataScope(string Role, IReadOnlyList<DataScopeRule> Rules);
/// <summary>命名资源的逻辑计划；Roles 之间 OR，每个分支内 Filters 之间 AND，空 Roles 表示拒绝；AllData 不跳过租户隔离。</summary>
public sealed record DataScopePlan(string Resource, bool AllData, IReadOnlyList<DataScopeRolePlan> Roles);
/// <summary>单个角色的 AND 分支，保留原 Filter AST，避免展开成指数大小的表达式。</summary>
public sealed record DataScopeRolePlan(string Role, IReadOnlyList<JsonElement> Filters);

/// <summary>随 Definition 分发的数据权限结构；Filter AST、系统变量与矩阵复用 Data 的唯一目录。</summary>
public static class DataScopeContracts
{
    private static DataScopeJson Json { get; } = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true
    });
    /// <summary>DataScopeRule 的 camelCase 源生成元数据；还须按 Schema 和可信字段目录校验。</summary>
    public static JsonTypeInfo<DataScopeRule> RuleTypeInfo => Json.DataScopeRule;
    /// <summary>只读合并计划的源生成元数据；不是 SQL 或权限判定结果。</summary>
    public static JsonTypeInfo<DataScopePlan> PlanTypeInfo => Json.DataScopePlan;
    /// <summary>原样嵌入的 JSON Schema 2020-12，引用 Data 的 Filter AST。</summary>
    public static string RuleSchema => Read("data-scope-rule.schema.json");
    /// <summary>authz/meta 的静态、可离线解析的元数据；不读取角色、主体或授予，不引入数据库依赖。</summary>
    public static JsonElement Metadata { get; } = CreateMetadata();
    private static JsonElement CreateMetadata()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            Write("dataScopeRule", "data-scope-rule.schema.json"); Write("filterAst", "filter-ast.schema.json");
            Write("operators", "filter-operators.json"); Write("systemVariables", "system-variables.json");
            writer.WriteStartObject("merge"); writer.WriteString("withinRole", "and"); writer.WriteString("acrossRoles", "or");
            writer.WriteString("anyAllData", "allow-resource"); writer.WriteString("noRules", "deny"); writer.WriteString("tenantIsolation", "always");
            writer.WriteEndObject(); writer.WriteEndObject();
            void Write(string property, string resource)
            {
                writer.WritePropertyName(property); using var document = JsonDocument.Parse(Read(resource)); document.RootElement.WriteTo(writer);
            }
        }
        using var result = JsonDocument.Parse(buffer.ToArray()); return result.RootElement.Clone();
    }
    private static string Read(string name)
    {
        using var stream = typeof(DataScopeContracts).Assembly.GetManifestResourceStream("authz/" + name)
            ?? throw new InvalidOperationException("数据权限契约未打包。");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DataScopeRule))]
[JsonSerializable(typeof(DataScopePlan))]
internal sealed partial class DataScopeJson : JsonSerializerContext;
