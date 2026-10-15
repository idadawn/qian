using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Audit.Tests;

public sealed class AuditInputPolicyTests
{
    private static JsonElement Json(string source) => JsonSerializer.Deserialize<JsonElement>(source);

    [Fact]
    public void 敏感字段先遮盖再哈希_原输入不变_改变秘密不会改变哈希()
    {
        var schema = Json("""
            {"type":"object","properties":{
              "name":{"type":"string"},"password":{"type":"string","sensitive":true},
              "profile":{"type":"object","x-qian-sensitive":true},
              "codes":{"type":"array","items":{"type":"string","sensitive":true}}},
              "required":["name","password"],"additionalProperties":true}
            """);
        var policy = new AuditInputPolicy(schema, isCommand: true);
        var input = Json("""{"name":"Ada","password":"secret-one","profile":{"pin":"001"},"codes":["otp1","otp2"],"unexpected":"do-not-hash-me"}""");
        var safe = policy.Redact(input);
        Assert.Equal("Ada", safe.GetProperty("name").GetString());
        foreach (var field in new[] { "password", "profile", "unexpected" }) Assert.Equal(AuditInputPolicy.Redacted, safe.GetProperty(field).GetString());
        Assert.All(safe.GetProperty("codes").EnumerateArray(), item => Assert.Equal(AuditInputPolicy.Redacted, item.GetString()));
        Assert.Equal("secret-one", input.GetProperty("password").GetString());
        var changed = Json(input.GetRawText().Replace("secret-one", "secret-two", StringComparison.Ordinal).Replace("001", "002", StringComparison.Ordinal));
        Assert.Equal(policy.InputHash(input), policy.InputHash(changed));
        Assert.NotEqual(policy.InputHash(input), policy.InputHash(Json(input.GetRawText().Replace("Ada", "Grace", StringComparison.Ordinal))));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(AuditCanonicalizer.Canonicalize(safe)))), policy.InputHash(input));
    }

    [Fact]
    public void 引用与组合条件中的敏感标记保守合并_关闭标记不能覆盖另一分支()
    {
        var policy = new AuditInputPolicy(Json("""
            {"$defs":{"s":{"type":"string","sensitive":true}},"type":"object",
             "properties":{"kind":{"type":"string"},"secret":{"allOf":[{"$ref":"#/$defs/s"},{"sensitive":false}]},"optional":{"type":"string"}},
             "if":{"properties":{"kind":{"const":"a"}}},"then":{"properties":{"optional":{"sensitive":true}}},
             "else":{"properties":{"optional":{"sensitive":false}}}}
            """), true);
        var safe = policy.Redact(Json("""{"kind":"b","secret":"secret","optional":"hidden-in-either-branch"}"""));
        Assert.Equal("b", safe.GetProperty("kind").GetString());
        Assert.Equal(AuditInputPolicy.Redacted, safe.GetProperty("secret").GetString());
        Assert.Equal(AuditInputPolicy.Redacted, safe.GetProperty("optional").GetString());
        var alternative = new AuditInputPolicy(Json("""{"anyOf":[{"type":"string"},{"type":"string","sensitive":true}]}"""), true);
        Assert.Equal(AuditInputPolicy.Redacted, alternative.Redact(Json("\"private\"")).GetString());
    }

    [Fact]
    public void 数组位置_动态键_额外字段_属性名与递归对象中的敏感标记均生效()
    {
        var tuple = new AuditInputPolicy(Json("""{"type":"array","prefixItems":[{"type":"integer"},{"type":"string","sensitive":true}],"items":{"type":"string","x-qian-sensitive":true}}"""), true);
        var result = tuple.Redact(Json("[1,\"private1\",\"private2\"]")); Assert.Equal(1, result[0].GetInt32()); Assert.Equal(AuditInputPolicy.Redacted, result[1].GetString()); Assert.Equal(AuditInputPolicy.Redacted, result[2].GetString());
        var map = new AuditInputPolicy(Json("""{"type":"object","patternProperties":{"^secret_":{"type":"string","sensitive":true}},"additionalProperties":{"type":"string"}}"""), true);
        Assert.Equal(AuditInputPolicy.Redacted, map.Redact(Json("""{"secret_x":"hidden","name":"visible"}""")).GetProperty("secret_x").GetString());
        Assert.Equal("visible", map.Redact(Json("""{"name":"visible"}""")).GetProperty("name").GetString());
        var names = new AuditInputPolicy(Json("""{"type":"object","propertyNames":{"sensitive":true}}"""), true);
        Assert.Equal(AuditInputPolicy.Redacted, names.Redact(Json("""{"private-key-name":"anything"}""")).GetString());
        var recursive = new AuditInputPolicy(Json("""{"type":"object","properties":{"secret":{"type":"string","sensitive":true},"child":{"$ref":"#"}}}"""), true);
        var tree = recursive.Redact(Json("""{"secret":"first","child":{"secret":"second"}}"""));
        Assert.Equal(AuditInputPolicy.Redacted, tree.GetProperty("child").GetProperty("secret").GetString());
    }

    [Fact]
    public async Task 动态键脱敏在并发与反复新建策略时结果稳定()
    {
        // 生产路径每条命令都新建策略；同一模式在多线程下共享编译实例，并发建图与热匹配都不能误判超时或串结果。
        const string source = """{"type":"object","patternProperties":{"^secret_":{"type":"string","sensitive":true},"^x-[a-z0-9]+$":{"type":"string"}},"additionalProperties":{"type":"integer"}}""";
        await Task.WhenAll(Enumerable.Range(0, 32).Select(worker => Task.Run(() =>
        {
            for (var round = 0; round < 40; round++)
            {
                var safe = new AuditInputPolicy(Json(source), true).Redact(Json($$"""{"secret_{{worker}}_{{round}}":"hidden","x-{{worker}}k{{round}}":"visible","n{{round}}":{{round}}}"""));
                Assert.Equal(AuditInputPolicy.Redacted, safe.GetProperty($"secret_{worker}_{round}").GetString());
                Assert.Equal("visible", safe.GetProperty($"x-{worker}k{round}").GetString());
                Assert.Equal(round, safe.GetProperty($"n{round}").GetInt32());
            }
        }, TestContext.Current.CancellationToken)));
        Assert.Same(AuditInputPolicy.Pattern("^secret_"), AuditInputPolicy.Pattern("^secret_"));
    }

    [Fact]
    public void 动态键模式复用线性引擎且超时有界_非法模式每次构造都拒绝_缓存有上限()
    {
        var compiled = AuditInputPolicy.Pattern("^secret_");
        Assert.Same(compiled, AuditInputPolicy.Pattern("^secret_"));
        Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, compiled.Options);
        Assert.Equal(TimeSpan.FromSeconds(1), compiled.MatchTimeout);
        // 语法错误、环视、反向引用、原子组、条件、\G 与超限自动机；第二次构造仍拒绝，失败不会留下可用实例。
        foreach (var pattern in new[] { "(", "a{2,1}", "(?=x)", "(?<=x)y", "(a)\\1", "(?>a)", "(?(a)b|c)", "\\Gx", "a{20000}" })
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var schema = JsonSerializer.SerializeToElement(new { patternProperties = new Dictionary<string, object> { [pattern] = new { sensitive = true } } });
                Assert.Equal("AUDIT_SCHEMA_INVALID", Assert.Throws<QianErrorException>(() => new AuditInputPolicy(schema, true)).Error.Code.Code);
            }
        for (var index = 0; index <= AuditInputPolicy.PatternCacheSize; index++) _ = AuditInputPolicy.Pattern($"^bounded_{index}$");
        Assert.InRange(AuditInputPolicy.CachedPatterns, 1, AuditInputPolicy.PatternCacheSize);
    }

    [Fact]
    public void 外部引用_同层引用循环_不支持语义_重复定义与错误标记拒绝且错误不含原值()
    {
        string[] schemas = [
            """{"$ref":"https://never-fetch.invalid/schema"}""", """{"$ref":"#"}""",
            """{"$defs":{"x":{"$ref":"#/$defs/x"}},"$ref":"#/$defs/x"}""",
            """{"$dynamicRef":"#x"}""", """{"sensitive":"private-marker"}""",
            """{"properties":{"x":{"type":"string"},"x":{"sensitive":true}}}""",
            """{"$ref":"#/$defs/missing"}""", """{"properties":{"x":{"$id":"other.json"}}}""",
            """{"patternProperties":{"(?=x)":{"sensitive":true}}}""",
            """{"$ref":"#/examples/0","examples":[{"$dynamicRef":"#x"}]}"""
        ];
        foreach (var schema in schemas)
        {
            var error = Assert.Throws<QianErrorException>(() => new AuditInputPolicy(Json(schema), true));
            Assert.Equal("AUDIT_SCHEMA_INVALID", error.Error.Code.Code); Assert.DoesNotContain("private-marker", error.Message);
        }
        var catalog = ErrorCatalog.CreateDefault(); using var registration = AuditErrors.Register(catalog);
        Assert.Equal(400, catalog.CreateDetail(new QianError(AuditErrors.SchemaInvalid), new string('1', 32)).Status);
    }

    [Fact]
    public void 输入不符合契约或敏感子树有重复字段时也拒绝()
    {
        var policy = new AuditInputPolicy(Json("""{"type":"object","required":["password"],"properties":{"password":{"type":"string","sensitive":true}},"additionalProperties":false}"""), true);
        foreach (var source in new[] { "{}", "{\"password\":12}", "{\"password\":\"secret-a\",\"extra\":\"secret-b\"}" })
        {
            var error = Assert.Throws<QianErrorException>(() => policy.InputHash(Json(source)));
            Assert.Equal("INPUT_INVALID", error.Error.Code.Code); Assert.DoesNotContain("secret", error.Message);
        }
        var sensitiveRoot = new AuditInputPolicy(Json("""{"sensitive":true}"""), true);
        Assert.Equal("INPUT_INVALID", Assert.Throws<QianErrorException>(() => sensitiveRoot.InputHash(Json("{\"x\":1,\"x\":2}"))).Error.Code.Code);
    }

    [Fact]
    public async Task 策略复制契约_原文档释放后仍可并发计算且不串输入()
    {
        AuditInputPolicy policy;
        using (var document = JsonDocument.Parse("""{"type":"object","properties":{"secret":{"type":"string","sensitive":true},"name":{"type":"string"}}}"""))
            policy = new AuditInputPolicy(document.RootElement, true);
        var hashes = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
            policy.InputHash(JsonSerializer.SerializeToElement(new { secret = "private-" + index, name = "same" })), TestContext.Current.CancellationToken)));
        Assert.Single(hashes.Distinct(StringComparer.Ordinal)); Assert.NotNull(hashes[0]);
    }

    [Fact]
    public void 命令默认记录_查询默认不记录_显式审计策略可覆盖且没有伪造执行器()
    {
        var schema = Json("{\"type\":\"string\"}"); var input = Json("\"input\"");
        Assert.NotNull(new AuditInputPolicy(schema, isCommand: true).InputHash(input));
        Assert.Null(new AuditInputPolicy(schema, isCommand: false).InputHash(input));
        Assert.NotNull(new AuditInputPolicy(schema, isCommand: false, audit: true).InputHash(input));
        Assert.Null(new AuditInputPolicy(schema, isCommand: true, audit: false).InputHash(input));
    }
}
