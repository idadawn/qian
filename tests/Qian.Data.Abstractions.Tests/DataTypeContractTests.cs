using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Data.Abstractions.Tests;

/// <summary>基准类型种子、方言数据规则与 DialectTypeMapping 的字段校验（不依赖数据库与具体方言包）。</summary>
public sealed class DataTypeContractTests
{
    [Fact]
    public void 系统基准类型种子符合Schema_与运算符矩阵的字段类型一一对应()
    {
        AssertValid(DataContractSchemas.BaseDataTypes);
        Assert.Equal(["int", "bigint", "decimal", "float", "datetime", "varchar", "text", "bool", "json", "uuid"],
            DataTypeContracts.SystemTypes.Select(type => type.Code));
        Assert.All(DataTypeContracts.SystemTypes, type => Assert.True(type.IsSystem));
        Assert.Equal(FilterContracts.Operators.Keys.Order(), DataTypeContracts.SystemTypes.Select(type => type.Code).Order());
        var decimalType = DataTypeContracts.SystemTypes.Single(type => type.Code == "decimal");
        Assert.True(decimalType.Accepts(DataTypeFacets.Precision) && decimalType.Accepts(DataTypeFacets.Scale) && decimalType.Accepts(DataTypeFacets.Range));
        Assert.False(DataTypeContracts.SystemTypes.Single(type => type.Code == "text").Accepts(DataTypeFacets.Length));
    }

    [Fact]
    public void 方言数据往返后仍符合Schema_区间以十进制字符串表示()
    {
        var dialect = DataTypeContracts.ReadDialect(Stream(Dialect()));
        var catalog = new DataTypeCatalog(DataTypeContracts.SystemTypes, [dialect]);
        var json = JsonSerializer.Serialize(catalog, DataContractJson.DataTypeCatalogTypeInfo);
        AssertValid(json);
        var bigint = JsonNode.Parse(json)!["dialects"]![0]!["mappings"]!.AsArray().Single(mapping => (string)mapping!["type"]! == "bigint")!;
        Assert.Equal("9223372036854775807", (string)bigint["range"]!["max"]!);
        Assert.Null(JsonNode.Parse(json)!["dialects"]![0]!["mappings"]![0]!["length"]);
    }

    [Theory]
    [InlineData("""{"type":"varchar","nativeType":"character varying","length":{"min":-1,"max":100,"default":10}}""", "区间无效")]
    [InlineData("""{"type":"varchar","nativeType":"character varying","length":{"min":10,"max":5,"default":10}}""", "区间无效")]
    [InlineData("""{"type":"varchar","nativeType":"character varying","length":{"min":1,"max":100,"default":200}}""", "区间无效")]
    [InlineData("""{"type":"varchar","nativeType":"character varying"}""", "约束与类型声明不一致")]
    [InlineData("""{"type":"int","nativeType":"integer","length":{"min":1,"max":10,"default":4}}""", "约束与类型声明不一致")]
    [InlineData("""{"type":"int","nativeType":"integer","range":{"min":"10","max":"1"}}""", "取值区间无效")]
    [InlineData("""{"type":"text","nativeType":"text","range":{"min":"0","max":"1"}}""", "取值区间无效")]
    [InlineData("""{"type":"int","nativeType":"integer"}""", "有多条映射")]
    [InlineData("""{"type":"money","nativeType":"money"}""", "未知基准类型")]
    public void 方言数据违反映射规则时加载即失败(string replacement, string reason)
    {
        var data = JsonNode.Parse(Dialect())!;
        var mappings = data["mappings"]!.AsArray();
        var replaced = JsonNode.Parse(replacement)!;
        var index = mappings.Select((mapping, position) => (mapping, position)).FirstOrDefault(item => (string)item.mapping!["type"]! == (string)replaced["type"]!).position;
        if (reason is "有多条映射" or "未知基准类型") mappings.Add(replaced);
        else mappings[index] = replaced;
        var error = Assert.Throws<QianDataMisconfiguredException>(() => DataTypeContracts.ReadDialect(Stream(data.ToJsonString())));
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 方言缺少系统类型映射或含未声明成员时拒绝()
    {
        var missing = JsonNode.Parse(Dialect())!;
        missing["mappings"]!.AsArray().RemoveAt(0);
        Assert.Contains("缺少系统基准类型的映射：int", Assert.Throws<QianDataMisconfiguredException>(() => DataTypeContracts.ReadDialect(Stream(missing.ToJsonString()))).Message, StringComparison.Ordinal);
        var unknown = JsonNode.Parse(Dialect())!;
        unknown["jdbcUrl"] = "jdbc:postgresql://{host}";
        Assert.Throws<QianDataMisconfiguredException>(() => DataTypeContracts.ReadDialect(Stream(unknown.ToJsonString())));
    }

    [Fact]
    public void 字段定义超出长度精度小数位或取值区间时拒绝_逐项指向出错成员()
    {
        var dialect = DataTypeContracts.ReadDialect(Stream(Dialect()));
        // 长度
        AssertRejected(dialect, new DataFieldDefinition("varchar", Length: 0), ("#/length", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("varchar", Length: 4001), ("#/length", DataErrors.FieldOutOfRange));
        // 精度与小数位：小数位不能超过精度
        AssertRejected(dialect, new DataFieldDefinition("decimal", Precision: 29), ("#/precision", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("decimal", Precision: 10, Scale: 11), ("#/scale", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("datetime", Precision: 7), ("#/precision", DataErrors.FieldOutOfRange));
        // 取值区间：整数超出 32 位、带小数；decimal(5,2) 上限 999.99 与两位小数
        AssertRejected(dialect, new DataFieldDefinition("int", Maximum: 2147483648m), ("#/maximum", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("int", Minimum: 1.5m), ("#/minimum", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("decimal", Precision: 5, Scale: 2, Maximum: 1000m), ("#/maximum", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("decimal", Precision: 5, Scale: 2, Minimum: 0.001m), ("#/minimum", DataErrors.FieldOutOfRange));
        AssertRejected(dialect, new DataFieldDefinition("int", Minimum: 10, Maximum: 1), ("#/minimum", DataErrors.FieldRangeInverted));
        // 类型不接受的约束
        AssertRejected(dialect, new DataFieldDefinition("int", Length: 10), ("#/length", DataErrors.FieldFacetUnsupported));
        AssertRejected(dialect, new DataFieldDefinition("text", Maximum: 1), ("#/maximum", DataErrors.FieldFacetUnsupported));
        // 多处同时越界时逐项报告
        AssertRejected(dialect, new DataFieldDefinition("decimal", Precision: 0, Scale: 30, Maximum: 1),
            ("#/precision", DataErrors.FieldOutOfRange), ("#/scale", DataErrors.FieldOutOfRange));
    }

    [Fact]
    public void 字段定义在区间内时补齐方言缺省值()
    {
        var dialect = DataTypeContracts.ReadDialect(Stream(Dialect()));
        Assert.Equal(new DataFieldMapping("varchar", "character varying", 255, null, null), Map(dialect, new DataFieldDefinition("varchar")));
        Assert.Equal(new DataFieldMapping("decimal", "numeric", null, 18, 2), Map(dialect, new DataFieldDefinition("decimal")));
        // 只给精度时缺省小数位随之收窄。
        Assert.Equal(new DataFieldMapping("decimal", "numeric", null, 1, 1), Map(dialect, new DataFieldDefinition("decimal", Precision: 1)));
        Assert.Equal(new DataFieldMapping("decimal", "numeric", null, 5, 2), Map(dialect,
            new DataFieldDefinition("decimal", Precision: 5, Scale: 2, Minimum: -999.99m, Maximum: 999.99m)));
        Assert.Equal(new DataFieldMapping("int", "integer", null, null, null), Map(dialect,
            new DataFieldDefinition("int", Minimum: -2147483648m, Maximum: 2147483647m)));
        Assert.Equal(new DataFieldMapping("float", "double precision", null, null, null), Map(dialect,
            new DataFieldDefinition("float", Minimum: -1e20m, Maximum: 0.000001m)));
    }

    [Fact]
    public void 字段定义经源生成元数据往返_上下限以字符串表示()
    {
        var field = new DataFieldDefinition("bigint", Minimum: -9223372036854775808m, Maximum: 9223372036854775807m);
        var json = JsonSerializer.Serialize(field, DataContractJson.FieldDefinitionTypeInfo);
        Assert.Equal("""{"type":"bigint","minimum":"-9223372036854775808","maximum":"9223372036854775807"}""", json);
        Assert.Equal(field, JsonSerializer.Deserialize(json, DataContractJson.FieldDefinitionTypeInfo));
        Assert.True(Schema(DataContractSchemas.DataTypes, "#/$defs/dataFieldDefinition").Evaluate(JsonDocument.Parse(json).RootElement).IsValid);
    }

    private static DataFieldMapping Map(DbDialect dialect, DataFieldDefinition field) =>
        dialect.Mappings.Single(mapping => mapping.Type == field.Type).Map(DataTypeContracts.SystemTypes.Single(type => type.Code == field.Type), field);

    private static void AssertRejected(DbDialect dialect, DataFieldDefinition field, params (string Pointer, ErrorCode Code)[] expected)
    {
        var error = Assert.Throws<QianErrorException>(() => Map(dialect, field)).Error;
        Assert.Equal(DataErrors.FieldDefinitionInvalid, error.Code);
        Assert.Equal(field.Type, error.Args["type"]);
        Assert.Equal(expected, error.Errors.Select(item => (item.Pointer, item.Code)).Take(expected.Length).ToArray());
    }

    // 与 PostgreSQL 方言同构的测试数据；varchar 上限收窄到 4000 以便覆盖越界。
    private static string Dialect() => """
        {
          "code": "test",
          "name": "Test",
          "driver": "Test.Driver",
          "defaultPort": 5432,
          "connectionTemplate": "Host={host};Port={port}",
          "mappings": [
            { "type": "int", "nativeType": "integer", "range": { "min": "-2147483648", "max": "2147483647", "fractionDigits": 0 } },
            { "type": "bigint", "nativeType": "bigint", "range": { "min": "-9223372036854775808", "max": "9223372036854775807", "fractionDigits": 0 } },
            { "type": "decimal", "nativeType": "numeric", "precision": { "min": 1, "max": 28, "default": 18 }, "scale": { "min": 0, "max": 28, "default": 2 } },
            { "type": "float", "nativeType": "double precision" },
            { "type": "datetime", "nativeType": "timestamp with time zone", "precision": { "min": 0, "max": 6, "default": 6 } },
            { "type": "varchar", "nativeType": "character varying", "length": { "min": 1, "max": 4000, "default": 255 } },
            { "type": "text", "nativeType": "text" },
            { "type": "bool", "nativeType": "boolean" },
            { "type": "json", "nativeType": "jsonb" },
            { "type": "uuid", "nativeType": "uuid" }
          ]
        }
        """;

    private static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));

    private static void AssertValid(string json) => Assert.True(JsonSchema.FromText(DataContractSchemas.DataTypes,
        new BuildOptions { SchemaRegistry = new SchemaRegistry() }).Evaluate(JsonDocument.Parse(json).RootElement).IsValid, json);

    private static JsonSchema Schema(string text, string pointer)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var root = JsonSchema.FromText(text, options);
        return JsonSchema.FromText("{\"$ref\":\"" + root.BaseUri + pointer + "\"}", options);
    }
}
