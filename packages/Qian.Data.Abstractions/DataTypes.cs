using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>基准类型可携带的字段约束名，与 data-types.schema.json 的 facet 枚举一致。</summary>
public static class DataTypeFacets
{
    /// <summary>最大长度（varchar）。</summary>
    public const string Length = "length";
    /// <summary>精度：decimal 为总位数，datetime 为秒的小数位数。</summary>
    public const string Precision = "precision";
    /// <summary>小数位（decimal），不超过精度。</summary>
    public const string Scale = "scale";
    /// <summary>取值上下限（minimum / maximum）。</summary>
    public const string Range = "range";
}

/// <summary>与具体数据库无关的基准数据类型，是库类型、控件与标准字段之间的中间语言；系统类型由 qian.data 种子提供，不可删除、不可改名。</summary>
public sealed record BaseDataType(string Code, string Name, string Description, bool IsSystem, IReadOnlyList<string> Facets)
{
    /// <summary>该类型的字段定义是否接受指定约束。</summary>
    public bool Accepts(string facet) => Facets.Contains(facet, StringComparer.Ordinal);
}

/// <summary>长度、精度或小数位的允许区间与缺省值，三者都不为负且缺省值落在区间内。</summary>
public sealed record DataTypeLimit(int Min, int Max, int Default);

/// <summary>字段取值上下限可落的区间；JSON 中以十进制字符串表示。FractionDigits 为上下限允许的小数位数，null 不限。</summary>
public sealed record DataValueRange(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Min,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Max,
    int? FractionDigits = null);

/// <summary>建模时的一列定义：基准类型与可选的长度、精度、小数位和取值上下限；省略的长度、精度、小数位取方言缺省值。</summary>
public sealed record DataFieldDefinition(string Type, int? Length = null, int? Precision = null, int? Scale = null,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Minimum = null,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? Maximum = null);

/// <summary>字段定义在某方言下补齐缺省值后的原生类型与参数。</summary>
public sealed record DataFieldMapping(string Type, string NativeType, int? Length, int? Precision, int? Scale);

/// <summary>方言内一个基准类型对应的原生类型，以及长度、精度、小数位与取值的约束（qian.data.dialect/v1 数据）。</summary>
public sealed record DialectTypeMapping(string Type, string NativeType, DataTypeLimit? Length = null, DataTypeLimit? Precision = null,
    DataTypeLimit? Scale = null, DataValueRange? Range = null)
{
    /// <summary>
    /// 按本映射校验字段定义并补齐缺省值。长度、精度、小数位超出区间，上下限超出原生类型可表示的范围（decimal 由精度与小数位推出），
    /// 最小值大于最大值，或给出该类型不接受的约束时，抛出 DATA_FIELD_DEFINITION_INVALID，errors[] 逐项指向出错成员。
    /// </summary>
    public DataFieldMapping Map(BaseDataType type, DataFieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(field);
        if (type.Code != Type || field.Type != Type)
            throw new ArgumentException("字段定义、基准类型与映射必须是同一个基准类型。", nameof(field));
        var errors = new List<QianFieldError>();
        var length = Limited(type, DataTypeFacets.Length, field.Length, Length, "#/length", errors);
        var precision = Limited(type, DataTypeFacets.Precision, field.Precision, Precision, "#/precision", errors);
        int? scale = null;
        if (!type.Accepts(DataTypeFacets.Scale))
        {
            if (field.Scale is not null) errors.Add(Unsupported(type, DataTypeFacets.Scale, "#/scale"));
        }
        else if (Scale is { } limit)
        {
            // 小数位不能超过精度；只给精度时缺省小数位随之收窄，不因缺省值越界而拒绝。
            var max = precision is { } digits ? Math.Min(limit.Max, digits) : limit.Max;
            scale = field.Scale ?? Math.Min(limit.Default, max);
            if (scale < limit.Min || scale > max) errors.Add(OutOfRange(DataTypeFacets.Scale, "#/scale", limit.Min, max));
        }
        CheckRange(type, field, precision, scale, errors);
        if (errors.Count > 0)
            throw new QianErrorException(new QianError(DataErrors.FieldDefinitionInvalid, new Dictionary<string, string> { ["type"] = Type }) { Errors = errors });
        return new DataFieldMapping(Type, NativeType, length, precision, scale);
    }

    private static int? Limited(BaseDataType type, string facet, int? value, DataTypeLimit? limit, string pointer, List<QianFieldError> errors)
    {
        if (!type.Accepts(facet) || limit is null)
        {
            if (value is not null) errors.Add(Unsupported(type, facet, pointer));
            return null;
        }
        var actual = value ?? limit.Default;
        if (actual < limit.Min || actual > limit.Max) errors.Add(OutOfRange(facet, pointer, limit.Min, limit.Max));
        return actual;
    }

    private void CheckRange(BaseDataType type, DataFieldDefinition field, int? precision, int? scale, List<QianFieldError> errors)
    {
        if (!type.Accepts(DataTypeFacets.Range))
        {
            if (field.Minimum is not null) errors.Add(Unsupported(type, DataTypeFacets.Range, "#/minimum"));
            if (field.Maximum is not null) errors.Add(Unsupported(type, DataTypeFacets.Range, "#/maximum"));
            return;
        }
        var bounds = Range ?? Derived(precision, scale);
        foreach (var (value, pointer) in new[] { (field.Minimum, "#/minimum"), (field.Maximum, "#/maximum") })
        {
            if (value is not { } bound || bounds is null) continue;
            if (bound < bounds.Min || bound > bounds.Max || bounds.FractionDigits is { } digits && decimal.Round(bound, digits) != bound)
                errors.Add(OutOfRange(DataTypeFacets.Range, pointer, bounds.Min, bounds.Max));
        }
        if (field.Minimum > field.Maximum) errors.Add(new QianFieldError(DataErrors.FieldRangeInverted, "#/minimum"));
    }

    // decimal(p, s) 可表示 ±(10^(p-s) − 10^-s)，上下限的小数位不超过 s。
    private static DataValueRange? Derived(int? precision, int? scale)
    {
        if (precision is not { } p || scale is not { } s || s > p || p > 28) return null;
        var max = Pow10(p - s) - 1m / Pow10(s);
        return new DataValueRange(-max, max, s);
    }

    private static decimal Pow10(int exponent)
    {
        var value = 1m;
        for (var i = 0; i < exponent; i++) value *= 10;
        return value;
    }

    private static QianFieldError OutOfRange(string facet, string pointer, decimal min, decimal max) =>
        new(DataErrors.FieldOutOfRange, pointer, new Dictionary<string, string>
        {
            ["facet"] = facet,
            ["min"] = min.ToString(CultureInfo.InvariantCulture),
            ["max"] = max.ToString(CultureInfo.InvariantCulture)
        });

    private static QianFieldError Unsupported(BaseDataType type, string facet, string pointer) =>
        new(DataErrors.FieldFacetUnsupported, pointer, new Dictionary<string, string> { ["facet"] = facet, ["type"] = type.Code });
}

/// <summary>数据库方言的描述数据：驱动、连接串模板（只作展示，连接由方言用驱动的构建器生成）与覆盖全部系统基准类型的映射。</summary>
public sealed partial record DbDialect(string Code, string Name, string Driver, int DefaultPort, string ConnectionTemplate,
    IReadOnlyList<DialectTypeMapping> Mappings)
{
    /// <summary>按 qian.data.dialect/v1 规则核对映射：每个基准类型恰有一条，约束与类型声明的 facet 一致，区间不为负、下限不大于上限、缺省值落在区间内。</summary>
    public void Validate(IEnumerable<BaseDataType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var known = types.ToDictionary(type => type.Code, StringComparer.Ordinal);
        if (Code is null || !CodePattern().IsMatch(Code)) throw Invalid("方言代码须为 1 到 32 位小写字母或数字，以字母开头");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64 || string.IsNullOrWhiteSpace(Driver) || Driver.Length > 128 ||
            string.IsNullOrWhiteSpace(ConnectionTemplate) || ConnectionTemplate.Length > 512 || DefaultPort is < 1 or > 65535)
            throw Invalid("名称、驱动、连接串模板或缺省端口无效");
        if (Mappings is null || Mappings.Count == 0) throw Invalid("没有类型映射");
        var mapped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapping in Mappings)
        {
            if (mapping is null || !known.TryGetValue(mapping.Type ?? "", out var type)) throw Invalid("映射引用了未知基准类型");
            if (!mapped.Add(type.Code)) throw Invalid("基准类型 " + type.Code + " 有多条映射");
            if (string.IsNullOrWhiteSpace(mapping.NativeType) || mapping.NativeType.Length > 64) throw Invalid("基准类型 " + type.Code + " 的原生类型无效");
            Facet(type, DataTypeFacets.Length, mapping.Length);
            Facet(type, DataTypeFacets.Precision, mapping.Precision);
            Facet(type, DataTypeFacets.Scale, mapping.Scale);
            if (mapping.Range is { } range && (!type.Accepts(DataTypeFacets.Range) || range.Min > range.Max || range.FractionDigits is < 0 or > 28))
                throw Invalid("基准类型 " + type.Code + " 的取值区间无效");
        }
        var missing = known.Values.Where(type => type.IsSystem && !mapped.Contains(type.Code)).Select(type => type.Code).ToArray();
        if (missing.Length > 0) throw Invalid("缺少系统基准类型的映射：" + string.Join(", ", missing));
    }

    private void Facet(BaseDataType type, string facet, DataTypeLimit? limit)
    {
        if (type.Accepts(facet) != limit is not null) throw Invalid("基准类型 " + type.Code + " 的 " + facet + " 约束与类型声明不一致");
        if (limit is not null && (limit.Min < 0 || limit.Min > limit.Max || limit.Default < limit.Min || limit.Default > limit.Max))
            throw Invalid("基准类型 " + type.Code + " 的 " + facet + " 区间无效");
    }

    private QianDataMisconfiguredException Invalid(string reason) => new("方言 " + Code + " 的类型映射无效：" + reason + "。");

    [GeneratedRegex("^[a-z][a-z0-9]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}

/// <summary>GET /api/data/meta/types 的响应：系统基准类型（种子顺序）与已注册方言（按代码排序）。</summary>
public sealed record DataTypeCatalog(IReadOnlyList<BaseDataType> Types, IReadOnlyList<DbDialect> Dialects);

/// <summary>基准类型目录：系统类型来自 qian.data 种子，方言映射来自 Data.Dialects 注册表；均为可查询元数据。</summary>
public interface IDataTypeCatalog
{
    /// <summary>当前快照。</summary>
    DataTypeCatalog Snapshot();
    /// <summary>按代码查找基准类型。</summary>
    BaseDataType? FindType(string code);
    /// <summary>按方言映射校验字段定义；方言未注册抛 DATA_DIALECT_NOT_FOUND，类型未知或超出约束抛 DATA_FIELD_DEFINITION_INVALID。</summary>
    DataFieldMapping MapField(string dialect, DataFieldDefinition field);
    /// <summary>删除基准类型：系统类型拒绝（DATA_TYPE_PROTECTED），未知类型为 DATA_TYPE_NOT_FOUND。S1 只有系统类型，自定义类型随 S2 建模交付。</summary>
    ValueTask DeleteTypeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>随 Definition 发布的基准类型种子与方言数据读取入口。</summary>
public static class DataTypeContracts
{
    /// <summary>系统基准类型，来源为同包嵌入的 base-data-types.json。</summary>
    public static IReadOnlyList<BaseDataType> SystemTypes { get; } = ReadSystemTypes();

    /// <summary>读取方言组件的 qian.data.dialect/v1 数据并按系统基准类型核对；未知成员、缺少成员或映射不一致时抛出。</summary>
    public static DbDialect ReadDialect(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        DbDialect dialect;
        try { dialect = JsonSerializer.Deserialize(utf8Json, DataContractJson.DialectTypeInfo) ?? throw new JsonException("方言数据为空。"); }
        catch (JsonException error) { throw new QianDataMisconfiguredException("方言数据不符合 qian.data.dialect/v1：" + error.Message); }
        dialect.Validate(SystemTypes);
        return dialect;
    }

    private static IReadOnlyList<BaseDataType> ReadSystemTypes()
    {
        var seed = JsonSerializer.Deserialize(DataContractSchemas.BaseDataTypes, DataContractJson.DataTypeCatalogTypeInfo)
            ?? throw new InvalidOperationException("基准类型种子为空。");
        if (seed.Types.Count == 0 || seed.Dialects.Count != 0 || seed.Types.Any(type => !type.IsSystem))
            throw new InvalidOperationException("基准类型种子只能包含系统类型。");
        return seed.Types;
    }
}
