using System.Reflection;
using System.Text.Json;
using Qian.Plugin.Sdk;

namespace Qian.Identity.Client.Tests;

// S1-IDP-014：仓库里 qian.identity.client 唯一清单的代码字段由源生成器派生（CI 另跑 Qian.Plugin.Tool check）；
// x-aot 由 tools/Qian.Identity.Client.AotSmoke 的 NativeAOT 冒烟背书，gateway profile 据此接纳本组件。
public sealed class ClientManifestTests
{
    [Fact]
    public void 仓库清单的代码字段与生成元数据一致_入口解析到IdentityClientPlugin_声明AOT兼容()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.identity.client/.qian-plugin/plugin.json")));
        var root = manifest.RootElement;
        var factory = typeof(IdentityClientPlugin).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType;
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(factory)!).Create();
        var entry = root.GetProperty("runtime").GetProperty("entry").GetString()!;
        Assert.Equal(("qian.identity.client", "qian.identity.client"), (code.ComponentId, root.GetProperty("id").GetString()));
        Assert.Equal((code.RuntimeEntry, "module"), (entry, root.GetProperty("runtime").GetProperty("form").GetString()));
        Assert.Empty(code.Inject);
        Assert.Empty(root.GetProperty("inject").EnumerateArray());
        Assert.Equal(["Identity"], code.Provides);
        Assert.Equal(code.Provides, root.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        var generated = root.GetProperty("x-qian-generated");
        Assert.True(generated.GetProperty("$generated").GetBoolean());
        Assert.Equal(["/inject", "/provides", "/runtime/form", "/runtime/entry"], generated.GetProperty("fields").EnumerateArray().Select(item => item.GetString()!));
        Assert.True(root.GetProperty("x-aot").GetBoolean());
        Assert.Same(typeof(IdentityClientPlugin), Type.GetType(entry, throwOnError: true));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}
