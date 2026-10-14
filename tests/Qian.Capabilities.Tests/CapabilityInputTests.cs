using System.Text;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors;

namespace Qian.Capabilities.Tests;

// S1-RT-019 / S1-RT-020：执行前的输入 Schema 校验、组件配置 catalogTtlMs 与 receiptMaxBytes、buf 输出解析。
public sealed class CapabilityInputTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void 输入Schema违反项映射为字段级错误码与输入JSON_Pointer_按位置排序且同一位置只报一次()
    {
        var entry = Entry("""
            {"type":"object","additionalProperties":false,"required":["id","name","items"],"properties":{
              "id":{"type":"string","format":"uuid"},
              "name":{"type":"string","minLength":2},
              "code":{"type":"string","pattern":"^[a-z]+$"},
              "tag":{"type":"string","minLength":5,"pattern":"^[a-z]+$"},
              "kind":{"enum":["a","b"]},
              "fixed":{"const":1},
              "count":{"type":"integer","minimum":1,"maximum":10},
              "items":{"type":"array","maxItems":2,"items":{"type":"object","additionalProperties":false,"required":["sku"],"properties":{"sku":{"type":"string"}}}},
              "a~b/c":{"type":"string"}}}
            """);
        var error = Assert.Throws<QianErrorException>(() => new CapabilityInputValidator().Validate(entry, Json("""
            {"id":"not-a-uuid","name":"A","code":"ABC","tag":"AB","kind":"c","fixed":2,"count":11,"items":[{"qty":1},{"sku":3}],"a~b/c":1,"extra":true}
            """)));
        Assert.Equal("INPUT_INVALID", error.Error.Code.Code);
        // tag 同时违反 minLength 与 pattern，同一位置只报先发现的一项。
        var fields = error.Error.Errors!;
        Assert.Equal([
            ("TYPE_MISMATCH", "#/a~0b~1c"), ("PATTERN_MISMATCH", "#/code"), ("OUT_OF_RANGE", "#/count"), ("UNKNOWN_PROPERTY", "#/extra"),
            ("VALUE_NOT_ALLOWED", "#/fixed"), ("FORMAT_INVALID", "#/id"), ("UNKNOWN_PROPERTY", "#/items/0/qty"), ("REQUIRED", "#/items/0/sku"),
            ("TYPE_MISMATCH", "#/items/1/sku"), ("VALUE_NOT_ALLOWED", "#/kind"), ("OUT_OF_RANGE", "#/name"), ("OUT_OF_RANGE", "#/tag")], fields.Select(item => (item.Code.Code, item.Pointer)));
        Assert.Equal([1], fields.Single(item => item.Pointer == "#/fixed").Allowed!.Select(value => value.GetInt32()));
        Assert.Equal(["a", "b"], fields.Single(item => item.Pointer == "#/kind").Allowed!.Select(value => value.GetString()!));
    }

    [Fact]
    public void 组合与条件关键字_只报告真正的输入错误_if与not的内部失败不算错误()
    {
        var entry = Entry("""
            {"type":"object","additionalProperties":false,"properties":{
              "mode":{"type":"string"},"target":{"type":"string"},"flag":{},"either":{"anyOf":[{"type":"string"},{"type":"integer"}]},
              "one":{"oneOf":[{"type":"integer"},{"type":"number"}]}},
             "if":{"properties":{"mode":{"const":"send"}},"required":["mode"]},"then":{"required":["target"]},
             "not":{"required":["flag"]}}
            """);
        var validator = new CapabilityInputValidator();
        validator.Validate(entry, Json("""{"mode":"draft","either":"x","one":1.5}"""));
        Assert.Equal([("REQUIRED", "#/target")], Codes(() => validator.Validate(entry, Json("""{"mode":"send"}"""))));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#")], Codes(() => validator.Validate(entry, Json("""{"flag":true}"""))));
        Assert.Equal([("TYPE_MISMATCH", "#/either")], Codes(() => validator.Validate(entry, Json("""{"either":true}"""))));
        // 1 同时匹配 integer 与 number：oneOf 的子项都通过，只能在该位置报告取值不允许。
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/one")], Codes(() => validator.Validate(entry, Json("""{"one":1}"""))));
    }

    [Fact]
    public void 引用_前缀项_contains_模式属性与未评估属性_错误定位到输入里的实际位置()
    {
        var entry = Entry("""
            {"type":"object","additionalProperties":false,
             "$defs":{"line":{"type":"object","additionalProperties":false,"required":["sku"],"properties":{"sku":{"type":"string"}}}},
             "properties":{
              "lines":{"type":"array","items":{"$ref":"#/$defs/line"}},
              "pair":{"type":"array","prefixItems":[{"type":"string"},{"type":"object"}],"items":{"type":"integer"}},
              "tail":{"type":"array","prefixItems":[{"type":"string"}],"items":false},
              "has":{"type":"array","contains":{"type":"integer"}},
              "pat":{"type":"object","patternProperties":{"^x":{"type":"string"}},"additionalProperties":false},
              "uneval":{"type":"object","properties":{"k":{}},"unevaluatedProperties":false},
              "self":{"$ref":"#"}}}
            """);
        // pair 的第 4 项（下标 3）类型不符：结果树给的是相对 prefixItems 的下标，校验器换回实际下标。
        Assert.Equal([
            ("VALUE_NOT_ALLOWED", "#/has"), ("UNKNOWN_PROPERTY", "#/lines/1/qty"), ("REQUIRED", "#/lines/1/sku"), ("TYPE_MISMATCH", "#/pair/3"),
            ("UNKNOWN_PROPERTY", "#/pat/q"), ("TYPE_MISMATCH", "#/pat/x1"), ("REQUIRED", "#/self/lines/0/sku"), ("OUT_OF_RANGE", "#/tail"), ("UNKNOWN_PROPERTY", "#/uneval/m")],
            Codes(() => new CapabilityInputValidator().Validate(entry, Json("""
                {"lines":[{"sku":"a"},{"qty":1}],"pair":["a",{},3,"x"],"tail":["a",1],"has":["s"],"pat":{"x1":1,"q":2},"uneval":{"k":1,"m":2},"self":{"lines":[{}]}}
                """))));
    }

    [Fact]
    public void anyOf与oneOf只报告最接近的分支_判别字段不符合并允许值_类型不符的分支让位_多分支同时匹配或并列分支结论不同只在该位置报告()
    {
        var entry = Entry("""
            {"type":"object","properties":{
              "shape":{"anyOf":[
                {"type":"object","required":["$type","radius"],"properties":{"$type":{"const":"circle"},"radius":{"type":"number"}}},
                {"type":"object","required":["$type","side"],"properties":{"$type":{"const":"square"},"side":{"type":"number"}}}]},
              "either":{"anyOf":[{"type":"string"},{"type":"object","required":["k"]}]},
              "mode":{"oneOf":[{"const":"fast"},{"const":"safe"}]},
              "one":{"oneOf":[{"type":"integer"},{"type":"number"}]},
              "mixed":{"anyOf":[{"type":"object","required":["a"]},{"type":"object","required":["b"]}]}}}
            """);
        var validator = new CapabilityInputValidator();
        // 判别字段对得上的分支胜出：只报该分支的问题，不把另一分支要求的 radius 混进来。
        Assert.Equal([("REQUIRED", "#/shape/side")], Codes(() => validator.Validate(entry, Json("""{"shape":{"$type":"square"}}"""))));
        Assert.Equal([("TYPE_MISMATCH", "#/shape/side")], Codes(() => validator.Validate(entry, Json("""{"shape":{"$type":"square","side":"x"}}"""))));
        // 判别字段与所有分支都不符：只报判别字段（pointer 是 URI 片段形式，$ 编码为 %24），允许值取各分支的并集。
        var unknown = Fields(() => validator.Validate(entry, Json("""{"shape":{"$type":"triangle"}}""")));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/shape/%24type")], unknown.Select(item => (item.Code.Code, item.Pointer)));
        Assert.Equal(["circle", "square"], unknown[0].Allowed!.Select(value => value.GetString()!));
        // 本位置类型不符的分支让位于类型相符的分支。
        Assert.Equal([("REQUIRED", "#/either/k")], Codes(() => validator.Validate(entry, Json("""{"either":{}}"""))));
        var mode = Fields(() => validator.Validate(entry, Json("""{"mode":"slow"}""")));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/mode")], mode.Select(item => (item.Code.Code, item.Pointer)));
        Assert.Equal(["fast", "safe"], mode[0].Allowed!.Select(value => value.GetString()!));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/one")], Codes(() => validator.Validate(entry, Json("""{"one":1}"""))));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/mixed")], Codes(() => validator.Validate(entry, Json("""{"mixed":{}}"""))));

        // 可空对象（oneOf 一支是 null）：类型不符的 null 分支先排除，即使它的违反项更少。
        var nullable = Entry("""
            {"type":"object","$defs":{"item":{"type":"object","properties":{"kind":{"enum":["a","b"]},"n":{"type":"integer"}}}},"properties":{
              "s":{"oneOf":[{"type":"null"},{"type":"object","properties":{"kind":{"enum":["a","b"]},"n":{"type":"integer"}}}]},
              "r":{"anyOf":[{"$ref":"#/$defs/item"},{"type":"null"}]}}}
            """);
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/s/kind"), ("TYPE_MISMATCH", "#/s/n")], Codes(() => validator.Validate(nullable, Json("""{"s":{"kind":"bad","n":"x"}}"""))));
        var kind = Fields(() => validator.Validate(nullable, Json("""{"s":{"kind":"bad"}}""")));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/s/kind")], kind.Select(item => (item.Code.Code, item.Pointer)));
        Assert.Equal(["a", "b"], kind[0].Allowed!.Select(value => value.GetString()!));
        Assert.Equal([("TYPE_MISMATCH", "#/r/n")], Codes(() => validator.Validate(nullable, Json("""{"r":{"n":"x"}}"""))));
    }

    [Fact]
    public void 超长属性名退到合法上级位置_format只强制uuid与date_内嵌id与锚点引用可定位_contains计数不足为越界()
    {
        var entry = Entry("""
            {"type":"object","additionalProperties":false,
             "$defs":{
               "inner":{"$id":"inner.json","type":"object","required":["z"],"properties":{"w":{"type":"object","required":["y"]}}},
               "shape":{"$anchor":"shape","type":"object","required":["s"]}},
             "properties":{
               "nested":{"type":"object","additionalProperties":false},
               "p":{"$ref":"inner.json"},"q":{"$ref":"inner.json#/properties/w"},"a":{"$ref":"#shape"},
               "at":{"type":"string","format":"date-time"},"t":{"type":"string","format":"time"},"u":{"type":"string","format":"uri"},
               "e":{"type":"string","format":"email"},"id":{"type":"string","format":"uuid"},"day":{"type":"string","format":"date"},
               "format":{"type":"string"},"cfg":{"const":{"format":"date-time"}},
               "c":{"type":"array","contains":{"type":"integer"},"minContains":2},
               "has":{"type":"array","contains":{"type":"integer"}}}}
            """);
        var validator = new CapabilityInputValidator();
        // pointer 上限 1024 字符：1100 个 ASCII 字符或 120 个汉字（百分号编码后 1080 字符）的属性名退到上级位置。
        var ascii = new string('k', 1100); var chinese = string.Concat(Enumerable.Repeat("客", 120));
        Assert.Equal([("UNKNOWN_PROPERTY", "#"), ("UNKNOWN_PROPERTY", "#/nested")],
            Codes(() => validator.Validate(entry, Json("{\"" + ascii + "\":1,\"nested\":{\"" + chinese + "\":1}}"))));
        // 不带时区偏移的时间、相对 URI 等 System.Text.Json 能解析的值不按 format 拒绝；uuid 与 date 仍强制。名为 format 的属性与 const 里的数据照常校验。
        validator.Validate(entry, Json("""
            {"at":"2026-10-14T08:00:00","t":"08:00:00","u":"relative/path","e":"not-an-email","id":"0199e6c1-2f4b-7c3a-9d1e-5a6b7c8d9e0f","day":"2026-10-14","format":"x","cfg":{"format":"date-time"}}
            """));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/cfg"), ("FORMAT_INVALID", "#/day"), ("TYPE_MISMATCH", "#/format"), ("FORMAT_INVALID", "#/id")],
            Codes(() => validator.Validate(entry, Json("""{"id":"x","day":"2026-13-01","format":1,"cfg":{}}"""))));
        // 带 $id 的内嵌资源与 $anchor 引用：违反项定位到引用目标里的要求。
        Assert.Equal([("REQUIRED", "#/a/s"), ("REQUIRED", "#/p/z"), ("REQUIRED", "#/q/y")], Codes(() => validator.Validate(entry, Json("""{"p":{},"q":{},"a":{}}"""))));
        Assert.Equal([("OUT_OF_RANGE", "#/c"), ("VALUE_NOT_ALLOWED", "#/has")], Codes(() => validator.Validate(entry, Json("""{"c":[1,"x"],"has":["x"]}"""))));
    }

    [Fact]
    public void 根类型不符_重复成员_超量错误分别处理()
    {
        var entry = Entry("""{"type":"object","additionalProperties":false,"properties":{"name":{"type":"string"}}}""");
        var validator = new CapabilityInputValidator();
        Assert.Equal([("TYPE_MISMATCH", "#")], Codes(() => validator.Validate(entry, Json("[]"))));
        var duplicate = Assert.Throws<QianErrorException>(() => validator.Validate(entry, Json("""{"name":"a","name":"b"}""")));
        Assert.Equal("BAD_REQUEST", duplicate.Error.Code.Code);
        var nested = Assert.Throws<QianErrorException>(() => validator.Validate(entry, Json("""{"name":"a","x":[{"k":1,"k":2}]}""")));
        Assert.Equal("BAD_REQUEST", nested.Error.Code.Code);
        var many = "{" + string.Join(",", Enumerable.Range(0, 150).Select(index => "\"p" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + "\":1")) + "}";
        var capped = Codes(() => validator.Validate(entry, Json(many)));
        Assert.Equal(100, capped.Length);
        Assert.All(capped, item => Assert.Equal("UNKNOWN_PROPERTY", item.Code));
        Assert.Equal("#/p000", capped[0].Pointer); Assert.Equal("#/p099", capped[^1].Pointer);
        // 成员名或字符串含孤立的 UTF-16 代理项（JSON 里的 \ud800 转义）：读不成确定的值，按无法解析的请求处理，不是服务端错误。
        var lone = "\\" + "ud800";
        foreach (var text in new[] { "{\"" + lone + "\":1}", "{\"name\":\"" + lone + "\"}", "{\"name\":\"a\",\"x\":[\"" + lone + "x\"]}" })
            Assert.Equal("BAD_REQUEST", Assert.Throws<QianErrorException>(() => validator.Validate(entry, Json(text))).Error.Code.Code);
        // 库无法可靠求值的 Schema（patternProperties / dependentSchemas 的键含 / 或 ~）编译前即拒绝，不放过约束；登记期检查见 CapabilityContractCheckTests。
        foreach (var schema in new[] {
            """{"type":"object","patternProperties":{"^a/b$":{"type":"string"}}}""", """{"type":"object","properties":{"p":{"patternProperties":{"^a~b$":{}}}}}""",
            """{"type":"object","dependentSchemas":{"a/b":{"required":["c"]}}}""" })
            Assert.Equal("unsupported-key", Assert.Throws<CapabilityInputSchemaException>(() => validator.Validate(Entry(schema), Json("""{"zz":1}"""))).Rule);
        validator.Validate(Entry("""{"type":"object","dependentRequired":{"a/b":["c"]},"properties":{"x/y":{"type":"integer"}}}"""), Json("""{"x/y":1}"""));
    }

    [Fact]
    public void 组件配置catalogTtlMs默认1000_取值0到3000_与插件配置Schema判定一致()
    {
        Assert.Equal(1000, CapabilitiesOptions.Parse(Json("{}")).CatalogTtlMs);
        Assert.Equal(1000, CapabilitiesOptions.Default.CatalogTtlMs);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.capabilities/schemas/config.schema.json")));
        var cases = new (string Config, int? Ttl, string? Code, string? Pointer)[]
        {
            ("{}", 1000, null, null), ("""{"catalogTtlMs":1.0}""", 1, null, null), ("""{"catalogTtlMs":2e3}""", 2000, null, null), ("""{"catalogTtlMs":0}""", 0, null, null), ("""{"catalogTtlMs":3000}""", 3000, null, null), ("""{"catalogTtlMs":250}""", 250, null, null),
            ("""{"catalogTtlMs":-1}""", null, "OUT_OF_RANGE", "#/catalogTtlMs"), ("""{"catalogTtlMs":3001}""", null, "OUT_OF_RANGE", "#/catalogTtlMs"),
            ("""{"catalogTtlMs":"1000"}""", null, "TYPE_MISMATCH", "#/catalogTtlMs"), ("""{"catalogTtlMs":1.5}""", null, "TYPE_MISMATCH", "#/catalogTtlMs"),
            ("""{"catalogTtlMs":null}""", null, "TYPE_MISMATCH", "#/catalogTtlMs"), ("""{"ttl":1000}""", null, "UNKNOWN_PROPERTY", "#/ttl"), ("[]", null, "TYPE_MISMATCH", "#"),
            // 成员名过长时 pointer 指向整个配置。
            ("{\"" + new string('k', 1100) + "\":1}", null, "UNKNOWN_PROPERTY", "#")
        };
        foreach (var (config, ttl, code, pointer) in cases)
        {
            var value = Json(config);
            Assert.Equal(ttl is not null, schema.Evaluate(value).IsValid);
            if (ttl is { } expected) { Assert.Equal(expected, CapabilitiesOptions.Parse(value).CatalogTtlMs); continue; }
            var error = Assert.Throws<QianErrorException>(() => CapabilitiesOptions.Parse(value));
            Assert.Equal("INPUT_INVALID", error.Error.Code.Code);
            Assert.Equal((code, pointer), (Assert.Single(error.Error.Errors!).Code.Code, error.Error.Errors![0].Pointer));
        }
        var duplicate = Assert.Throws<QianErrorException>(() => CapabilitiesOptions.Parse(Json("""{"catalogTtlMs":1,"catalogTtlMs":2}""")));
        Assert.Equal("BAD_REQUEST", duplicate.Error.Code.Code);
    }

    [Fact]
    public void 组件配置receiptMaxBytes默认65536_取值0到1MiB_与插件配置Schema判定一致()
    {
        Assert.Equal(65536, CapabilitiesOptions.Parse(Json("{}")).ReceiptMaxBytes);
        Assert.Equal(65536, CapabilitiesOptions.Default.ReceiptMaxBytes);
        // 配置上限与回执表的检查约束一致。
        Assert.Equal(CapabilitiesDbContext.ReceiptResponseLimit, CapabilitiesOptions.MaxReceiptMaxBytes);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.capabilities/schemas/config.schema.json")));
        var cases = new (string Config, int? Limit, string? Code)[]
        {
            ("""{"receiptMaxBytes":0}""", 0, null), ("""{"receiptMaxBytes":1048576}""", 1048576, null), ("""{"receiptMaxBytes":1024,"catalogTtlMs":2500}""", 1024, null),
            ("""{"receiptMaxBytes":-1}""", null, "OUT_OF_RANGE"), ("""{"receiptMaxBytes":1048577}""", null, "OUT_OF_RANGE"),
            ("""{"receiptMaxBytes":"64"}""", null, "TYPE_MISMATCH"), ("""{"receiptMaxBytes":0.5}""", null, "TYPE_MISMATCH"),
        };
        foreach (var (config, limit, code) in cases)
        {
            var value = Json(config);
            Assert.Equal(limit is not null, schema.Evaluate(value).IsValid);
            if (limit is { } expected) { Assert.Equal(expected, CapabilitiesOptions.Parse(value).ReceiptMaxBytes); continue; }
            var error = Assert.Throws<QianErrorException>(() => CapabilitiesOptions.Parse(value));
            Assert.Equal((code, "#/receiptMaxBytes"), (Assert.Single(error.Error.Errors!).Code.Code, error.Error.Errors![0].Pointer));
        }
        Assert.Equal(2500, CapabilitiesOptions.Parse(Json("""{"receiptMaxBytes":1024,"catalogTtlMs":2500}""")).CatalogTtlMs);
    }

    [Fact]
    public void buf规则只从标准输出取_标准输出的其余行与标准错误分开截断保留_不抛异常()
    {
        var output = string.Join("\n",
            """{"path":"model.proto","start_line":3,"type":"FIELD_SAME_TYPE","message":"字段类型改变"}""",
            "WARN: flag --error-format is deprecated",
            """{"type":"FIELD_NO_DELETE"}""",
            """{"type":"FIELD_SAME_TYPE"}""",
            "{not json",
            "[1,2]",
            """{"type":"bad type!"}""",
            """{"message":"no type"}""");
        // 标准错误里形似规则的行也不算规则。
        var report = BufProtoCompatibilityProbe.ParseBreaking(output, "progress 100%\n" + """{"type":"PACKAGE_NO_DELETE"}""" + "\n" + new string('x', 600));
        Assert.Equal(["FIELD_NO_DELETE", "FIELD_SAME_TYPE"], report.Rules);
        Assert.Equal(["WARN: flag --error-format is deprecated", "{not json", "[1,2]", """{"type":"bad type!"}""", """{"message":"no type"}"""], report.Noise);
        Assert.Equal(["progress 100%", """{"type":"PACKAGE_NO_DELETE"}""", new string('x', 500)], report.Errors);
        var lines = string.Join("\n", Enumerable.Range(0, 80).Select(index => "line " + index));
        var many = BufProtoCompatibilityProbe.ParseBreaking(lines, lines);
        Assert.Empty(many.Rules); Assert.Equal(50, many.Noise.Count); Assert.Equal(50, many.Errors.Count);
    }

    [Fact]
    public async Task buf退出码与输出组合_标准输出噪声与标准错误分别写日志_缺规则或执行失败带两路诊断拒绝()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "替身检查器是 POSIX shell 脚本。");
        var directory = Directory.CreateTempSubdirectory("qian-buf-stub-");
        try
        {
            var logger = new RecordingLogger();
            var noisy = await Stub(directory, "noisy", 100, ["""{"type":"FIELD_SAME_TYPE","message":"字段类型改变"}""", "WARN: progress"], ["""{"type":"PACKAGE_NO_DELETE"}""", "note: cache"]);
            var result = await new BufProtoCompatibilityProbe(noisy, logger).CompareDescriptorsAsync(new byte[] { 1 }, new byte[] { 2 }, Ct);
            Assert.False(result.IsCompatible); Assert.Equal(["FIELD_SAME_TYPE"], result.BreakingRules);
            Assert.Equal(2, logger.Messages.Count);
            var stdout = Assert.Single(logger.Messages, message => message.Contains("标准输出", StringComparison.Ordinal));
            Assert.Contains("WARN: progress", stdout, StringComparison.Ordinal); Assert.DoesNotContain("note: cache", stdout, StringComparison.Ordinal);
            var stderr = Assert.Single(logger.Messages, message => message.Contains("标准错误", StringComparison.Ordinal));
            Assert.Contains("note: cache", stderr, StringComparison.Ordinal); Assert.Contains("PACKAGE_NO_DELETE", stderr, StringComparison.Ordinal);

            var clean = await Stub(directory, "clean", 0, ["note: nothing to compare"]);
            Assert.True((await new BufProtoCompatibilityProbe(clean).CompareDescriptorsAsync(new byte[] { 1 }, new byte[] { 2 }, Ct)).IsCompatible);

            // 规则行只在标准错误里：不算规则，按缺少规则拒绝。
            var unexplained = await Stub(directory, "unexplained", 100, ["only noise"], ["""{"type":"FIELD_SAME_TYPE"}"""]);
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => new BufProtoCompatibilityProbe(unexplained).CompareDescriptorsAsync(new byte[] { 1 }, new byte[] { 2 }, Ct).AsTask());
            Assert.Equal(("only noise", """{"type":"FIELD_SAME_TYPE"}"""), (missing.Data["contractDiagnostics"], missing.Data["contractErrors"]));

            var crashed = await Stub(directory, "crashed", 3, [], ["panic: boom"]);
            var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => new BufProtoCompatibilityProbe(crashed).CompareDescriptorsAsync(new byte[] { 1 }, new byte[] { 2 }, Ct).AsTask());
            Assert.Equal(("", "panic: boom"), (failed.Data["contractDiagnostics"], failed.Data["contractErrors"]));
        }
        finally { directory.Delete(true); }
    }

    // 只用 shell 内建命令：检查器在清空的环境变量下运行，没有 PATH。
    private static async Task<string> Stub(DirectoryInfo directory, string name, int exit, string[] output, string[]? error = null)
    {
        var path = Path.Combine(directory.FullName, name);
        var script = new StringBuilder("#!/bin/sh\nif [ \"$1\" = \"--version\" ]; then echo 1.73.0; exit 0; fi\n");
        foreach (var line in output) script.Append("echo '").Append(line.Replace("'", "'\\''", StringComparison.Ordinal)).Append("'\n");
        foreach (var line in error ?? []) script.Append("echo '").Append(line.Replace("'", "'\\''", StringComparison.Ordinal)).Append("' >&2\n");
        script.Append("exit ").Append(exit).Append('\n');
        await File.WriteAllTextAsync(path, script.ToString(), Ct);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static (string Code, string Pointer)[] Codes(Action action) =>
        Fields(action).Select(item => (item.Code.Code, item.Pointer)).ToArray();

    private static IReadOnlyList<QianFieldError> Fields(Action action)
    {
        var error = Assert.Throws<QianErrorException>(action);
        Assert.Equal("INPUT_INVALID", error.Error.Code.Code);
        return error.Error.Errors!;
    }

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

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
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
