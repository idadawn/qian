using System.Reflection;
using System.Text.Json;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk;

namespace Qian.Files.Tests;

// 仓库里 qian.files 的唯一清单：代码字段由源生成器派生，每个能力一个工具文件且与生成契约一致（CI 另由 scripts/check_plugin_manifests.py 跑 Plugin.Tool check）。
// 已过锁步发布与私有源真实安装两门（S1-FILE-004，R100 口径），清单不再带 x-skeleton，NuGet 条目钉同一锁步版本并带归档摘要；权限与角色声明能被授权组件编译成目录。
public sealed class FilesComponentManifestTests
{
    private static readonly string Root = FilesMetadata.RepositoryRoot();

    [Fact]
    public void 清单代码字段与生成元数据一致_工具文件与六个能力契约一一对应_骨架标记已移除_NuGet条目钉锁步版本并带摘要()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "plugins/qian.files/.qian-plugin/plugin.json")));
        var json = manifest.RootElement;
        var factory = typeof(FilesPlugin).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType;
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(factory)!).Create();
        var runtime = json.GetProperty("runtime");
        Assert.Equal(("qian.files", "qian.files"), (code.ComponentId, json.GetProperty("id").GetString()));
        Assert.Equal((code.RuntimeEntry, "module"), (runtime.GetProperty("entry").GetString(), runtime.GetProperty("form").GetString()));
        Assert.Same(typeof(FilesPlugin), Type.GetType(code.RuntimeEntry, throwOnError: true));
        Assert.Equal(["Data"], code.Inject);
        // local 后端经 [Contributes] 声明为 Files.Backends 的具名贡献（R38），由生成器派生进 provides。
        Assert.Equal(["Files", "Files.Backends:local"], code.Provides);
        Assert.Equal(code.Inject, Strings(json.GetProperty("inject")));
        Assert.Equal(code.Provides, Strings(json.GetProperty("provides")));
        Assert.Equal((FilesDbContext.Schema, "./schemas/config.schema.json"), (runtime.GetProperty("dbSchema").GetString(), runtime.GetProperty("configSchema").GetString()));
        var packages = runtime.GetProperty("nuget").EnumerateArray().ToArray();
        Assert.Equal(["Qian.Files.Abstractions", "Qian.Files"], packages.Select(item =>
        {
            Assert.Equal(["id", "version", "digest"], item.EnumerateObject().Select(property => property.Name));
            Assert.Matches("^sha256:[0-9a-f]{64}$", item.GetProperty("digest").GetString());
            return item.GetProperty("id").GetString()!;
        }));
        // 包族锁步发布：同一组件的包钉同一版本。
        Assert.Single(packages.Select(item => item.GetProperty("version").GetString()).Distinct());
        Assert.Equal(["platform", "standalone"], Strings(json.GetProperty("policy").GetProperty("profiles")));
        Assert.Equal(["global", "tenant"], Strings(json.GetProperty("scopes")));
        Assert.False(json.TryGetProperty("x-skeleton", out _));

        var generated = json.GetProperty("x-qian-generated");
        Assert.True(generated.GetProperty("$generated").GetBoolean());
        Assert.Equal(["/inject", "/provides", "/runtime/form", "/runtime/entry"], Strings(generated.GetProperty("fields")));
        // 权限与角色声明仍为手写 metadata，没有代码派生的表面。
        Assert.Empty(generated.GetProperty("metadata").EnumerateObject());
        Assert.Equal(code.Capabilities.Select(contract => "tools/" + contract.Name + ".json"), Strings(generated.GetProperty("tools")));
        Assert.Equal(code.Capabilities.Select(contract => contract.Name + ".json"),
            Directory.EnumerateFiles(Path.Combine(Root, "plugins/qian.files/tools")).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var contract in code.Capabilities)
        {
            using var tool = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "plugins/qian.files/tools", contract.Name + ".json")));
            var file = tool.RootElement; var metadata = contract.Metadata;
            Assert.Equal(contract.Id, file.GetProperty("binding").GetProperty("id").GetString());
            foreach (var field in new[] { "sideEffect", "policyHint", "requiredPermission", "description" })
                Assert.Equal(metadata.GetProperty(field).GetString(), file.GetProperty(field).GetString());
            Assert.Equal((metadata.GetProperty("idempotent").GetBoolean(), metadata.GetProperty("audit").GetBoolean()),
                (file.GetProperty("idempotent").GetBoolean(), file.GetProperty("audit").GetBoolean()));
            Assert.True(JsonElement.DeepEquals(metadata.GetProperty("agent"), file.GetProperty("agent")), contract.Name);
            Assert.StartsWith("qian.files:file:", file.GetProperty("requiredPermission").GetString(), StringComparison.Ordinal);
            Assert.False(file.GetProperty("inputSchema").GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void 权限与角色声明编译为目录_三个权限点_owner与admin全权_developer与user只读写()
    {
        var catalog = FilesMetadata.Catalog();
        Assert.Equal(("qian.files", "1.0.0"), (catalog.PluginId, catalog.Version));
        Assert.Equal([(FilesPermissions.Delete, PermissionRisk.High), (FilesPermissions.Read, PermissionRisk.Low), (FilesPermissions.Write, PermissionRisk.Medium)],
            catalog.Permissions.Select(permission => (permission.Code, permission.Risk)).OrderBy(item => item.Code, StringComparer.Ordinal));
        var roles = catalog.Roles.ToDictionary(role => role.Role, role => role.Grants.Order(StringComparer.Ordinal).ToArray());
        string[] all = [FilesPermissions.Delete, FilesPermissions.Read, FilesPermissions.Write];
        string[] readWrite = [FilesPermissions.Read, FilesPermissions.Write];
        Assert.Equal(all, roles["tenant.owner"]);
        Assert.Equal(all, roles["tenant.admin"]);
        Assert.Equal(readWrite, roles["tenant.developer"]);
        Assert.Equal(readWrite, roles["tenant.user"]);
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();
}
