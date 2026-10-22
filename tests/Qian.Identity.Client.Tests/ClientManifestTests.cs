using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Identity.Client.Tests;

public sealed class ClientManifestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 唯一清单从稳定入口派生_公开客户端实际挂载与卸载不需要Config或DataProvider()
    {
        var metadata = new IdentityClientComponentQianCodeMetadata().Create();
        var root = RepositoryRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins/qian.identity.client/.qian-plugin/plugin.json")));
        Assert.Equal("qian.identity.client", metadata.ComponentId);
        Assert.Empty(metadata.Inject);
        Assert.Equal(IdentityDefinition.Key.Name, Assert.Single(metadata.Provides));
        Assert.Equal(metadata.RuntimeEntry, manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString());
        Assert.Equal("global", Assert.Single(manifest.RootElement.GetProperty("scopes").EnumerateArray()).GetString());
        Assert.True(manifest.RootElement.GetProperty("x-skeleton").GetBoolean());
        // x-aot 以 tools/Qian.Identity.Client.AotSmoke 的 CI NativeAOT 冒烟为据（S1-IDP-014）。
        Assert.True(manifest.RootElement.GetProperty("x-aot").GetBoolean());
        var schemaPath = manifest.RootElement.GetProperty("runtime").GetProperty("configSchema").GetString()![2..];
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "plugins/qian.identity.client", schemaPath)));
        Assert.True(JsonElement.DeepEquals(metadata.ConfigSchema!.Value, schema.RootElement));
        Assert.Empty(metadata.Capabilities);

        await using var services = new ServiceCollection().AddLogging().AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = new Uri("https://id.qian.test/"), ClientId = "qian-public", UseInMemorySessionStore = true
        }).BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var entryType = Type.GetType(metadata.RuntimeEntry, throwOnError: true)!;
        var plugin = Assert.IsAssignableFrom<IQianPlugin>(Activator.CreateInstance(entryType));
        Assert.Empty(plugin.Inject);
        await loader.MountAsync([new(new PluginEntry("identity", metadata.ComponentId, "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { issuer = "https://id.qian.test/", clientId = "qian-public" })), plugin,
            metadata.Provides.Select(name => new ServiceKey(name)).ToArray())], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("identity", ScopeKey.Global));
        Assert.NotNull(loader.GetContext("identity", ScopeKey.Global).Inject<IIdentityService>(IdentityDefinition.Key));
        Assert.True(services.GetRequiredService<IdentityClientRuntime>().Active);
        await loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        Assert.False(services.GetRequiredService<IdentityClientRuntime>().Active);
    }

    [Theory]
    [InlineData("secretRef://platform/identity/client-secret", true)]
    [InlineData("secretRef://dev/identity/password", true)]
    [InlineData("secretRef://platform/identity/client secret", false)]
    [InlineData("secretRef://tenant/identity/client-secret", false)]
    [InlineData("plaintext", false)]
    public void 代码配置Schema保留secretRef边界_合法路径的字母s不被误当作空白(string reference, bool valid)
    {
        var metadata = new IdentityClientComponentQianCodeMetadata().Create();
        var schema = JsonSchema.FromText(metadata.ConfigSchema!.Value.GetRawText());
        var configuration = JsonSerializer.SerializeToElement(new { issuer = "https://id.qian.test/", clientId = "qian-web", clientSecret = reference });
        Assert.Equal(valid, schema.Evaluate(configuration).IsValid);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}
