using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qian.Data;

/// <summary>五处共用的两层过滤表达式；空 and 为真、空 or 为假。</summary>
public sealed record FilterExpression(string Logic, IReadOnlyList<FilterGroup> Groups);
/// <summary>一组按 and 或 or 组合的条件；组内至少一项。</summary>
public sealed record FilterGroup(string Logic, IReadOnlyList<FilterCondition> Conditions);
/// <summary>过滤条件；非空值运算符恰有 value 或 sysVar，空值运算符均不携带。</summary>
public sealed record FilterCondition(string Field, string Op, JsonElement? Value = null, string? SysVar = null);
/// <summary>过滤契约错误；宿主映射为 INPUT_INVALID 的字段错误，不生成 SQL。</summary>
public sealed record FilterValidationIssue(string Path, string Message);
/// <summary>可信宿主解析的系统变量元数据；目录本身不解析身份或组织权限。</summary>
public sealed record FilterSystemVariable(string Description, bool Collection, IReadOnlyList<string> FieldTypes);

/// <summary>随 Definition 发布的过滤矩阵、系统变量与语义检查。</summary>
public static partial class FilterContracts
{
    /// <summary>基准字段类型到运算符的只读矩阵，来源为同包 JSON 目录。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Operators { get; } = ReadOperators();
    /// <summary>系统变量唯一目录；集合变量只可用于 in/notIn。</summary>
    public static IReadOnlyDictionary<string, FilterSystemVariable> SystemVariables { get; } = ReadVariables();

    /// <summary>按编译后字段白名单验证类型、运算符、操作数与系统变量，返回 JSON Pointer 错误。</summary>
    public static IReadOnlyList<FilterValidationIssue> Validate(FilterExpression expression, IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(fields);
        var errors = new List<FilterValidationIssue>();
        CheckLogic(expression.Logic, "#/logic", errors);
        if (expression.Groups is null)
        {
            errors.Add(new("#/groups", "缺少条件组。"));
            return errors.AsReadOnly();
        }
        for (var groupIndex = 0; groupIndex < expression.Groups.Count; groupIndex++)
        {
            var group = expression.Groups[groupIndex];
            var path = $"#/groups/{groupIndex}";
            if (group is null) { errors.Add(new(path, "条件组不能为空。")); continue; }
            CheckLogic(group.Logic, path + "/logic", errors);
            if (group.Conditions is null || group.Conditions.Count == 0)
            {
                errors.Add(new(path + "/conditions", "条件组至少包含一个条件。"));
                continue;
            }
            for (var index = 0; index < group.Conditions.Count; index++)
                CheckCondition(group.Conditions[index], fields, $"{path}/conditions/{index}", errors);
        }
        return errors.AsReadOnly();
    }

    private static void CheckLogic(string logic, string path, List<FilterValidationIssue> errors)
    {
        if (logic is not ("and" or "or")) errors.Add(new(path, "逻辑运算符必须为 and 或 or。"));
    }

    private static void CheckCondition(FilterCondition? condition, IReadOnlyDictionary<string, string> fields,
        string path, List<FilterValidationIssue> errors)
    {
        if (condition is null) { errors.Add(new(path, "条件不能为空。")); return; }
        if (string.IsNullOrEmpty(condition.Field) || condition.Field.Length > 200 || !FieldPattern().IsMatch(condition.Field)
            || condition.Field.Split('.').Any(part => part.Equals("tenant_id", StringComparison.OrdinalIgnoreCase)
                || part.Equals("tenantId", StringComparison.OrdinalIgnoreCase))
            || !fields.TryGetValue(condition.Field, out var fieldType) || !Operators.TryGetValue(fieldType, out var allowed))
        {
            errors.Add(new(path + "/field", "字段不在编译后白名单中、属于租户字段或类型未登记。"));
            return;
        }
        if (!allowed.Contains(condition.Op, StringComparer.Ordinal))
        {
            errors.Add(new(path + "/op", "该字段类型不支持此运算符。"));
            return;
        }
        if (condition.Op is "isNull" or "notNull")
        {
            if (condition.Value.HasValue || condition.SysVar is not null)
                errors.Add(new(path, "空值运算符不接受 value 或 sysVar。"));
            return;
        }
        if (condition.Value.HasValue == (condition.SysVar is not null))
        {
            errors.Add(new(path, "条件必须且只能提供 value 或 sysVar。"));
            return;
        }
        if (condition.SysVar is not null)
        {
            if (!SystemVariables.TryGetValue(condition.SysVar, out var variable)
                || !variable.FieldTypes.Contains(fieldType, StringComparer.Ordinal)
                || variable.Collection != (condition.Op is "in" or "notIn") || condition.Op == "between")
                errors.Add(new(path + "/sysVar", "系统变量未登记，或其类型/基数与条件不匹配。"));
            return;
        }
        var value = condition.Value!.Value;
        if (condition.Op is "in" or "notIn" or "between")
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0
                || (condition.Op == "between" && value.GetArrayLength() != 2))
            {
                errors.Add(new(path + "/value", "in/notIn 需要非空数组，between 需要两个边界。"));
                return;
            }
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (!MatchesType(item, fieldType)) errors.Add(new($"{path}/value/{index}", "值不符合字段类型。"));
                index++;
            }
        }
        else if (!MatchesType(value, fieldType)) errors.Add(new(path + "/value", "值不符合字段类型；空值比较请使用 isNull/notNull。"));
    }

    private static bool MatchesType(JsonElement value, string type) => type switch
    {
        "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        "bigint" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "decimal" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        "float" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
        "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "varchar" or "text" => value.ValueKind == JsonValueKind.String,
        "uuid" => value.ValueKind == JsonValueKind.String && Guid.TryParseExact(value.GetString(), "D", out _),
        "datetime" => value.ValueKind == JsonValueKind.String && DatePattern().IsMatch(value.GetString()!)
            && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        "json" => value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined),
        _ => false
    };

    private static ReadOnlyDictionary<string, IReadOnlyList<string>> ReadOperators()
    {
        using var document = JsonDocument.Parse(DataContractSchemas.OperatorMatrix);
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (IReadOnlyList<string>)Array.AsReadOnly(
                property.Value.EnumerateArray().Select(item => item.GetString()!).ToArray()), StringComparer.Ordinal));
    }

    private static ReadOnlyDictionary<string, FilterSystemVariable> ReadVariables()
    {
        using var document = JsonDocument.Parse(DataContractSchemas.SystemVariables);
        return new ReadOnlyDictionary<string, FilterSystemVariable>(document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name, property => new FilterSystemVariable(property.Value.GetProperty("description").GetString()!,
                property.Value.GetProperty("collection").GetBoolean(), Array.AsReadOnly(property.Value.GetProperty("fieldTypes")
                    .EnumerateArray().Select(item => item.GetString()!).ToArray())), StringComparer.Ordinal));
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPattern();
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,9})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}
