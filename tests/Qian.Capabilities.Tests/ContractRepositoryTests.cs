using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities.Tests;

public sealed class ContractRepositoryTests
{
    private const string Plugin = "test.contracts";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] NoKeys = [];
    private static readonly string[] Scopes = ["global"];

    [Fact]
    public async Task 从真实制品快照按组件版本读契约_比较完整基线_返回内容不可变()
    {
        var first = Artifact("1.0.0"); var second = Artifact("1.1.0", description: "兼容说明"); var breaking = Artifact("1.2.0", removePath: true);
        var probe = new OasDiffOpenApiProbe(Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH") ?? throw new InvalidOperationException("缺少真实检查器。"));
        using var repository = new InstalledContractRepository([first, second, breaking], probe, new SurfaceValidator());
        Assert.Equal(3, repository.GetContracts(Plugin, "1.0.0").Count);
        var document = repository.Find(Plugin, "1.0.0", Plugin + ".customer/v1"); Assert.NotNull(document);
        Assert.Equal(first.ArtifactDigest, document.ArtifactDigest);
        var before = document.ReadContent(); var copy = document.ReadContent(); Array.Fill(copy, (byte)0);
        Assert.Equal(before, document.ReadContent());
        Assert.Contains("syntax", Encoding.UTF8.GetString(repository.Find(Plugin, "1.0.0", Plugin + ".wire/v1")!.ReadContent()), StringComparison.Ordinal);
        Assert.True((await repository.CheckOpenApiAsync(Plugin, "1.0.0", "1.1.0", document.Id, Ct)).IsCompatible);
        Assert.False((await repository.CheckOpenApiAsync(Plugin, "1.0.0", "1.2.0", document.Id, Ct)).IsCompatible);
        Assert.Null(repository.Find(Plugin, "9.0.0", document.Id));
        var error = await Assert.ThrowsAsync<QianErrorException>(() => repository.CheckOpenApiAsync(Plugin, "1.0.0", "9.0.0", document.Id, Ct).AsTask());
        Assert.Equal("NOT_FOUND", error.Error.Code.Code);
        repository.Dispose();
        Assert.Throws<ObjectDisposedException>(() => repository.GetContracts(Plugin, "1.0.0"));
    }

    [Fact]
    public void 声明不能替代原始文件快照_越界缺失文件和非法Schema被拒绝()
    {
        foreach (var component in new[]
        {
            Artifact("1.0.0", captureFiles: false), Artifact("1.0.1", contractPath: "../outside.json"),
            Artifact("1.0.2", contractPath: "./contracts/missing.json"), Artifact("1.0.3", invalidSchema: true)
        })
            Assert.Throws<QianErrorException>(() => new InstalledContractRepository([component], null, new SurfaceValidator()));
    }

    private static InstalledComponentMetadata Artifact(string version, string description = "客户列表", bool removePath = false,
        string contractPath = "./contracts/customer-v1.openapi.json", bool captureFiles = true, bool invalidSchema = false)
    {
        var manifest = JsonSerializer.SerializeToElement(new
        {
            id = Plugin, version, description = "版本化契约仓库真实制品测试组件。", author = new { name = "Qian" }, license = "Apache-2.0",
            @interface = new { displayName = "契约", category = "testing" }, inject = NoKeys, provides = NoKeys,
            policy = new { tier = "private", sandbox = "none", authentication = "ON_INSTALL" }, scopes = Scopes
        });
        var openapi = JsonNode.Parse("""{"openapi":"3.1.1","info":{"title":"客户契约","version":"1.0.0"},"paths":{"/customers":{"get":{"operationId":"listCustomers","responses":{"200":{"description":"成功"}}}}}}""")!;
        if (removePath) openapi["paths"] = new JsonObject();
        else openapi["paths"]!["/customers"]!["get"]!["description"] = description;
        var declarations = JsonSerializer.Serialize(new[]
        {
            new { id = Plugin + ".customer/v1", kind = "openapi", path = contractPath },
            new { id = Plugin + ".wire/v1", kind = "proto", path = "./contracts/customer-v1.proto" },
            new { id = Plugin + ".item/v1", kind = "json-schema", path = "./schemas/customer-v1.schema.json" }
        });
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            [".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.GetRawText()),
            ["metadata/contracts.json"] = Encoding.UTF8.GetBytes(declarations),
            ["contracts/customer-v1.openapi.json"] = Encoding.UTF8.GetBytes(openapi.ToJsonString()),
            ["contracts/customer-v1.proto"] = Encoding.UTF8.GetBytes("syntax = \"proto3\"; package test.contracts.v1; message Customer { string id = 1; }"),
            ["schemas/customer-v1.schema.json"] = Encoding.UTF8.GetBytes(invalidSchema ? "{\"type\":42}" : "{\"type\":\"object\"}")
        };
        var directory = Directory.CreateTempSubdirectory("qian-contract-artifact-");
        try
        {
            foreach (var file in files)
            {
                var path = Path.Combine(directory.FullName, file.Key); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, file.Value.ToArray());
            }
            var validator = new SurfaceValidator(); var options = new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true };
            var validation = validator.ValidateComponent(directory.FullName, options);
            Assert.True(validation.IsValid, JsonSerializer.Serialize(validation.Diagnostics));
            var surface = new DeclarativeSurfacePlugin(validation);
            var definition = new PluginDefinition(new PluginEntry("contracts", Plugin, version, ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), surface, surface.Provides);
            if (!captureFiles) return new RegistryComponentBinding(definition, validation).Metadata;
            var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
            var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
            return RegistryComponentBinding.FromArtifact(definition, files, digest, validator, options).Metadata;
        }
        finally { directory.Delete(true); }
    }
}
