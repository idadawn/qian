using System.Text;
using System.Text.Json;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// S1-AGT-002 验收 2（TCK）：共享表面 fixture（tck/surfaces/cases.json，.NET 与 TS 校验器逐项一致）中工具输入含租户字段的用例，打成已签名的声明式制品经
// InstallAsync 安装。反例一律 INPUT_INVALID，字段错误为 REGISTRY_INPUT_INVALID，pointer 与 fixture 一致，不登记修订、不留候选制品；只是含有 tenant 的名称照常安装。
public sealed class TenantFieldInstallTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    // 顶层（tool Schema 的 propertyNames）与嵌套（语义检查）各一组名称写法，外加 $defs、数组项、组合子句；properties 之外的写法（S1-AGT-004）：
    // required、dependentRequired 的名称与数组项、dependentSchemas、patternProperties 的模式（含无法判定的写法）、顶层的 required 与 patternProperties、
    // 名称表里叫 default 的属性、propertyNames 用 enum / const 列出的名称；S1-AGT-007 补的写法：propertyNames 的 then / else / allOf / oneOf、
    // 旧式 dependencies 的名称、数组项与 Schema 值、definitions 与 $defs 里叫实例关键字的条目、途经实例数据或停在名称表本身上的引用；以及对照正例。
    private static readonly string[] TenantCases = ["tool-top-level-tenant", "tool-top-level-tenant-bare", "tool-top-level-tenant-snake", "tool-top-level-tenant-kebab",
        "tool-top-level-tenant-upper", "tool-nested-tenant", "tool-nested-tenant-bare", "tool-nested-tenant-snake", "tool-nested-tenant-kebab", "tool-nested-tenant-pascal",
        "tool-defs-tenant", "tool-array-items-tenant", "tool-any-of-tenant", "tool-required-tenant", "tool-top-level-required-tenant", "tool-dependent-required-tenant",
        "tool-dependent-required-key-tenant", "tool-dependent-schemas-tenant", "tool-pattern-properties-tenant-prefix", "tool-pattern-properties-tenant-case",
        "tool-pattern-properties-tenant-wildcard", "tool-pattern-properties-tenant-unsupported", "tool-top-level-pattern-properties-tenant", "tool-nested-tenant-keyword-name",
        "tool-property-names-tenant-enum", "tool-property-names-tenant-const", "tool-property-names-tenant-then", "tool-property-names-tenant-else",
        "tool-property-names-tenant-all-of", "tool-property-names-tenant-one-of", "tool-dependencies-tenant-key", "tool-dependencies-tenant-item",
        "tool-dependencies-tenant-schema", "tool-definitions-tenant-keyword-name", "tool-defs-tenant-keyword-name", "tool-ref-tenant-default",
        "tool-ref-tenant-percent-encoded", "tool-ref-tenant-encoded-slash", "tool-ref-tenant-encoded-leading-slash", "tool-ref-tenant-name-table",
        "tool-dynamic-ref-tenant", "tool-ref-tenant-id-shadow", "tool-tenant-lookalike"];

    [Fact]
    public async Task 工具输入含租户字段的制品经InstallAsync安装失败_INPUT_INVALID带fixture的JSONPointer_不登记修订也不留候选制品()
    {
        await using var host = await LifecycleHost.StartAsync(postgres, ct: Ct);
        var key = new SigningKey();
        host.Trust(0, "k1", key, host.Time.Now.AddDays(-1));
        var cases = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(LifecycleHost.RepositoryRoot(), "tck", "surfaces", "cases.json"))).EnumerateArray()
            .Where(item => item.GetProperty("name").GetString() is { } name && name.StartsWith("tool-", StringComparison.Ordinal) && name.Contains("tenant", StringComparison.Ordinal))
            .ToArray();
        // 按名称核对整批用例，生成器改名或漏生成不会悄悄减少覆盖。
        Assert.Equal(TenantCases.Order(StringComparer.Ordinal), cases.Select(item => item.GetProperty("name").GetString()!).Order(StringComparer.Ordinal));
        var rejected = new List<string>(); string? accepted = null;
        foreach (var item in cases)
        {
            var name = item.GetProperty("name").GetString()!;
            var files = item.GetProperty("files").EnumerateObject().ToDictionary(file => file.Name,
                file => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(file.Value.GetString()!), StringComparer.Ordinal);
            var artifact = Artifacts.Signed(files, key);
            var expected = item.GetProperty("expected").EnumerateArray().Select(issue => issue.GetProperty("pointer").GetString()!).ToArray();
            if (expected.Length == 0)
            {
                Assert.True(item.GetProperty("valid").GetBoolean(), name);
                var revision = await host.Lifecycle.InstallAsync(artifact, "tck", Ct);
                Assert.Equal((PluginRevisionStates.Installed, artifact.Digest), (revision.State, revision.ArtifactDigest));
                accepted = artifact.Digest;
                continue;
            }
            var error = (await Assert.ThrowsAsync<QianErrorException>(() => host.Lifecycle.InstallAsync(artifact, "tck", Ct).AsTask())).Error;
            Assert.Equal("INPUT_INVALID", error.Code.Code);
            Assert.Equal("REGISTRY_INPUT_INVALID", Assert.Single(error.Errors!).Code.Code);
            Assert.True(expected.SequenceEqual(error.Errors!.Select(field => field.Pointer)), name + ": " + string.Join(", ", error.Errors!.Select(field => field.Pointer)));
            rejected.Add(artifact.Digest);
        }
        Assert.Equal(TenantCases.Length - 1, rejected.Count);
        Assert.NotNull(accepted);
        // 失败的安装不登记修订，也不在注册表留下候选制品；只有对照正例成为 qian.fixture 的修订 1。
        Assert.Equal(1, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.plugin_revisions WHERE plugin_id = 'qian.fixture'", Ct));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres,
            $"SELECT count(*) FROM plugin_qian_registry.plugin_artifacts WHERE digest IN ({string.Join(", ", rejected.Select(digest => "'" + digest + "'"))})", Ct));
        Assert.Equal(1, await LifecycleHost.CountAsync(postgres, $"SELECT count(*) FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{accepted}'", Ct));
    }
}
