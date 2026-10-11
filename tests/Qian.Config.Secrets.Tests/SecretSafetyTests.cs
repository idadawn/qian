using System.Text.Json;

namespace Qian.Config.Secrets.Tests;

public sealed class SecretSafetyTests
{
    [Theory]
    [InlineData("password", "synthetic-password")]
    [InlineData("client_secret", "synthetic-secret")]
    [InlineData("QIAN_TOKEN", "synthetic-token")]
    [InlineData("credential", "eyJmb28iOiJiYXIifQ.c3ludGhldGlj.c2lnbmF0dXJl")]
    [InlineData("endpoint", "https://user:synthetic@example.test")]
    [InlineData("endpoint", "https://example.test?api_key=synthetic")]
    [InlineData("endpoint", "https://example.test?x=aZ8pQ1mB9xR2vC7nH5jD3kL6wS4tF0uE")]
    [InlineData("connection", "Host=example;Password=synthetic")]
    [InlineData("opaque", "aZ8pQ1mB9xR2vC7nH5jD3kL6wS4tF0uE")]
    [InlineData("opaque", "-----BEGIN PRIVATE KEY----- synthetic")]
    [InlineData("nested", "{\"password\":\"synthetic-secret\"}")]
    public void 非引用敏感键和疑似凭据被拒绝_诊断不含原值(string key, string value)
    {
        var result = SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = value });
        Assert.NotEmpty(result);
        Assert.DoesNotContain(value, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("password", "secretRef://platform/db/password")]
    [InlineData("Authorization", "Bearer secretRef://tenant/service/token")]
    [InlineData("endpoint", "https://a-reasonably-long-service-name.example.test/api")]
    [InlineData("digest", "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("PageSize", "25")]
    [InlineData("nested", "{\"PageSize\":25,\"Mode\":\"normal\"}")]
    public void 合法引用和普通配置通过(string key, string value) =>
        Assert.Empty(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = value }));

    [Fact]
    public void Schema引用_组合_数组_扁平键_敏感父级都不能绕过()
    {
        using var schema = JsonDocument.Parse("""
        {"$defs":{"credential":{"type":"string","x-qian-sensitive":true}},
         "properties":{"Service":{"properties":{"credential":{"allOf":[{"$ref":"#/$defs/credential"}]}}},
                       "list":{"items":{"$ref":"#/$defs/credential"}},
                       "marked":{"x-qian-sensitive":true}}}
        """);
        using var plain = JsonDocument.Parse("""{"Service":{"credential":"plain"},"list":["plain"],"marked":{"child":"plain"}}""");
        Assert.Equal(3, SecretSafety.Scan(plain.RootElement, schema.RootElement).Count);
        var flattened = new Dictionary<string, string?> { ["Service:credential"] = "plain", ["list:0"] = "plain", ["marked:child"] = "plain" };
        Assert.Equal(3, SecretSafety.ScanConfiguration(flattened, schema.RootElement).Count);
        using var references = JsonDocument.Parse("""{"Service":{"credential":"secretRef://platform/service/credential"},"list":["secretRef://dev/app/token"],"marked":"secretRef://platform/whole/secret"}""");
        Assert.Empty(SecretSafety.Scan(references.RootElement, schema.RootElement));
    }

    [Fact]
    public void 外部Schema引用不可静默跳过()
    {
        using var schema = JsonDocument.Parse("""{"properties":{"value":{"$ref":"https://untrusted.example/schema"}}}""");
        using var config = JsonDocument.Parse("""{"value":"plain"}""");
        Assert.Contains(SecretSafety.Scan(config.RootElement, schema.RootElement), item => item.Code == "CONFIG_SECRET_SCHEMA_INVALID");
    }
    [Theory]
    [InlineData("{\"password\":123}")]
    [InlineData("{\"password\":null}")]
    [InlineData("{\"password\":{\"hidden\":\"plain\"}}")]
    public void 非字符串也不能绕过敏感键(string json)
    {
        using var input = JsonDocument.Parse(json);
        Assert.NotEmpty(SecretSafety.Scan(input.RootElement));
    }
}
