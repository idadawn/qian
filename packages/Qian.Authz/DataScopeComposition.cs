using Qian.Authz.Abstractions;
using System.Text.Json;
using Json.Schema;

namespace Qian.Authz;

/// <summary>只构造固定合并语义的逻辑计划；S1 不编译 SQL、不解析系统变量，也不改变现有 fail-closed 数据/字段权限端口。</summary>
public static class DataScopeComposition
{
    private static readonly JsonSchema Schema = CreateSchema();
    private static JsonSchema CreateSchema()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(DataScopeContracts.Metadata.GetProperty("filterAst").GetRawText(), options);
        return JsonSchema.FromText(DataScopeContracts.RuleSchema, options);
    }
    /// <summary>按资源筛选、同角色合并为 AND 分支、不同角色 OR；无规则拒绝，任一 AllData 只放开目标资源。</summary>
    public static DataScopePlan ForResource(string resource, IReadOnlyList<RoleDataScope> roles)
    {
        ArgumentNullException.ThrowIfNull(roles); ValidateResource(resource);
        var groups = new SortedDictionary<string, List<JsonElement>>(StringComparer.Ordinal); var all = false;
        foreach (var role in roles)
        {
            if (role is null || string.IsNullOrWhiteSpace(role.Role) || role.Role.Length > 128 || role.Role != role.Role.Trim() || role.Role.Any(char.IsControl) || role.Rules is null)
                throw new ArgumentException("角色数据范围无效。", nameof(roles));
            foreach (var rule in role.Rules)
            {
                if (rule is null) throw new ArgumentException("规则不能为空。", nameof(roles));
                ValidateResource(rule.Resource);
                if (rule.AllData == (rule.Filter is not null)) throw new ArgumentException("allData 与 filter 必须互斥且完整。", nameof(roles));
                if (!Schema.Evaluate(JsonSerializer.SerializeToElement(rule, DataScopeContracts.RuleTypeInfo)).IsValid)
                    throw new ArgumentException("规则不符合数据权限 Schema。", nameof(roles));
                if (rule.Resource != resource) continue;
                if (rule.AllData) { all = true; continue; }
                if (!groups.TryGetValue(role.Role, out var filters)) groups.Add(role.Role, filters = []);
                filters.Add(rule.Filter!.Value.Clone());
            }
        }
        return new DataScopePlan(resource, all, all ? [] : Array.AsReadOnly(groups.Select(group =>
            new DataScopeRolePlan(group.Key, Array.AsReadOnly(group.Value.ToArray()))).ToArray()));
    }
    private static void ValidateResource(string resource)
    {
        if (resource is not { Length: > 0 and <= 384 } || !PermissionCode.IsValid(resource + ":read"))
            throw new ArgumentException("资源必须为组件 id:资源短名。", nameof(resource));
    }
}
