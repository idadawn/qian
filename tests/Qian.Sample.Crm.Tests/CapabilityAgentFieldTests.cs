using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Qian.Plugin.Sdk;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected static readonly string[] AgentFields = ["sideEffect", "policyHint", "idempotent", "async", "requiredScopes", "requiredSecrets", "dataClassification", "costTier",
        "contractVersion", "schemaSha256", "audit", "agent"];
    // 探针只在 crm.admin scope 下可见；其余用例的令牌不带它，看到的仍是三条。
    private protected const string AgentFieldScopes = "crm.read crm.write crm.admin";
    private protected sealed record AgentFieldRow(Type Request, string Name, string SideEffect, string PolicyHint, bool Idempotent, string Async, string[] Scopes, string[] Secrets,
        string Classification, string CostTier, string ContractVersion, bool Audit, bool Expose, bool RequiresConfirmation);
    // 逐字段写明的期望值：create 写且幂等（audit 由写派生为 true）、get 显式 audit、list 只读不审计，三条其余取 [Capability] 的缺省；purge 每个字段都取非缺省值。
    private protected static readonly AgentFieldRow[] AgentFieldTable =
    [
        new(typeof(CreateCustomerCommand), "customer.create", "write", "allow", true, "none", ["crm.write"], [], "pii", "low", "crm-v1", true, true, false),
        new(typeof(GetCustomerQuery), "customer.get", "read", "allow", false, "none", ["crm.read"], [], "pii", "low", "crm-v1", true, true, false),
        new(typeof(ListCustomersQuery), "customer.list", "read", "allow", false, "none", ["crm.read"], [], "pii", "low", "crm-v1", false, true, false),
        new(typeof(PurgeCustomersCommand), "customer.purge", "destructive", "deny", true, "task", ["crm.admin"], ["crm-archive-key"], "internal", "high", "crm-admin-v1", true, false, true),
    ];
}

// S1-AGT-002 验收 1（IT），S1-AGT-006 补区分度（R112）：GET /api/capabilities 对同一插件（qian.sample.crm）返回的条目带全部 Agent 字段，取值与 [Capability] 声明一致
// （缺省值也写进条目，不在读取时派生）；schemaSha256 与打包时的值一致。「打包时」取仓库提交的 samples/Qian.Sample.Crm/tools/<name>.json：它由 qian-plugin sync 生成，
// CI 的清单门禁（scripts/check_plugin_manifests.py）对实际程序集重新计算并比对漂移，比对的两侧不是同一次计算。四条能力的每个 Agent 字段都至少有两种取值，
// 其中 customer.purge 是为此而设的探针（policyHint 取 destructive 的非缺省值 deny），读取时按其它字段派生或写死任一字段，都会与声明或提交的工具文件不符。
public sealed partial class CapabilityTests
{
    [Fact]
    public async Task 能力目录对同一插件的条目带全部Agent字段_取值同Capability声明_与仓库提交的打包工具文件逐字段一致()
    {
        // 打包时的工具文件：仓库提交的 tools/*.json，恰为四条能力各一份；清单派生字段的工具索引也恰是这四份。
        var component = Path.Combine(RepositoryRoot(), "samples/Qian.Sample.Crm");
        var packed = Directory.EnumerateFiles(Path.Combine(component, "tools"), "*.json").ToDictionary(path => Path.GetFileNameWithoutExtension(path),
            path => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)), StringComparer.Ordinal);
        Assert.Equal(AgentFieldTable.Select(row => row.Name).Order(StringComparer.Ordinal), packed.Keys.Order(StringComparer.Ordinal));
        var manifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(component, ".qian-plugin", "plugin.json"), Ct));
        Assert.Equal(AgentFieldTable.Select(row => "tools/" + row.Name + ".json").Order(StringComparer.Ordinal),
            manifest.GetProperty("x-qian-generated").GetProperty("tools").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal));

        await using var host = await Start(capabilities: true);
        using var response = await CatalogRequest(host, A, "/api/capabilities", AgentFieldScopes);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(AgentFieldTable.Length, items.Length);
        foreach (var row in AgentFieldTable)
        {
            var entry = Assert.Single(items, item => item.GetProperty("id").GetString() == "qian.sample.crm/" + row.Name);
            // 全部 Agent 字段都在条目里，取值同表（缺省值也写出）。
            Assert.Equal((row.SideEffect, row.PolicyHint, row.Idempotent, row.Async, row.Classification, row.CostTier, row.ContractVersion, row.Audit, row.Expose, row.RequiresConfirmation),
                (entry.GetProperty("sideEffect").GetString(), entry.GetProperty("policyHint").GetString(), entry.GetProperty("idempotent").GetBoolean(),
                    entry.GetProperty("async").GetString(), entry.GetProperty("dataClassification").GetString(), entry.GetProperty("costTier").GetString(),
                    entry.GetProperty("contractVersion").GetString(), entry.GetProperty("audit").GetBoolean(),
                    entry.GetProperty("agent").GetProperty("expose").GetBoolean(), entry.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean()));
            Assert.Equal(row.Scopes, entry.GetProperty("requiredScopes").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(row.Secrets, entry.GetProperty("requiredSecrets").EnumerateArray().Select(item => item.GetString()));
            // 表同 [Capability] 声明（反射读特性，与生成器各读一遍）。
            var declared = row.Request.GetCustomAttribute<CapabilityAttribute>()!;
            Assert.Equal((row.Name, row.SideEffect, row.PolicyHint, row.Idempotent, row.Async, row.Classification, row.CostTier, row.ContractVersion, row.Audit, row.Expose,
                row.RequiresConfirmation), (declared.Name, declared.SideEffect, declared.PolicyHint, declared.Idempotent, declared.Async, declared.DataClassification,
                declared.CostTier, declared.ContractVersion, declared.Audit, declared.AgentExpose, declared.RequiresConfirmation));
            Assert.Equal(row.Scopes, declared.RequiredScopes.AsEnumerable());
            Assert.Equal(row.Secrets, declared.RequiredSecrets.AsEnumerable());
            // 与仓库提交的打包工具文件逐字段相同；schemaSha256 即提交时 sync 算出的值。
            var tool = packed[row.Name];
            foreach (var field in AgentFields)
                Assert.True(JsonElement.DeepEquals(tool.GetProperty(field), entry.GetProperty(field)), row.Name + "#/" + field);
            Assert.True(JsonElement.DeepEquals(tool.GetProperty("inputSchema"), entry.GetProperty("inputSchema")), row.Name + "#/inputSchema");
            Assert.Matches("^[0-9a-f]{64}$", entry.GetProperty("schemaSha256").GetString()!);
        }
        Assert.Equal(AgentFieldTable.Length, items.Select(item => item.GetProperty("schemaSha256").GetString()).Distinct(StringComparer.Ordinal).Count());

        // 探针只登记契约：持 crm.admin 与写权限的服务主体调用也只得到 503 UNAVAILABLE，不执行清除。
        var invoke = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/qian.sample.crm/customer.purge/invoke")
            { Content = new StringContent(JsonSerializer.Serialize(new { idempotencyKey = Guid.CreateVersion7() }), Encoding.UTF8, "application/json") };
        invoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, AgentFieldScopes));
        using var refused = await host.Send(invoke);
        Assert.True(refused.StatusCode == HttpStatusCode.ServiceUnavailable, host.FailureEvidence + await refused.Content.ReadAsStringAsync(Ct));
        Assert.Equal("UNAVAILABLE", (await refused.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }
}
