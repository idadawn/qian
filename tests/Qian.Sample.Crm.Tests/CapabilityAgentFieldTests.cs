using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Qian.Plugin.Sdk;
using Qian.Surfaces;

namespace Qian.Sample.Crm.Tests;

// S1-AGT-002 验收 1（IT）：GET /api/capabilities 对同一插件（qian.sample.crm）返回的条目带全部 Agent 字段，取值与 [Capability] 声明一致（缺省值也写进条目，
// 不在读取时派生）；schemaSha256 与打包时的值一致。「打包时」取 CI 同一个 qian-plugin 工具进程对实际程序集 sync 出的 tools/<name>.json
// （仓库不为示例宿主建清单，组件目录在临时目录里只放一份最小清单）。
public sealed partial class CustomerCommandTests
{
    private static readonly string[] AgentFields = ["sideEffect", "policyHint", "idempotent", "async", "requiredScopes", "requiredSecrets", "dataClassification", "costTier",
        "contractVersion", "schemaSha256", "audit", "agent"];
    // 三个能力的声明：create 写且幂等（audit 由写派生为 true）、get 显式 audit、list 只读不审计；其余取 [Capability] 的缺省。
    private static readonly (Type Request, string Name, string SideEffect, bool Idempotent, bool Audit, string Scope)[] AgentFieldTable =
    [
        (typeof(CreateCustomerCommand), "customer.create", "write", true, true, "crm.write"),
        (typeof(GetCustomerQuery), "customer.get", "read", false, true, "crm.read"),
        (typeof(ListCustomersQuery), "customer.list", "read", false, false, "crm.read"),
    ];
    private static readonly string[] PackScopes = ["global", "tenant"];

    [Fact]
    public async Task 能力目录对同一插件的条目带全部Agent字段_取值同Capability声明_schemaSha256与打包时sync出的工具文件一致()
    {
        // 工具拒绝经过符号链接的派生路径（macOS 的临时目录在 /var -> /private/var 之下），组件目录放在测试输出目录里，与 SDK 的工具验收相同。
        var component = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "crm-pack-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            // 打包：最小清单 + 实际程序集，经 CI 同一个工具进程 sync 出 tools/*.json 与清单派生字段。
            Directory.CreateDirectory(Path.Combine(component, ".qian-plugin"));
            await File.WriteAllTextAsync(Path.Combine(component, ".qian-plugin", "plugin.json"), JsonSerializer.Serialize(new
            {
                id = "qian.sample.crm", version = "1.0.0", description = "参考宿主 CRM 组件的打包验收清单。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = "CRM", category = "testing" }, inject = Array.Empty<string>(), provides = Array.Empty<string>(),
                policy = new { tier = "official", sandbox = "none", authentication = "ON_INSTALL" }, scopes = PackScopes
            }), Ct);
            var (exitCode, output, error) = await PackTool("sync", typeof(CrmPlugin).Assembly.Location, component);
            Assert.True(exitCode == 0, output + error);
            var packed = Directory.EnumerateFiles(Path.Combine(component, "tools"), "*.json").ToDictionary(path => Path.GetFileNameWithoutExtension(path),
                path => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)), StringComparer.Ordinal);
            Assert.Equal(AgentFieldTable.Select(row => row.Name).Order(StringComparer.Ordinal), packed.Keys.Order(StringComparer.Ordinal));

            await using var host = await Start(capabilities: true);
            using var response = await CatalogRequest(host, A, "/api/capabilities");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(AgentFieldTable.Length, items.Length);
            foreach (var row in AgentFieldTable)
            {
                var entry = Assert.Single(items, item => item.GetProperty("id").GetString() == "qian.sample.crm/" + row.Name);
                var declared = row.Request.GetCustomAttribute<CapabilityAttribute>()!;
                Assert.Equal(row.Name, declared.Name);
                // 全部 Agent 字段都在条目里，取值同声明（含缺省的 policyHint allow、async none、requiredSecrets []、costTier low、requiresConfirmation false）。
                Assert.Equal((row.SideEffect, "allow", row.Idempotent, "none", "pii", "low", "crm-v1", row.Audit),
                    (entry.GetProperty("sideEffect").GetString(), entry.GetProperty("policyHint").GetString(), entry.GetProperty("idempotent").GetBoolean(),
                        entry.GetProperty("async").GetString(), entry.GetProperty("dataClassification").GetString(), entry.GetProperty("costTier").GetString(),
                        entry.GetProperty("contractVersion").GetString(), entry.GetProperty("audit").GetBoolean()));
                Assert.Equal((declared.SideEffect, declared.PolicyHint, declared.Idempotent, declared.Async, declared.DataClassification, declared.CostTier,
                    declared.ContractVersion, declared.Audit), (row.SideEffect, "allow", row.Idempotent, "none", "pii", "low", "crm-v1", row.Audit));
                Assert.Equal([row.Scope], entry.GetProperty("requiredScopes").EnumerateArray().Select(item => item.GetString()));
                Assert.Equal([row.Scope], declared.RequiredScopes.AsEnumerable());
                Assert.Empty(entry.GetProperty("requiredSecrets").EnumerateArray()); Assert.Empty(declared.RequiredSecrets);
                Assert.Equal((true, false), (entry.GetProperty("agent").GetProperty("expose").GetBoolean(), entry.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean()));
                Assert.Equal((true, false), (declared.AgentExpose, declared.RequiresConfirmation));
                // 与打包时的工具文件逐字段相同；schemaSha256 两条路径（运行时目录编译、打包时 sync）一致，并恰是条目自身 description / inputSchema / name 的 JCS 哈希。
                var tool = packed[row.Name];
                foreach (var field in AgentFields)
                    Assert.True(JsonElement.DeepEquals(tool.GetProperty(field), entry.GetProperty(field)), row.Name + "#/" + field);
                Assert.True(JsonElement.DeepEquals(tool.GetProperty("inputSchema"), entry.GetProperty("inputSchema")), row.Name + "#/inputSchema");
                var hash = entry.GetProperty("schemaSha256").GetString()!;
                Assert.Matches("^[0-9a-f]{64}$", hash);
                Assert.Equal(tool.GetProperty("schemaSha256").GetString(), hash);
                Assert.Equal(ToolContractHash.Compute(entry), hash);
            }
            // 清单派生字段同样来自这次打包：工具索引恰为三个文件。
            var manifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(component, ".qian-plugin", "plugin.json"), Ct));
            Assert.Equal(AgentFieldTable.Select(row => "tools/" + row.Name + ".json").Order(StringComparer.Ordinal),
                manifest.GetProperty("x-qian-generated").GetProperty("tools").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal));
        }
        finally { Directory.Delete(component, true); }
    }

    // 与 CI 的 qian-plugin check|sync 同一个工具程序（随项目引用复制到测试输出目录）。
    private static async Task<(int ExitCode, string Output, string Error)> PackTool(string verb, string assembly, string component)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { Path.Combine(AppContext.BaseDirectory, "Qian.Plugin.Tool.dll"), verb, assembly, component }) start.ArgumentList.Add(value);
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync(Ct); var errors = child.StandardError.ReadToEndAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await child.WaitForExitAsync(timeout.Token); }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
        return (child.ExitCode, await output, await errors);
    }
}
