using System.Text.Json;
using Json.Schema;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz.Tests;

public sealed class DataScopeContractTests
{
    private const string Resource = "qian.sample.crm:customer";
    [Fact]
    public void 规则Schema与样例可离线校验_元数据直接复用过滤AST矩阵和十五个系统变量()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(DataContractSchemas.Filter, options);
        var rule = JsonSchema.FromText(DataScopeContracts.RuleSchema, options);
        var meta = Assert.IsType<JsonSchema>(options.SchemaRegistry.Get(new Uri("https://json-schema.org/draft/2020-12/schema")));
        var metadata = DataScopeContracts.Metadata;
        Assert.True(meta.Evaluate(metadata.GetProperty("dataScopeRule")).IsValid);
        Assert.True(meta.Evaluate(metadata.GetProperty("filterAst")).IsValid);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(DataContractSchemas.Filter).RootElement, metadata.GetProperty("filterAst")));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(DataContractSchemas.SystemVariables).RootElement, metadata.GetProperty("systemVariables")));
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(DataContractSchemas.OperatorMatrix).RootElement, metadata.GetProperty("operators")));
        Assert.Equal(15, metadata.GetProperty("systemVariables").EnumerateObject().Count());
        Assert.All(metadata.GetProperty("systemVariables").EnumerateObject(), variable => Assert.False(string.IsNullOrWhiteSpace(variable.Value.GetProperty("description").GetString())));
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "contracts/authz/v1/fixtures"), "*.json"))
        {
            var raw = File.ReadAllText(file); Assert.True(rule.Evaluate(JsonDocument.Parse(raw).RootElement).IsValid, file);
            var parsed = JsonSerializer.Deserialize(raw, DataScopeContracts.RuleTypeInfo)!;
            Assert.True(rule.Evaluate(JsonSerializer.SerializeToElement(parsed, DataScopeContracts.RuleTypeInfo)).IsValid);
        }
    }

    [Theory]
    [InlineData("{\"resource\":\"qian.sample.crm:customer\",\"allData\":false}")]
    [InlineData("{\"resource\":\"qian.sample.crm:customer\",\"allData\":true,\"filter\":{\"logic\":\"and\",\"groups\":[]}}")]
    [InlineData("{\"resource\":\"qian.sample.crm:customer\\n\",\"allData\":true}")]
    [InlineData("{\"resource\":\"qian.sample.crm:customer\",\"allData\":true,\"tenantId\":\"a\"}")]
    [InlineData("{\"resource\":\"customer\",\"allData\":true}")]
    public void 不完整或矛盾的规则和注入字段拒绝(string json)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() }; JsonSchema.FromText(DataContractSchemas.Filter, options);
        Assert.False(JsonSchema.FromText(DataScopeContracts.RuleSchema, options).Evaluate(JsonDocument.Parse(json).RootElement).IsValid);
    }

    [Fact]
    public void 同角色AND跨角色OR_重复角色分段仍然AND_不把合并展开为SQL()
    {
        var plan = DataScopeComposition.ForResource(Resource, [new("sales", [Rule("team", "sales")]),
            new("sales", [Rule("state", "open")]), new("owner", [Rule("owner", "me")])]);
        Assert.False(plan.AllData); Assert.Equal(2, plan.Roles.Count);
        Assert.Equal(2, Assert.Single(plan.Roles, role => role.Role == "sales").Filters.Count);
        var rows = new[] { Row("sales", "open", "other"), Row("sales", "closed", "other"), Row("finance", "closed", "me"), Row("finance", "open", "other") };
        Assert.Equal([true, false, true, false], rows.Select(row => Evaluate(plan, row)));
        // 计划保留两个 Filter AST 的逻辑边界，不能把组内 OR 与角色间 OR 混成一层。
        var disjunction = new FilterExpression("or", [new("and", [new("team", "eq", JsonSerializer.SerializeToElement("sales"))]),
            new("and", [new("owner", "eq", JsonSerializer.SerializeToElement("me"))])]);
        var nested = DataScopeComposition.ForResource(Resource, [new("reviewer", [new(Resource, false, JsonSerializer.SerializeToElement(disjunction, DataContractJson.FilterTypeInfo)), Rule("state", "open")])]);
        Assert.Equal([true, false, false, false], rows.Select(row => Evaluate(nested, row)));
        var serialized = JsonSerializer.SerializeToElement(plan, DataScopeContracts.PlanTypeInfo);
        Assert.Equal(Resource, serialized.GetProperty("resource").GetString());
    }

    [Fact]
    public void 全部数据仅放开命名资源_空角色空规则和其他资源的全部数据不放行()
    {
        var all = DataScopeComposition.ForResource(Resource, [new("owner", [new(Resource, true)]), new("sales", [Rule("team", "sales")])]);
        Assert.True(all.AllData); Assert.Empty(all.Roles);
        foreach (var roles in new RoleDataScope[][] { [], [new("empty", [])], [new("other", [new("qian.sample.crm:invoice", true)])] })
        {
            var denied = DataScopeComposition.ForResource(Resource, roles);
            Assert.False(denied.AllData); Assert.Empty(denied.Roles); Assert.False(Evaluate(denied, Row("sales", "open", "me")));
        }
        Assert.Throws<ArgumentException>(() => DataScopeComposition.ForResource(Resource, [new("bad", [new(Resource, false)])]));
        Assert.Throws<ArgumentException>(() => DataScopeComposition.ForResource(Resource, [new("bad", [new(Resource, true, Rule("team", "sales").Filter)])]));
    }

    private static DataScopeRule Rule(string field, string value) => new(Resource, false, JsonSerializer.SerializeToElement(new FilterExpression("and", [new("and", [new(field, "eq", JsonSerializer.SerializeToElement(value))])]), DataContractJson.FilterTypeInfo));
    private static Dictionary<string, string> Row(string team, string state, string owner) => new() { ["team"] = team, ["state"] = state, ["owner"] = owner };
    // 仅测试中的集合语义解释器；生产仍无数据权限编译器，不能把它作为执行入口。
    private static bool Evaluate(DataScopePlan plan, Dictionary<string, string> row) => plan.AllData || plan.Roles.Any(role => role.Filters.Select(raw => raw.Deserialize(DataContractJson.FilterTypeInfo)!).All(filter =>
        Combine(filter.Logic, filter.Groups.Select(group => Combine(group.Logic, group.Conditions.Select(condition => row[condition.Field] == condition.Value!.Value.GetString()))))));
    private static bool Combine(string logic, IEnumerable<bool> terms) => logic == "and" ? terms.All(value => value) : terms.Any(value => value);
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("找不到仓库。");
    }
}
