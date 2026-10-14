using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Common;

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
        Assert.Same(SchemaPattern.Compile("^legacy_", foldCase: true), SchemaPattern.Compile("^legacy_", foldCase: true));
    }

    [Fact]
    public void 动态键模式保留线性引擎且超时有界_非法模式每次扫描都报告_缓存有上限()
    {
        var compiled = SchemaPattern.Compile("^legacy_", foldCase: true);
        Assert.Same(compiled, SchemaPattern.Compile("^legacy_", foldCase: true));
        Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, compiled.Options);
        Assert.Equal(TimeSpan.FromSeconds(1), compiled.MatchTimeout);
        // 语法错误、环视、反向引用、原子组、条件、\G、超限自动机，以及 .NET 接受但各方言含义不同的词边界、Unicode 属性与内联选项；第二次扫描仍报告，失败不会留下可用实例。
        foreach (var pattern in new[] { "(", "a{2,1}", "(?=x)", "(?<=x)y", "(a)\\1", "(?>a)", "(?(a)b|c)", "\\Gx", "a{20000}", "\\bx", "\\p{L}", "(?i)a" })
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var schema = JsonSerializer.SerializeToElement(new { patternProperties = new Dictionary<string, object> { [pattern] = new { type = "string" } } });
                using var config = JsonDocument.Parse("""{"key":"value"}""");
                Assert.Contains(SecretSafety.Scan(config.RootElement, schema), item => item.Code == "CONFIG_SECRET_SCHEMA_INVALID" && item.Reason == "unsupported-pattern");
            }
        for (var index = 0; index <= SchemaPattern.CacheSize; index++) _ = SchemaPattern.Compile($"^bounded_{index}$", foldCase: true);
        Assert.InRange(SchemaPattern.Cached, 1, SchemaPattern.CacheSize);
    }

    [Fact]
    public void 动态键按ECMA262判定_否定简写不再漏掉非ASCII键名()
    {
        // .NET 默认方言的 \W \D \S 认 Unicode，曾让这些键名漏过敏感标记；按 ECMA-262，\w \d \s 只认 ASCII。
        var cases = new (string Pattern, string Key)[]
        {
            (@"^\W+$", new string((char)0x00E9, 1)),
            (@"^\D+$", new string([(char)0x0661, (char)0x0662])),
            (@"^\S+$", new string(['a', (char)0x0085, 'b'])),
        };
        foreach (var (pattern, key) in cases)
        {
            var schema = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["patternProperties"] = new Dictionary<string, object> { [pattern] = new Dictionary<string, object> { ["x-qian-sensitive"] = true } } });
            var plain = JsonSerializer.SerializeToElement(new Dictionary<string, string> { [key] = "plain" });
            var diagnostic = Assert.Single(SecretSafety.Scan(plain, schema));
            Assert.Equal(("CONFIG_SECRET_EXPOSED", "sensitive-schema"), (diagnostic.Code, diagnostic.Reason));
            var reference = JsonSerializer.SerializeToElement(new Dictionary<string, string> { [key] = "secretRef://platform/service/credential" });
            Assert.Empty(SecretSafety.Scan(reference, schema));
        }
    }

    [Fact]
    public void 动态键与配置键一样不区分大小写_大小写变体同样报告()
    {
        using var schema = JsonDocument.Parse("""
            {"properties":{"Endpoints":{"patternProperties":{"^[a-z][a-z0-9-]*$":{"properties":{"Url":{"type":"string"}}},"^legacy_":{"x-qian-sensitive":true}}}}}
            """);
        foreach (var key in new[] { "Endpoints:legacy_billing", "Endpoints:Legacy_billing", "Endpoints:LEGACY_BILLING", "endpoints:Legacy_Billing" })
        {
            var diagnostic = Assert.Single(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = "plain" }, schema.RootElement));
            Assert.Equal(("CONFIG_SECRET_EXPOSED", "#/" + key, "sensitive-schema"), (diagnostic.Code, diagnostic.Location, diagnostic.Reason));
            Assert.Empty(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = "secretRef://platform/billing/legacy" }, schema.RootElement));
        }
        // 应用按任意大小写读取都拿到同一个值，JSON 形态的配置文件也一样判定。
        using var json = JsonDocument.Parse("""{"Endpoints":{"Legacy_billing":"plain","Svc-Orders":{"Url":"https://orders.example.test/api"}}}""");
        var found = Assert.Single(SecretSafety.Scan(json.RootElement, schema.RootElement));
        Assert.Equal(("CONFIG_SECRET_EXPOSED", "#/Endpoints/Legacy_billing"), (found.Code, found.Location));
    }

    [Fact]
    public void 方言判断不同的键名整体按敏感处理_合法引用仍可通过()
    {
        // 换行、只有 ECMA-262 算作空白的字符与代理项让各方言对同一模式判断不同，这类键名不做匹配，值只能是合法引用。
        using var schema = JsonDocument.Parse("""{"properties":{"Endpoints":{"patternProperties":{"^[a-z]+$":{"type":"string"}}}}}""");
        foreach (var segment in new[] { "a" + (char)0x00A0 + "b", "a\rb", "a" + (char)0x2028 + "b", "a" + (char)0xD83D + (char)0xDE00 })
        {
            var key = "Endpoints:" + segment;
            var diagnostic = Assert.Single(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = "plain" }, schema.RootElement));
            Assert.Equal(("CONFIG_SECRET_EXPOSED", "sensitive-schema"), (diagnostic.Code, diagnostic.Reason));
            Assert.Empty(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { [key] = "secretRef://platform/endpoints/value" }, schema.RootElement));
        }
        Assert.Empty(SecretSafety.ScanConfiguration(new Dictionary<string, string?> { ["Endpoints:orders"] = "plain" }, schema.RootElement));
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
