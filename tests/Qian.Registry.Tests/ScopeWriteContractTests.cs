using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Registry.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Registry.Tests;

public sealed class ScopeWriteContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions ContractJson = new() { WriteIndented = true };

    [Fact]
    public async Task 注册表写入契约由真实端点导出_含条件修订与权限_发布契约无漂移_移除PUT被准入拒绝()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        await using var app = builder.Build();
        app.UseQianErrors(); app.UseQianRegistryAliases();
        app.MapQianRegistry(_ => throw new InvalidOperationException("契约编译不会执行端点。"));
        await app.StartAsync(Ct);
        var contract = await app.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.registry", Ct);
        const string path = "/api/registry/tenants/{tenant}/patches/{scope}/{entry}";
        var operation = contract.GetProperty("paths").GetProperty(path).GetProperty("put");
        Assert.Equal(RegistryDefinition.ScopeWritePermission, operation.GetProperty("x-qian-permission").GetString());
        Assert.Equal(2, contract.GetProperty("paths").EnumerateObject().Count());
        var document = JsonNode.Parse(contract.GetRawText())!;
        var schema = document["paths"]![path]!["put"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
        if (schema["$ref"] is { } reference) schema = document["components"]!["schemas"]![reference.GetValue<string>().Split('/')[^1]]!;
        Assert.Equal(["patch", "revision"], schema["required"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Null(schema["properties"]!["tenant"]); Assert.Null(schema["properties"]!["tenant_id"]);
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var file = Path.Combine(root!.FullName, "plugins/qian.registry/contracts/http-v1.openapi.json");
        if (Environment.GetEnvironmentVariable("QIAN_UPDATE_REGISTRY_CONTRACT") == "1")
        { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, JsonSerializer.Serialize(contract, ContractJson) + "\n"); }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file)), contract), "真实端点与发布契约漂移。");
        var tool = Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH") ?? throw new InvalidOperationException("缺少真实 oasdiff 契约检查器。");
        var probe = new OasDiffOpenApiProbe(tool);
        await using var runtime = new PluginLoader(app.Services);
        var inner = new ContractMarker();
        var entry = new PluginEntry("registry", "qian.registry", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { }));
        await runtime.MountAsync([new PluginDefinition(entry, new OpenApiContractPlugin(inner, probe, "qian.registry.http/v1", contract, contract), [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("registry", ScopeKey.Global)); Assert.Equal(1, inner.Applied);
        await runtime.UnmountAsync("registry", ScopeKey.Global, Ct);
        document["paths"]![path]!.AsObject().Remove("put");
        await runtime.MountAsync([new PluginDefinition(entry, new OpenApiContractPlugin(inner, probe, "qian.registry.http/v1", contract, JsonSerializer.SerializeToElement(document)), [])], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("registry", ScopeKey.Global)); Assert.Equal(1, inner.Applied);
        Assert.Equal("PRECONDITION_FAILED", Assert.IsType<QianErrorException>(runtime.GetFailure("registry", ScopeKey.Global)).Error.Code.Code);
    }

    private sealed class ContractMarker : IQianPlugin
    {
        internal int Applied { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Applied++; return ValueTask.CompletedTask; }
    }
}
