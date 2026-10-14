using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Capabilities.Tests;

// S1-RT-020 第 1 项：输入 Schema 在登记阶段预编译；不合格的契约带组件、能力短名与 JSON Pointer 拒绝，不留到首次调用。
public sealed class CapabilityContractCheckTests
{
    [Fact]
    public void 预编译拒绝不受支持的键_找不到目标的引用与无法编译的Schema_给出Schema内位置_通过的进入缓存()
    {
        var validator = new CapabilityInputValidator();
        var cases = new (string Schema, string[] Path, string Rule)[]
        {
            ("""{"type":"object","properties":{"p":{"type":"object","patternProperties":{"^a/b$":{"type":"string"}}}}}""", ["properties", "p", "patternProperties", "^a/b$"], "unsupported-key"),
            ("""{"type":"object","dependentSchemas":{"a~b":{"required":["c"]}}}""", ["dependentSchemas", "a~b"], "unsupported-key"),
            ("""{"type":"object","properties":{"x":{"$ref":"#/$defs/missing"}}}""", ["properties", "x", "$ref"], "unresolved-reference"),
            ("""{"type":"object","properties":{"x":{"$ref":"other.json"}}}""", ["properties", "x", "$ref"], "unresolved-reference"),
            ("""{"type":"object","properties":{"x":{"type":"array","items":{"$ref":"#nope"}}}}""", ["properties", "x", "items", "$ref"], "unresolved-reference"),
            ("""{"type":"object","anyOf":[{},{"$dynamicRef":"#meta"}]}""", ["anyOf", "1", "$dynamicRef"], "unresolved-reference"),
            // 内嵌资源里的引用相对其 $id 解析：#/$defs/top 在 inner.json 内不存在，即使根文档里有同名定义。
            ("""{"$defs":{"top":{},"inner":{"$id":"inner.json","properties":{"q":{"$ref":"#/$defs/top"}}}},"properties":{"i":{"$ref":"inner.json"}}}""",
                ["$defs", "inner", "properties", "q", "$ref"], "unresolved-reference"),
            ("""{"type":5}""", [], "schema-invalid"),
            ("""{"type":"object","properties":{"code":{"type":"string","pattern":"("}}}""", [], "schema-invalid"),
        };
        foreach (var (schema, path, rule) in cases)
        {
            var entry = Entry(schema);
            var key = entry.GetProperty("schemaSha256").GetString()!;
            var error = Assert.Throws<CapabilityInputSchemaException>(() => validator.Prepare(key, entry.GetProperty("inputSchema")));
            Assert.Equal(path, error.Path); Assert.Equal(rule, error.Rule);
            Assert.False(validator.IsPrepared(key));
            // 未经登记期检查直接执行时同样拒绝，不进入求值。
            Assert.Equal(rule, Assert.Throws<CapabilityInputSchemaException>(() => validator.Validate(entry, Json("{}"))).Rule);
        }

        // 内嵌 $id、锚点引用，以及键含 / 的 dependentRequired 与 properties 都能可靠处理：通过后进入缓存，执行时不再编译。
        var valid = Entry("""
            {"type":"object","$defs":{"line":{"$anchor":"line","type":"object"},"inner":{"$id":"inner.json","$defs":{"x":{"type":"string"}},"properties":{"q":{"$ref":"#/$defs/x"}}}},
             "properties":{"a":{"$ref":"#line"},"b":{"$ref":"inner.json"},"x/y":{"type":"integer"}},"dependentRequired":{"a/b":["c"]}}
            """);
        var validKey = valid.GetProperty("schemaSha256").GetString()!;
        Assert.False(validator.IsPrepared(validKey));
        validator.Prepare(validKey, valid.GetProperty("inputSchema"));
        Assert.True(validator.IsPrepared(validKey));
        validator.Validate(valid, Json("""{"a":{},"b":{"q":"s"},"x/y":1}"""));
        Assert.Equal("TYPE_MISMATCH", Assert.Throws<QianErrorException>(() => validator.Validate(valid, Json("""{"b":{"q":1}}"""))).Error.Errors![0].Code.Code);
    }

    [Fact]
    public void 登记期检查拒绝租户字段_异常带组件_能力_条目内位置与原因码_同一契约只生成一次并只告警一次()
    {
        var logger = new RecordingLogger();
        var check = new CapabilityContractCheck(new SurfaceValidator(), new ToolContractHasher(), logger);
        var contract = Contract("probe.tenant", ProbeJson.Default.TenantProbe);
        var error = Assert.Throws<CapabilityContractException>(() => check.Check(contract));
        Assert.Equal(("qian.test", "probe.tenant", "qian.test/probe.tenant#/inputSchema/properties/tenantId", "tenant-field", "INPUT_INVALID"),
            (error.ComponentId, error.Name, error.Location, error.Rule, error.ReasonCode));
        Assert.Equal(["capability", "componentId", "location", "reasonCode", "rule"], error.Data.Keys.Cast<string>().Order(StringComparer.Ordinal));
        Assert.Equal("qian.test/probe.tenant", error.Data["capability"]); Assert.Equal("INPUT_INVALID", error.Data["reasonCode"]);
        foreach (var part in new[] { "qian.test", "probe.tenant", "tenant-field", "qian.test/probe.tenant#/inputSchema/properties/tenantId" })
            Assert.Contains(part, error.Message, StringComparison.Ordinal);
        // 结果按契约实例缓存：再次登记同样拒绝，目录与执行入口读到 null；告警只记一次。
        Assert.NotSame(error, Assert.Throws<CapabilityContractException>(() => check.Check(contract)));
        Assert.Null(check.Entry(contract));
        Assert.Contains("qian.test/probe.tenant", Assert.Single(logger.Messages), StringComparison.Ordinal);

        // 嵌套对象里的租户字段同样拒绝，位置指到该字段；工具声明不合格时位置指到声明里的字段。
        Assert.Equal("qian.test/probe.nested#/inputSchema/properties/filter/properties/tenant",
            Assert.Throws<CapabilityContractException>(() => check.Check(Contract("probe.nested", ProbeJson.Default.NestedProbe))).Location);
        var tool = Assert.Throws<CapabilityContractException>(() => check.Check(Contract("probe.bad", ProbeJson.Default.PlainProbe, costTier: "huge")));
        Assert.Equal(("qian.test/probe.bad#/costTier", "tool-invalid"), (tool.Location, tool.Rule));
        Assert.Equal(3, logger.Messages.Count);

        // 合格契约：条目只生成一次，输入校验器已按 schemaSha256 预编译。
        var plain = Contract("probe.plain", ProbeJson.Default.PlainProbe);
        check.Check(plain);
        var entry = check.Entry(plain)!.Value;
        Assert.Equal("qian.test/probe.plain", entry.GetProperty("id").GetString());
        Assert.True(check.Inputs.IsPrepared(entry.GetProperty("schemaSha256").GetString()!));
        Assert.Equal(entry.GetRawText(), check.Entry(plain)!.Value.GetRawText());
        Assert.Equal(3, logger.Messages.Count);

        // HTTP 契约生成走同一套检查，按 INPUT_INVALID 报告，登记拒绝留在 InnerException。
        var openApi = Assert.Throws<QianErrorException>(() => CapabilityContractCompiler.CreateOpenApi([contract], new SurfaceValidator(), new ToolContractHasher()));
        Assert.Equal("INPUT_INVALID", openApi.Error.Code.Code);
        Assert.Equal("tenant-field", Assert.IsType<CapabilityContractException>(openApi.InnerException).Rule);
    }

    [Fact]
    public void 条目输入Schema不合格时_位置以能力ID为文件指向条目内的出错处_超长时退到合法上级()
    {
        var check = new CapabilityContractCheck(new SurfaceValidator(), new ToolContractHasher());
        var keys = Assert.Throws<CapabilityContractException>(() => check.Prepare("qian.test", "probe.keys", Entry("""{"type":"object","properties":{"p":{"patternProperties":{"^a/b$":{}}}}}""")));
        Assert.Equal(("qian.test/probe.keys#/inputSchema/properties/p/patternProperties/%5Ea~1b%24", "unsupported-key"), (keys.Location, keys.Rule));
        Assert.IsType<CapabilityInputSchemaException>(keys.InnerException);
        var reference = Assert.Throws<CapabilityContractException>(() => check.Prepare("qian.test", "probe.ref", Entry("""{"type":"object","properties":{"x":{"$ref":"#/$defs/missing"}}}""")));
        Assert.Equal(("qian.test/probe.ref#/inputSchema/properties/x/%24ref", "unresolved-reference"), (reference.Location, reference.Rule));
        var invalid = Assert.Throws<CapabilityContractException>(() => check.Prepare("qian.test", "probe.invalid", Entry("""{"type":5}""")));
        Assert.Equal(("qian.test/probe.invalid#/inputSchema", "schema-invalid"), (invalid.Location, invalid.Rule));
        Assert.All(new[] { keys, reference, invalid }, item => Assert.True(ErrorPointer.IsValid(item.Location), item.Location));
        var longKey = new string('k', 1100) + "/x";
        var trimmed = Assert.Throws<CapabilityContractException>(() => check.Prepare("qian.test", "probe.long", Entry("{\"type\":\"object\",\"patternProperties\":{\"" + longKey + "\":{}}}")));
        Assert.Equal("qian.test/probe.long#/inputSchema/patternProperties", trimmed.Location);
    }

    private static CapabilityContract Contract(string name, JsonTypeInfo request, string costTier = "low") => new("qian.test",
        "{\"name\":\"" + name + "\",\"description\":\"登记期契约检查的测试能力，只读取测试数据，不修改任何状态。\",\"sideEffect\":\"read\"," +
        "\"policyHint\":\"allow\",\"idempotent\":false,\"async\":\"none\",\"requiredScopes\":[],\"requiredSecrets\":[],\"dataClassification\":\"internal\",\"costTier\":\"" + costTier + "\"," +
        "\"contractVersion\":\"test-v1\",\"audit\":false,\"agent\":{\"expose\":false,\"requiresConfirmation\":false},\"binding\":{\"kind\":\"command\",\"id\":\"qian.test/" + name + "\"}," +
        "\"requiredPermission\":\"qian.test:probe:read\"}", request, ProbeJson.Default.ProbeResult);

    private static JsonElement Entry(string inputSchema)
    {
        using var schema = JsonDocument.Parse(inputSchema);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(inputSchema)));
        return JsonSerializer.SerializeToElement(new { inputSchema = schema.RootElement, schemaSha256 = hash });
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text); return document.RootElement.Clone();
    }

    private sealed class RecordingLogger : ILogger
    {
        internal List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { lock (Messages) Messages.Add(formatter(state, exception)); }
    }
}

internal sealed record TenantProbe(string TenantId, int Take);
internal sealed record NestedProbe(ProbeFilter Filter);
internal sealed record ProbeFilter(string Tenant);
internal sealed record PlainProbe(string Name, int Take);
internal sealed record ProbeResult(int Total);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(TenantProbe))]
[JsonSerializable(typeof(NestedProbe))]
[JsonSerializable(typeof(PlainProbe))]
[JsonSerializable(typeof(ProbeResult))]
internal sealed partial class ProbeJson : JsonSerializerContext;
