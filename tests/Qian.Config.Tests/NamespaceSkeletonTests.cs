using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Config.Tool;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

// S1-CFG-003 验收 2：config.schema 变更后，由 profile 推导的 namespace 骨架与线上键比对（ApolloFixture 协议替身作线上），缺键 / 多键即失败；
// 工具只输出键名与 Schema 指针，不回显线上值。真实工具进程的门禁自测见 tests/gates/test_config_namespaces.py。
public sealed class NamespaceSkeletonTests(ApolloFixture apollo) : IClassFixture<ApolloFixture>
{
    private const string Namespace = "qian.plugin.test.sample";
    private const string Title = "synthetic-title-value";
    private const string Credential = "secretRef://platform/sample/credential";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schema变更后骨架与线上键比对_缺键多键即失败_输出不含配置值()
    {
        var appId = "qian-skeleton-" + Guid.NewGuid().ToString("N");
        using var repo = new FixtureRepo(appId, ["application", Namespace]);
        repo.Schema(Sample());
        await Publish(appId, new() { ["pageSize"] = "20", ["title"] = Title, ["storage:kind"] = "local", ["storage:credential"] = Credential });

        var clean = await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri);
        Assert.Equal(0, clean.Exit);
        var sample = Assert.Single(clean.Report["profiles"]![0]!["namespaces"]!.AsArray());
        Assert.Equal((Namespace, "present", true, 4, 1, 1), (sample!["name"]!.GetValue<string>(), sample["online"]!.GetValue<string>(),
            sample["subscribed"]!.GetValue<bool>(), sample["keys"]!.GetValue<int>(), sample["required"]!.GetValue<int>(), sample["sensitive"]!.GetValue<int>()));

        // Schema 新增必填键（profile 条目随之补上）：线上缺键。
        var schema = Sample(); schema["properties"]!["region"] = new JsonObject { ["type"] = "string" }; schema["required"]!.AsArray().Add("region");
        repo.Schema(schema); repo.Configure(new JsonObject { ["storage"] = new JsonObject { ["kind"] = "local" }, ["region"] = "cn" });
        Assert.Equal([("missing", Namespace + ":region", "#/properties/region")], Findings(await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri)));

        // Schema 把 title 改名为 caption：线上旧键成为多键。
        schema = Sample(); schema["properties"]!.AsObject().Remove("title"); schema["properties"]!["caption"] = new JsonObject { ["type"] = "string" };
        repo.Schema(schema); repo.Configure(null);
        Assert.Equal([("extra", Namespace + ":title", "#")], Findings(await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri)));

        // 线上漂移：不带 namespace 前缀的键、标量下的子键、对象本身被赋值、必填子键缺失。
        repo.Schema(Sample());
        await Publish(appId, new() { ["pageSize"] = "20", ["storage"] = Title, ["storage:credential"] = Credential, ["storage:credential:extra"] = Title }, outside: new() { ["title"] = Title });
        var drift = await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri);
        Assert.Equal(1, drift.Exit);
        Assert.Equal([
            ("extra", Namespace + ":storage", "#/properties/storage"),
            ("extra", Namespace + ":storage:credential:extra", "#/properties/storage/properties/credential"),
            ("extra", "title", "#"),
            ("missing", Namespace + ":storage:kind", "#/properties/storage/properties/kind")], Findings(drift));
        foreach (var run in new[] { clean, drift }) Assert.DoesNotContain(Title, run.Output + run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("sample/credential", clean.Output + drift.Output, StringComparison.Ordinal);

        // 宿主订阅的 namespace 线上不存在即缺键；未订阅且不存在的不参与比对。
        await apollo.DeleteAsync(appId, Namespace, cancellationToken: Ct);
        Assert.Equal([("missing", Namespace, "#")], Findings(await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri)));
        repo.Subscribe(["application"]);
        var absent = await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri);
        Assert.Equal(0, absent.Exit);
        Assert.Equal("absent", absent.Report["profiles"]![0]!["namespaces"]![0]!["online"]!.GetValue<string>());
    }

    [Fact]
    public async Task 骨架文件只含Schema默认值_敏感键与无默认值的键写成注释()
    {
        using var repo = new FixtureRepo("qian-skeleton-files", ["application"]);
        repo.Schema(Sample());
        var output = Path.Combine(repo.Root, "skeleton");
        var run = await RunAsync(repo, "--out", output);
        Assert.Equal(0, run.Exit);
        Assert.Null(run.Report["profiles"]![0]!["namespaces"]![0]!["online"]);
        var lines = await File.ReadAllLinesAsync(Path.Combine(output, "test", Namespace + ".properties"), Ct);
        Assert.Equal([
            $"# 必填：{Namespace}:storage:kind",
            $"{Namespace}:pageSize = 20",
            $"# {Namespace}:storage:credential = secretRef://platform/…",
            $"# {Namespace}:storage:kind =",
            $"# {Namespace}:title ="], lines.Where(line => !line.StartsWith("# Apollo", StringComparison.Ordinal) && !line.StartsWith("# 由", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AccessKey开启后_未签名请求失败退出_按环境变量签名后通过()
    {
        var appId = "qian-skeleton-ak-" + Guid.NewGuid().ToString("N");
        var variable = "QIAN_CONFIG_TOOL_AK_" + Guid.NewGuid().ToString("N");
        using var repo = new FixtureRepo(appId, ["application", Namespace]);
        repo.Schema(Sample());
        await Publish(appId, new() { ["pageSize"] = "20", ["storage:kind"] = "local" });
        await apollo.SetAccessKeyAsync(appId, "synthetic-access-key", Ct);
        try
        {
            var unsigned = await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri);
            Assert.Equal(2, unsigned.Exit);
            Assert.Contains("AccessKey", unsigned.Error, StringComparison.Ordinal);
            Assert.Equal(2, (await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri, "--access-key-env", variable)).Exit);
            Environment.SetEnvironmentVariable(variable, "synthetic-access-key");
            var signed = await RunAsync(repo, "--compare", apollo.ConfigServiceUri.AbsoluteUri, "--access-key-env", variable);
            Assert.Equal(0, signed.Exit);
            Assert.DoesNotContain("synthetic-access-key", signed.Output + signed.Error, StringComparison.Ordinal);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); await apollo.ClearAccessKeyAsync(appId, Ct); }
    }

    [Fact]
    public async Task 参数不合法时退出码为2()
    {
        string[][] cases = [[], [".", "--app-id", "x"], [".", "--compare", "ftp://example.test/"], [".", "--compare", "http://user:pass@example.test/"], [".", "--unknown"], [".", "--out"]];
        foreach (var args in cases)
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.Equal(2, await ConfigTool.RunAsync(args, output, error, Ct));
            Assert.Contains("用法", error.ToString(), StringComparison.Ordinal);
        }
    }

    // Schema 语义的边界：数组下标、映射、开放对象、$ref 与 oneOf 并集。
    [Fact]
    public void 骨架比对覆盖数组映射开放对象引用与分支()
    {
        using var schema = JsonDocument.Parse("""
            {"type":"object","additionalProperties":false,"$defs":{"endpoint":{"type":"object","properties":{"url":{"type":"string"}},"required":["url"],"additionalProperties":false}},
             "properties":{
               "hosts":{"type":"array","items":{"type":"string"}},
               "labels":{"type":"object","additionalProperties":{"type":"string"}},
               "loose":{"type":"object","properties":{"known":{"type":"string"}}},
               "primary":{"$ref":"#/$defs/endpoint"},
               "mode":{"oneOf":[{"type":"object","properties":{"a":{"type":"string"}},"additionalProperties":false},
                                {"type":"object","properties":{"b":{"type":"string"}},"additionalProperties":false}]}}}
            """);
        var skeleton = NamespaceSkeleton.Create("qian.plugin.edge", schema.RootElement);
        string[] online = ["qian.plugin.edge:hosts:0", "qian.plugin.edge:hosts:first", "qian.plugin.edge:labels:any", "qian.plugin.edge:loose:other",
            "qian.plugin.edge:primary:URL", "qian.plugin.edge:primary:extra", "qian.plugin.edge:mode:a", "qian.plugin.edge:mode:b", "qian.plugin.edge:mode:c", "qian.plugin.edge:"];
        Assert.Equal([
                ("extra", "qian.plugin.edge:", "#"),
                ("extra", "qian.plugin.edge:hosts:first", "#/properties/hosts"),
                ("extra", "qian.plugin.edge:mode:c", "#/properties/mode"),
                ("extra", "qian.plugin.edge:primary:extra", "#/properties/primary")],
            skeleton.Compare("p", online).Select(item => (item.Kind, item.Key, item.Pointer)).Order());
        Assert.Equal([("missing", "qian.plugin.edge:primary:url", "#/$defs/endpoint/properties/url")],
            skeleton.Compare("p", ["qian.plugin.edge:primary:extra"]).Where(item => item.Kind == "missing").Select(item => (item.Kind, item.Key, item.Pointer)));
        Assert.Equal(["hosts", "labels", "loose:known", "mode:a", "mode:b", "primary:url"], skeleton.Keys.Select(key => key.Path));
    }

    private static JsonObject Sample() => new()
    {
        ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
        ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("storage"),
        ["properties"] = new JsonObject
        {
            ["pageSize"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["default"] = 20 },
            ["title"] = new JsonObject { ["type"] = "string" },
            ["storage"] = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("kind"),
                ["properties"] = new JsonObject
                {
                    ["kind"] = new JsonObject { ["enum"] = new JsonArray("local", "s3") },
                    ["credential"] = new JsonObject { ["type"] = "string", ["x-qian-sensitive"] = true }
                }
            }
        }
    };

    // keys 自动带上 namespace 前缀；outside 原样发布，模拟不带前缀的键。
    private async Task Publish(string appId, Dictionary<string, string> keys, Dictionary<string, string>? outside = null)
    {
        var values = keys.ToDictionary(item => Namespace + ":" + item.Key, item => item.Value);
        foreach (var (key, value) in outside ?? []) values[key] = value;
        await apollo.PublishAsync(appId, Namespace, values, cancellationToken: Ct);
    }

    private static async Task<(int Exit, JsonNode Report, string Output, string Error)> RunAsync(FixtureRepo repo, params string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        var exit = await ConfigTool.RunAsync([repo.Root, "--profile", "test", .. args], output, error, Ct);
        return (exit, exit == 2 ? new JsonObject() : JsonNode.Parse(output.ToString())!, output.ToString(), error.ToString());
    }

    private static (string, string, string)[] Findings((int Exit, JsonNode Report, string Output, string Error) run)
    {
        Assert.Equal(1, run.Exit);
        return run.Report["findings"]!.AsArray().Select(item => (item!["kind"]!.GetValue<string>(), item["key"]!.GetValue<string>(), item["pointer"]!.GetValue<string>())).Order().ToArray();
    }

    // 最小仓库：真实的 qian.config.apollo 清单与 Schema + 一个测试组件，profile test 用 patch 挂载两者。
    private sealed class FixtureRepo : IDisposable
    {
        private readonly string _appId;
        private string[] _namespaces = [];
        private JsonObject? _config;
        public FixtureRepo(string appId, string[] namespaces)
        {
            _appId = appId;
            Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-skeleton-" + Guid.NewGuid().ToString("N"))).FullName;
            var source = new DirectoryInfo(AppContext.BaseDirectory);
            while (source is not null && !File.Exists(Path.Combine(source.FullName, "Qian.slnx"))) source = source.Parent;
            foreach (var file in new[] { ".qian-plugin/plugin.json", "schemas/config.schema.json" })
            {
                var target = Path.Combine(Root, "plugins/qian.config.apollo", file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(source!.FullName, "plugins/qian.config.apollo", file), target);
            }
            Directory.CreateDirectory(Path.Combine(Root, "plugins/test.sample/.qian-plugin"));
            Directory.CreateDirectory(Path.Combine(Root, "plugins/test.sample/schemas"));
            File.WriteAllText(Path.Combine(Root, "plugins/test.sample/.qian-plugin/plugin.json"), """
                {"id":"test.sample","version":"1.0.0","description":"namespace 骨架测试组件","author":{"name":"Qian"},"license":"MIT",
                 "interface":{"displayName":"sample","category":"test"},"inject":[],"provides":[],
                 "policy":{"tier":"private","sandbox":"none","authentication":"ON_INSTALL","required":false},"scopes":["global"]}
                """);
            Directory.CreateDirectory(Path.Combine(Root, "profiles/test"));
            File.WriteAllText(Path.Combine(Root, "profiles/test/qian.profile.yml"), """
                {"name":"test","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}
                """);
            Subscribe(namespaces);
        }
        public string Root { get; }
        public void Schema(JsonObject schema) => File.WriteAllText(Path.Combine(Root, "plugins/test.sample/schemas/config.schema.json"), schema.ToJsonString());
        public void Subscribe(string[] namespaces) { _namespaces = namespaces; WritePatch(); }
        // profile 条目配置同样按组件 Schema 校验；null 回到默认的 { storage: { kind: local } }。
        public void Configure(JsonObject? config) { _config = config; WritePatch(); }
        private void WritePatch() => File.WriteAllText(Path.Combine(Root, "profiles/test/qian.patch.yml"), new JsonArray(
            new JsonObject { ["id"] = "config", ["plugin"] = "qian.config.apollo", ["config"] = new JsonObject
                { ["meta"] = "http://apollo-config.apollo:8080", ["appId"] = _appId, ["namespaces"] = new JsonArray([.. _namespaces.Select(name => JsonValue.Create(name))]) } },
            new JsonObject { ["id"] = "sample", ["plugin"] = "test.sample", ["config"] = _config?.DeepClone() ?? new JsonObject { ["storage"] = new JsonObject { ["kind"] = "local" } } }).ToJsonString());
        public void Dispose() => Directory.Delete(Root, true);
    }
}
