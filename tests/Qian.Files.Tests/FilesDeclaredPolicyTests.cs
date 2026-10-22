using System.Text.Json;
using Json.Schema;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Files.Tests;

// 组件 metadata/upload-policies.json 的汇总规则（S1-FILE-004）：只能声明本组件的用途、同组件取最高版本、已登记的策略优先。
public sealed class FilesDeclaredPolicyTests
{
    private static readonly string[] Pdf = ["application/pdf"];

    [Fact]
    public void 按组件汇总声明_同组件多版本取最高版本_无声明的组件跳过()
    {
        var policies = DeclaredUploadPolicies.Compile([
            InstalledSnapshot.Component("acme.docs", "1.9.0", new[] { Policy("acme.docs/old") }),
            InstalledSnapshot.Component("acme.docs", "1.10.0", new[] { Policy("acme.docs/contract"), Policy("acme.docs/scan") }),
            // 预发布版本低于同号正式版：1.10.0-beta.1 < 1.10.0。
            InstalledSnapshot.Component("acme.docs", "1.10.0-beta.1", new[] { Policy("acme.docs/beta") }),
            InstalledSnapshot.Component("acme.crm", "2.0.0", new[] { Policy("acme.crm/invoice") with { MaxCount = 3 } }),
            InstalledSnapshot.Component("acme.plain", "1.0.0", null)]);
        Assert.Equal(["acme.crm/invoice", "acme.docs/contract", "acme.docs/scan"], policies.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(3, policies["acme.crm/invoice"].MaxCount);
        Assert.Equal(Pdf, policies["acme.docs/scan"].AllowedTypes);
    }

    [Theory]
    // 不是数组；冒名声明别的组件的用途（会写进它的存储键前缀）；同文件重复；未知字段；类型、大小、数量不合法；元素不是对象。
    [InlineData("""{"purpose":"acme.docs/a","allowedTypes":["application/pdf"],"maxSize":1}""", "#", true)]
    [InlineData("""[{"purpose":"acme.crm/a","allowedTypes":["application/pdf"],"maxSize":1}]""", "#/0/purpose", false)]
    [InlineData("""[{"purpose":"acme.docs/a","allowedTypes":["application/pdf"],"maxSize":1},{"purpose":"acme.docs/a","allowedTypes":["text/plain"],"maxSize":2}]""", "#/1/purpose", false)]
    [InlineData("""[{"purpose":"acme.docs/a","allowedTypes":["application/pdf"],"maxSize":1,"disposition":"inline"}]""", "#/0", true)]
    [InlineData("""[{"purpose":"acme.docs/a","allowedTypes":[],"maxSize":1}]""", "#/0/allowedTypes", true)]
    [InlineData("""[{"purpose":"acme.docs/a","allowedTypes":["application/pdf"],"maxSize":0}]""", "#/0/maxSize", true)]
    [InlineData("""[{"purpose":"acme.docs/a","allowedTypes":["application/pdf"],"maxSize":1,"maxCount":0}]""", "#/0/maxCount", true)]
    [InlineData("""[{"purpose":"acme.docs","allowedTypes":["application/pdf"],"maxSize":1}]""", "#/0/purpose", true)]
    [InlineData("""[1]""", "#/0", true)]
    public void 声明不合法时报INPUT_INVALID并指向声明组件的元数据_结构错误Schema同样拒绝(string document, string location, bool structural)
    {
        using var json = JsonDocument.Parse(document);
        var error = Assert.Throws<QianErrorException>(() => DeclaredUploadPolicies.Compile([Docs(json.RootElement)])).Error;
        Assert.Equal((PlatformErrors.InputInvalid.Code, "acme.docs/metadata/upload-policies.json" + location), (error.Code.Code, error.Pointer));
        // 用途归属与同文件不重复是 Schema 表达不了的语义：Schema 只拦结构错误。
        Assert.Equal(!structural, Schema().Evaluate(json.RootElement).IsValid);
    }

    [Fact]
    public void 合法声明通过Schema_超过256条时两端都拒绝()
    {
        var valid = JsonSerializer.SerializeToElement(new[] { new { purpose = "acme.docs/a", allowedTypes = Pdf, maxSize = 1L, maxCount = 2 } });
        Assert.True(Schema().Evaluate(valid).IsValid);
        Assert.Single(DeclaredUploadPolicies.Compile([Docs(valid)]));
        var many = JsonSerializer.SerializeToElement(Enumerable.Range(0, DeclaredUploadPolicies.MaxPolicies + 1)
            .Select(index => new { purpose = "acme.docs/p" + index, allowedTypes = Pdf, maxSize = 1L }));
        Assert.False(Schema().Evaluate(many).IsValid);
        Assert.Equal("acme.docs/metadata/upload-policies.json#", Assert.Throws<QianErrorException>(() => DeclaredUploadPolicies.Compile([Docs(many)])).Error.Pointer);
    }

    [Fact]
    public void 已登记的策略优先于组件声明_撤销后回落到声明_清单合并两者()
    {
        var snapshot = new InstalledSnapshot([InstalledSnapshot.Component("acme.crm", "1.0.0",
            new[] { Policy("acme.crm/attachment") with { MaxSize = 1024 }, Policy("acme.crm/logo") })]);
        var registry = new UploadPolicyRegistry(new DeclaredUploadPolicies(() => snapshot));
        var registration = registry.Register(Policy("acme.crm/attachment") with { MaxSize = 2048 });
        Assert.Equal(2048, registry.Find("acme.crm/attachment")!.MaxSize);
        Assert.Equal(Pdf, registry.Find("acme.crm/logo")!.AllowedTypes);
        Assert.Equal([("acme.crm/attachment", 2048L), ("acme.crm/logo", 4096L)], registry.List().Select(item => (item.Purpose, item.MaxSize)));
        registration.Dispose();
        Assert.Equal(1024, registry.Find("acme.crm/attachment")!.MaxSize);
        Assert.Null(registry.Find("acme.crm/unknown"));
        // 预装快照不可变：多次查找只读一次。
        Assert.Equal(1, snapshot.Reads);
    }

    [Fact]
    public void Registry缺席或未就绪时声明为空且不缓存_就绪后编译一次()
    {
        InstalledSnapshot? snapshot = null; var ready = false;
        var declared = new DeclaredUploadPolicies(() => ready ? snapshot : throw new ServiceNotReadyException());
        Assert.Empty(declared.Policies);
        ready = true;
        Assert.Empty(declared.Policies);
        snapshot = new InstalledSnapshot([InstalledSnapshot.Component("acme.crm", "1.0.0", new[] { Policy("acme.crm/logo") })]);
        Assert.Equal("acme.crm/logo", Assert.Single(declared.Policies).Key);
        Assert.Single(declared.Policies);
        Assert.Equal(1, snapshot.Reads);
    }

    private static UploadPolicy Policy(string purpose) => new(purpose, Pdf, 4096);

    private static InstalledComponentMetadata Docs(JsonElement document) => new("acme.docs", "1.0.0", JsonSerializer.SerializeToElement(new { id = "acme.docs" }),
        new Dictionary<string, JsonElement> { [DeclaredUploadPolicies.MetadataPath] = document.Clone() });

    private static JsonSchema Schema() => JsonSchema.FromText(
        File.ReadAllText(Path.Combine(FilesMetadata.RepositoryRoot(), "schemas", "v2", "files-upload-policies.schema.json")), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
}
