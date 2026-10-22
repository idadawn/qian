using System.Text.Json.Nodes;
using Qian.Plugin.Tool;
using Qian.Runtime;
using Qian.Surfaces;

namespace Qian.Plugin.Sdk.Tests;

public sealed class ManifestPlanTests
{
    [Fact]
    public void 同一清单派生入口服务键与工具_同步后无漂移_保留手写策略()
    {
        using var fixture = new ComponentDirectory(); var code = new ContractPluginQianCodeMetadata().Create();
        var plan = ManifestPlan.Create(fixture.Path, code);
        Assert.Contains(plan.Drift, d => d.File == ManifestPlan.ManifestFile && d.Pointer == "/runtime/entry");
        Assert.Contains(plan.Drift, d => d.File == "tools/customer.create.json");
        plan.Apply(); Assert.Empty(ManifestPlan.Create(fixture.Path, code).Drift);
        var manifest = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(fixture.Path, ManifestPlan.ManifestFile)))!;
        Assert.Equal("手写展示名", manifest["interface"]!["displayName"]!.GetValue<string>());
        Assert.Equal("official", manifest["policy"]!["tier"]!.GetValue<string>());
        Assert.True(manifest["x-qian-generated"]!["$generated"]!.GetValue<bool>());
        var check = new SurfaceValidator().ValidateComponent(fixture.Path, new Qian.Surfaces.Abstractions.SurfaceValidationOptions { Capabilities = code.Capabilities.Select(c => c.Id).ToArray() });
        Assert.True(check.IsValid, string.Join('\n', check.Diagnostics.Select(d => Qian.Runtime.Errors.ErrorDetailJson.Serialize(d.Error))));
        Assert.Equal(3, Directory.GetFiles(fixture.Path, "*.json", SearchOption.AllDirectories).Count(path => System.IO.Path.GetDirectoryName(path)!.EndsWith("tools", StringComparison.Ordinal)));
        Assert.Single(Directory.GetFiles(fixture.Path, "plugin.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void 删除代码能力后遗留工具会报漂移_只移除有双重所属证据的派生文件()
    {
        using var fixture = new ComponentDirectory(); var initial = new ContractPluginQianCodeMetadata().Create();
        ManifestPlan.Create(fixture.Path, initial).Apply();
        var without = new ComponentCodeMetadata(initial.ComponentId, initial.RuntimeEntry,
            initial.Inject.Select(name => new ServiceKey(name)).ToArray(), initial.Provides.Select(name => new ServiceKey(name)).ToArray(),
            initial.Capabilities.Where(c => c.Name != "customer.create").ToArray());
        var plan = ManifestPlan.Create(fixture.Path, without);
        Assert.Contains(plan.Drift, d => d.File == "tools/customer.create.json");
        Assert.True(File.Exists(System.IO.Path.Combine(fixture.Path, "tools/customer.create.json")));
        plan.Apply(); Assert.False(File.Exists(System.IO.Path.Combine(fixture.Path, "tools/customer.create.json")));
        Assert.Empty(ManifestPlan.Create(fixture.Path, without).Drift);
    }

    [Fact]
    public void 拒绝覆盖手写工具_路径越界_符号链接和重复键不写入()
    {
        using var fixture = new ComponentDirectory(); var code = new ContractPluginQianCodeMetadata().Create();
        Directory.CreateDirectory(System.IO.Path.Combine(fixture.Path, "tools"));
        var file = System.IO.Path.Combine(fixture.Path, "tools/customer.create.json"); File.WriteAllText(file, "{\"manual\":true}");
        Assert.Equal(ManifestErrors.HandwrittenConflict, Assert.Throws<ManifestException>(() => ManifestPlan.Create(fixture.Path, code)).Code); Assert.Equal("{\"manual\":true}", File.ReadAllText(file));
        File.Delete(file);
        var manifestFile = System.IO.Path.Combine(fixture.Path, ManifestPlan.ManifestFile); var original = File.ReadAllText(manifestFile);
        var manifest = JsonNode.Parse(original)!; manifest["surfaces"] = new JsonObject { ["tools"] = "./../escape" }; File.WriteAllText(manifestFile, manifest.ToJsonString());
        Assert.Equal(ManifestErrors.PathEscape, Assert.Throws<ManifestException>(() => ManifestPlan.Create(fixture.Path, code)).Code); File.WriteAllText(manifestFile, original);
        Directory.Delete(System.IO.Path.Combine(fixture.Path, "tools"));
        using var outside = new ComponentDirectory(); Directory.CreateSymbolicLink(System.IO.Path.Combine(fixture.Path, "tools"), outside.Path);
        Assert.Equal(ManifestErrors.PathEscape, Assert.Throws<ManifestException>(() => ManifestPlan.Create(fixture.Path, code)).Code); Directory.Delete(System.IO.Path.Combine(fixture.Path, "tools"));
        File.WriteAllText(manifestFile, original.Replace("\"id\":\"qian.sdk-tests\"", "\"id\":\"qian.sdk-tests\",\"id\":\"qian.sdk-tests\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => ManifestPlan.Create(fixture.Path, code));
    }

    [Fact]
    public void 权限元数据不能用风险映射扩大到未声明动作()
    {
        using var fixture = new ComponentDirectory();
        var original = new ContractPluginQianCodeMetadata().Create();
        var metadata = new ComponentCodeMetadata(original.ComponentId, original.RuntimeEntry, original.Inject.Select(name => new ServiceKey(name)).ToArray(),
            original.Provides, original.Capabilities, """[{"resource":"notes","actions":["read"],"risk":{"admin":"high"}}]""", "[]", null);
        var error = Assert.Throws<ManifestException>(() => ManifestPlan.Create(fixture.Path, metadata));
        Assert.Equal(ManifestErrors.MetadataInvalid, error.Code); Assert.Equal("metadata/permissions.json#/0/risk", error.Pointer);
        Assert.False(File.Exists(System.IO.Path.Combine(fixture.Path, "metadata/permissions.json")));
    }

    private sealed class ComponentDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "manifest-tests-" + Guid.NewGuid().ToString("N"));
        internal ComponentDirectory()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, ".qian-plugin"));
            File.WriteAllText(System.IO.Path.Combine(Path, ManifestPlan.ManifestFile), """
                {"id":"qian.sdk-tests","version":"1.0.0","description":"用于真实 SDK 代码契约和派生清单的验收组件。",
                 "author":{"name":"Qian"},"license":"Apache-2.0","interface":{"displayName":"手写展示名","category":"testing"},
                 "inject":[],"provides":[],"scopes":["global"],"runtime":{"form":"module","entry":"Before, Before"},
                 "policy":{"tier":"official","sandbox":"none","authentication":"ON_INSTALL"}}
                """);
        }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
