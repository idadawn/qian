using System.Text.Json;
using System.Text.RegularExpressions;

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

    [Fact]
    public async Task 动态键扫描在并发与反复调用时结果稳定_复用同一编译实例()
    {
        // Apollo 每份快照都要扫描；同一模式在多线程下共享编译实例，并发建图与热匹配都不能误报 Schema 无效或漏掉敏感键。
        using var schema = JsonDocument.Parse("""
            {"properties":{"Endpoints":{"patternProperties":{"^[a-z][a-z0-9-]*$":{"properties":{"Url":{"type":"string"}}},"^legacy_":{"x-qian-sensitive":true}}}}}
            """);
        var root = schema.RootElement;
        await Task.WhenAll(Enumerable.Range(0, 32).Select(worker => Task.Run(() =>
        {
            for (var round = 0; round < 40; round++)
            {
                var result = SecretSafety.ScanConfiguration(new Dictionary<string, string?>
                {
                    [$"Endpoints:svc-{worker}-{round}:Url"] = "https://orders.example.test/api",
                    [$"Endpoints:legacy_{worker}_{round}"] = "plain",
                }, root);
                var diagnostic = Assert.Single(result);
                Assert.Equal(("CONFIG_SECRET_EXPOSED", $"#/Endpoints:legacy_{worker}_{round}"), (diagnostic.Code, diagnostic.Location));
            }
        }, TestContext.Current.CancellationToken)));
        Assert.Same(SecretSafety.Pattern("^legacy_"), SecretSafety.Pattern("^legacy_"));
    }

    [Fact]
    public void 动态键模式保留线性引擎且超时有界_非法模式每次扫描都报告_缓存有上限()
    {
        var compiled = SecretSafety.Pattern("^legacy_");
        Assert.Same(compiled, SecretSafety.Pattern("^legacy_"));
        Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, compiled.Options);
        Assert.Equal(TimeSpan.FromSeconds(1), compiled.MatchTimeout);
        // 语法错误、环视、反向引用、原子组、条件、\G 与超限自动机；第二次扫描仍报告，失败不会留下可用实例。
        foreach (var pattern in new[] { "(", "a{2,1}", "(?=x)", "(?<=x)y", "(a)\\1", "(?>a)", "(?(a)b|c)", "\\Gx", "a{20000}" })
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var schema = JsonSerializer.SerializeToElement(new { patternProperties = new Dictionary<string, object> { [pattern] = new { type = "string" } } });
                using var config = JsonDocument.Parse("""{"key":"value"}""");
                Assert.Contains(SecretSafety.Scan(config.RootElement, schema), item => item.Code == "CONFIG_SECRET_SCHEMA_INVALID" && item.Reason == "unsupported-pattern");
            }
        for (var index = 0; index <= SecretSafety.PatternCacheSize; index++) _ = SecretSafety.Pattern($"^bounded_{index}$");
        Assert.InRange(SecretSafety.CachedPatterns, 1, SecretSafety.PatternCacheSize);
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
