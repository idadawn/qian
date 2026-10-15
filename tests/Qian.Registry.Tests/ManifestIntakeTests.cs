using System.Text;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

// S1-RT-004 TCK：共享表面 fixture（tck/surfaces/cases.json，.NET 与 TS 校验器逐项一致）中的清单准入用例，经注册表的候选制品登记入口执行。
// 反例一律 INPUT_INVALID，字段错误的 JSON Pointer 与 fixture 一致，候选目录不留记录；正例登记为 Submitted。
// 宿主预装集合（data → Data、registry → Registry）就是 inject 的服务键目录，注册表总会带上它。
public sealed partial class PatchPersistenceTests
{
    // 本任务的五类反例及其对照正例；缺字段一类按前缀覆盖 Schema 的全部 required。
    private static readonly string[] IntakeCases = ["manifest-oci-tag", "manifest-oci-digest", "manifest-scope-kind", "tool-top-level-tenant",
        "inject-without-provider", "inject-with-provider", "inject-unchecked-without-catalog"];

    [Fact]
    public async Task 清单准入TCK_缺必填字段_OCI用tag_scope种类非法_工具输入含租户字段_inject无Provider_经注册表登记均失败并带JSONPointer()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant);
        var catalog = new PostgresArtifactCatalog(running.Host.Services.GetRequiredService<IDataService>());
        var cases = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "tck", "surfaces", "cases.json"))).EnumerateArray()
            .Where(item => item.GetProperty("name").GetString() is { } name && (name.StartsWith("manifest-missing-", StringComparison.Ordinal) || IntakeCases.Contains(name))).ToArray();
        // 「缺任一 S1 必填字段」按 Schema 的 required 逐个覆盖，不漏不多。
        var required = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "schemas", "v2", "plugin.schema.json")))
            .GetProperty("required").EnumerateArray().Select(field => field.GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(required, cases.Select(item => item.GetProperty("name").GetString()!).Where(name => name.StartsWith("manifest-missing-", StringComparison.Ordinal))
            .Select(name => name["manifest-missing-".Length..]).Order(StringComparer.Ordinal));
        var rejected = 0; var accepted = 0;
        foreach (var item in cases)
        {
            var name = item.GetProperty("name").GetString()!;
            var files = item.GetProperty("files").EnumerateObject().ToDictionary(file => file.Name,
                file => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(file.Value.GetString()!), StringComparer.Ordinal);
            var digest = TestDigest(files);
            var expected = item.GetProperty("expected").EnumerateArray().Select(issue => issue.GetProperty("pointer").GetString()!).ToArray();
            // fixture 演示离线校验省略服务键目录时不检查 inject；注册表的准入始终带预装目录，同一清单因此被拒。
            if (name == "inject-unchecked-without-catalog") expected = [".qian-plugin/plugin.json#/inject/0"];
            if (expected.Length == 0)
            {
                var entry = await running.Registry.SubmitArtifactAsync(files, digest, "tck", Ct);
                Assert.Equal("Submitted", entry.State); Assert.Equal(digest, entry.Digest); accepted++;
                continue;
            }
            var error = await Assert.ThrowsAsync<QianErrorException>(() => running.Registry.SubmitArtifactAsync(files, digest, "tck", Ct).AsTask());
            Assert.Equal("INPUT_INVALID", error.Error.Code.Code);
            Assert.All(error.Error.Errors, field => Assert.Equal("REGISTRY_INPUT_INVALID", field.Code.Code));
            Assert.True(expected.Order(StringComparer.Ordinal).SequenceEqual(error.Error.Errors.Select(field => field.Pointer).Order(StringComparer.Ordinal)),
                name + ": " + string.Join(", ", error.Error.Errors.Select(field => field.Pointer)));
            Assert.Null(await catalog.GetAsync(digest, Ct));
            rejected++;
        }
        Assert.Equal(required.Length + IntakeCases.Length, cases.Length);
        Assert.Equal(required.Length + 5, rejected); Assert.Equal(2, accepted);
    }
}
