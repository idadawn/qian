using System.Text.Json;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

namespace Qian.Profiles.Tests;

// 敏感配置检查里的键名判定：动态键按 ECMA-262 解释，属性名与动态键都不区分大小写，方言判断不同的键名按敏感处理。
// 只测 .NET 装配器：共享 TCK 同时约束 CLI，而 CLI 的 profiles.ts 仍按 JS 方言、区分大小写判定。
public sealed class ProfileSensitiveConfigTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Accent = new((char)0x00E9, 1);
    private static readonly string Spaced = "a" + (char)0x00A0 + "b";
    // 动态键：^legacy_ 与 ^\W+$ 敏感、^[a-z]+$ 不敏感；limits 只声明 size，其余键名敏感。
    private const string Schema = """
        {"type":"object","properties":{"port":{"type":"integer"},"marker":{"type":"string"},"obsolete":{"type":"string"},
         "secret":{"type":"string","x-qian-sensitive":true},
         "endpoints":{"type":"object","patternProperties":{"^legacy_":{"type":"string","x-qian-sensitive":true},"^\\W+$":{"type":"string","x-qian-sensitive":true},"^[a-z]+$":{"type":"string"}}},
         "limits":{"type":"object","properties":{"size":{"type":"string"}},"additionalProperties":{"type":"string","x-qian-sensitive":true}}}}
        """;

    [Fact]
    public void 明文放在敏感键的任一写法下都被拒绝_诊断不含原值()
    {
        var cases = new (string Field, string Key, string Reason)[]
        {
            // \W 按 ECMA-262 只认 ASCII 单词字符，é 属于 \W。
            ("endpoints", Accent, "敏感配置必须为 secretRef，不能打印或保存明文。"),
            // 动态键与属性名都按 Web 默认反序列化的口径不区分大小写。
            ("endpoints", "Legacy_billing", "敏感配置必须为 secretRef，不能打印或保存明文。"),
            ("", "Secret", "敏感配置必须为 secretRef，不能打印或保存明文。"),
            // Size 按原样没有声明，验证器与直接读 JsonElement 的插件都把它当作 additionalProperties。
            ("limits", "Size", "敏感配置必须为 secretRef，不能打印或保存明文。"),
            ("endpoints", Spaced, "键名含各正则方言判断不同的字符，按敏感配置处理，必须为 secretRef。"),
        };
        foreach (var (field, key, reason) in cases)
        {
            var config = new Dictionary<string, object> { ["port"] = 80 };
            if (field.Length == 0) config[key] = "DO-NOT-LEAK";
            else config[field] = new Dictionary<string, string> { [key] = "DO-NOT-LEAK" };
            using var fixture = new Fixture(Schema, config);
            var result = fixture.Load();
            var pointer = "ops.yml#/0/config/" + (field.Length == 0 ? key : field + "/" + key);
            Assert.False(result.IsValid, pointer);
            Assert.Contains(result.Diagnostics.SelectMany(d => d.Error.Errors ?? []), error => error.Pointer == pointer && error.Message == reason);
            Assert.DoesNotContain("DO-NOT-LEAK", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void 合法引用与普通键名照常通过()
    {
        var config = new Dictionary<string, object>
        {
            ["port"] = 80,
            ["Secret"] = "secretRef://platform/settings/secret",
            ["endpoints"] = new Dictionary<string, string>
            {
                ["orders"] = "plain", ["Legacy_billing"] = "secretRef://platform/billing/legacy",
                [Accent] = "secretRef://platform/billing/accent", [Spaced] = "secretRef://platform/billing/spaced",
            },
            ["limits"] = new Dictionary<string, string> { ["size"] = "large" },
        };
        using var fixture = new Fixture(Schema, config);
        var result = fixture.Load();
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics));
    }

    [Fact]
    public void 超出可移植子集的模式让装配失败_不跳过敏感标记()
    {
        // 验证器（ECMAScript 方言）接受先行断言，但各方言无法一致地解释，装配失败而不是忽略该模式。
        const string schema = """
            {"type":"object","properties":{"port":{"type":"integer"},"marker":{"type":"string"},"obsolete":{"type":"string"},
             "endpoints":{"type":"object","patternProperties":{"^(?=x)x":{"x-qian-sensitive":true}}}}}
            """;
        var config = new Dictionary<string, object> { ["port"] = 80, ["endpoints"] = new Dictionary<string, string> { ["x"] = "secretRef://platform/x/y" } };
        using var fixture = new Fixture(schema, config);
        var result = fixture.Load();
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics.SelectMany(d => d.Error.Errors ?? []), error =>
            error.Pointer == "ops.yml#/0/config/endpoints/x" && error.Message == "组件 config.schema 的 patternProperties 含不支持的正则写法，只接受 ECMA-262 可移植子集。");
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Qian.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("找不到仓库。");
    }

    // 复制共享 TCK 的基础目录，换上测试用的组件配置 Schema，再以 ops.yml 整体替换 settings 条目的配置。
    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "qian-profile-sensitive-" + Guid.NewGuid().ToString("N"));

        internal Fixture(string schema, Dictionary<string, object> config)
        {
            var template = Path.Combine(Root, "tck/profiles/base");
            foreach (var source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(_directory, Path.GetRelativePath(template, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
            File.WriteAllText(Path.Combine(_directory, "plugins/settings/schemas/config.schema.json"), schema);
            // JSON 是合法的 YAML 流式写法；非 ASCII 键名序列化成 YAML 也认的 \u 转义。
            File.WriteAllText(Path.Combine(_directory, "ops.yml"), JsonSerializer.Serialize(new[] { new { id = "settings", config } }));
        }

        internal ProfileLoadResult Load() => new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions
        {
            WorkingDirectory = _directory, Profile = "test", HomeDirectory = "home", IncludeHomePatch = false, Patches = ["ops.yml"],
        });

        public void Dispose() => Directory.Delete(_directory, true);
    }
}
