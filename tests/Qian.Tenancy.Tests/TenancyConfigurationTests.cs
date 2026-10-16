using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Data;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Tenancy.Tests;

public sealed partial class TenantDirectoryTests
{
    private static readonly string[] ConfigurationProfiles = ["identity", "standalone", "gateway"];
    [Fact]
    public async Task base组合与派生文档配置实际可挂载_同时保留resolver与初始化清单()
    {
        var root = RepositoryRoot();
        var profile = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = root, Profile = "platform", IncludeHomePatch = false });
        Assert.True(profile.IsValid, JsonSerializer.Serialize(profile.Diagnostics));
        var config = Assert.Single(profile.Snapshot!.Entries, entry => entry.Id == "tenancy").Config;
        var documentation = File.ReadAllText(Path.Combine(root, "docs/generated/config-catalog.md"));
        var tenancy = documentation[documentation.IndexOf("组件：` qian.tenancy@", StringComparison.Ordinal)..];
        var begin = tenancy.IndexOf("```json\n", StringComparison.Ordinal) + "```json\n".Length;
        var published = JsonSerializer.Deserialize<JsonElement>(tenancy[begin..tenancy.IndexOf("\n```", begin, StringComparison.Ordinal)]);
        Assert.True(JsonElement.DeepEquals(config, published));
        var configured = JsonNode.Parse(config.GetRawText())!;
        configured["initializationCapabilities"] = new JsonArray("qian.sample.crm/customer.list");
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host);
        await using var loader = new PluginLoader(host.Services);
        await loader.MountAsync([Definition("data", new DataPlugin(), [DataDefinition.Key])], Ct);
        var otherProfiles = ConfigurationProfiles.Select(name =>
            new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = root, Profile = name, IncludeHomePatch = false }))
            .SelectMany(result => { Assert.True(result.IsValid); return result.Snapshot!.Entries.Where(entry => entry.Id == "tenancy").Select(entry => entry.Config); });
        foreach (var value in new[] { config, published, JsonSerializer.SerializeToElement(configured) }.Concat(otherProfiles))
        {
            var definition = Definition("tenancy", new TenancyPlugin(), [TenancyDefinition.Key]);
            await loader.MountAsync([definition with { Entry = definition.Entry with { Config = value } }], Ct);
            Assert.True(loader.GetState("tenancy", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("tenancy", ScopeKey.Global)?.ToString());
            Assert.NotNull(loader.GetContext("tenancy", ScopeKey.Global).Inject<ITenantProvisioning>(TenantProvisioning.Key));
            await loader.UnmountAsync("tenancy", ScopeKey.Global, Ct);
        }
        Assert.Contains("qian.sample.crm/customer.list", TenancyPlugin.ReadConfiguration(JsonSerializer.SerializeToElement(configured)));
        var manifest = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root, "plugins/qian.tenancy/.qian-plugin/plugin.json")));
        Assert.Equal("global", Assert.Single(manifest.GetProperty("scopes").EnumerateArray()).GetString());
        var tenantDefinition = Definition("tenancy", new TenancyPlugin(), [TenancyDefinition.Key]);
        var scope = new ScopeKey("tenant", Guid.NewGuid().ToString());
        await loader.MountAsync([tenantDefinition with { Entry = tenantDefinition.Entry with { Scope = scope } }], Ct);
        Assert.Equal("INPUT_INVALID", Assert.IsType<QianErrorException>(loader.GetFailure("tenancy", scope)).Error.Code.Code);
    }

    [Theory]
    [InlineData("{\"initializationCapabilities\":[\"qian.test/*\"]}")]
    [InlineData("{\"initializationCapabilities\":[\"qian.test\"]}")]
    [InlineData("{\"initializationCapabilities\":[\"qian.test/init\",\"qian.test/init\"]}")]
    [InlineData("{\"initializationCapabilities\":\"qian.test/init\"}")]
    [InlineData("{\"initializationCapabilities\":[1]}")]
    [InlineData("{\"initializationCapabilities\":[\"qian.test/init/extra\"]}")]
    [InlineData("{\"initializationCapabilities\":[\"qian.test/init\\n\"]}")]
    [InlineData("{\"resolver\":{\"mode\":\"development\"}}")]
    [InlineData("{\"resolver\":{\"allowHeaderFallback\":true}}")]
    [InlineData("{\"resolver\":{\"mode\":\"production\",\"mode\":\"production\"}}")]
    [InlineData("{\"resolver\":null}")]
    [InlineData("{\"unknown\":true}")]
    [InlineData("{\"initializationCapabilities\":[],\"initializationCapabilities\":[]}")]
    public async Task 非法部署配置在实际Apply拒绝(string json)
    {
        using var host = Host(); await host.StartAsync(Ct);
        await using var loader = new PluginLoader(host.Services);
        var definition = Definition("tenancy", new TenancyPlugin(), [TenancyDefinition.Key]);
        await loader.MountAsync([Definition("data", new DataPlugin(), [DataDefinition.Key]),
            definition with { Entry = definition.Entry with { Config = JsonSerializer.Deserialize<JsonElement>(json) } }], Ct);
        Assert.Equal(PluginState.Failed, loader.GetState("tenancy", ScopeKey.Global));
        Assert.Equal("INPUT_INVALID", Assert.IsType<QianErrorException>(loader.GetFailure("tenancy", ScopeKey.Global)).Error.Code.Code);
    }

    [Fact]
    public void 租户九个服务键符合单段语法且不重名()
    {
        ServiceKey[] keys = [TenantProvisioning.Key, TenantProvisioning.MembershipKey, TenantProvisioning.NotificationKey, TenantOperations.Key,
            TenantLifecycle.Key, TenantSettings.Key, TenantSettings.OperationsKey, TenantSettings.QuotaKey, TenantSettings.UserCounterKey];
        Assert.Equal(9, keys.Select(key => key.Name).Distinct().Count());
        Assert.All(keys, key => Assert.Matches("^Tenancy[A-Za-z]+$", key.Name));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("仓库根目录不存在。");
    }
}
