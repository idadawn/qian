using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qian.Plugin.Sdk.Generators;

namespace Qian.Plugin.Sdk.Tests;

public sealed class ComponentGeneratorTests
{
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private const string Source = """
        using System; using System.Threading; using System.Threading.Tasks; using System.Collections.Generic;
        using System.Text.Json; using Qian.Runtime; using Qian.Plugin.Sdk;
        [QianPlugin("qian.example")]
        public sealed class Plugin : IQianPlugin
        {
            public IReadOnlyList<ServiceKey> Inject => [];
            public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
            {
                ArgumentNullException.ThrowIfNull(ctx);
                ctx.Provide(new ServiceKey("Visible"), new object());
                return ValueTask.CompletedTask;
            }
            private static void Register(IPluginContext ctx) => ctx.Provide(new ServiceKey("Hidden"), new object());
        }
        """;
    [Theory]
    [InlineData("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "Register(ctx);")]
    [InlineData("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "var alias = ctx; Register(alias);")]
    [InlineData("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "if (config.ValueKind == JsonValueKind.Object) ctx.Provide(new ServiceKey(\"Visible\"), new object());")]
    [InlineData("Inject => [];", "Inject => System.Array.Empty<ServiceKey>();")]
    [InlineData("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "if (config.ValueKind == JsonValueKind.Null) return ValueTask.CompletedTask; ctx.Provide(new ServiceKey(\"Visible\"), new object());")]
    [InlineData("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "try { ctx.Provide(new ServiceKey(\"Visible\"), new object()); } catch { }")]
    public void 无法静态证明的注册明确报错_不生成空提供项(string before, string after)
    {
        var result = Generate(Source.Replace(before, after, StringComparison.Ordinal));
        Assert.Equal("QIANSDK002", Assert.Single(result.Diagnostics).Id); Assert.Empty(result.GeneratedTrees);
    }
    [Fact]
    public void 直接注册及空注入可生成组件元数据() => Assert.Single(Generate(Source).GeneratedTrees);
    [Theory]
    [InlineData("ctx.Provide(new ServiceKey(\"Data.Dialects\"), \"pg\", new object());", true)]
    [InlineData("ctx.Provide(new ServiceKey(\"Data.Dialects\"), config.GetProperty(\"name\").GetString()!, new object());", false)]
    public void 具名Provide只允许静态贡献名(string registration, bool valid)
    {
        var result = Generate(Source.Replace("ctx.Provide(new ServiceKey(\"Visible\"), new object());", registration, StringComparison.Ordinal));
        if (valid) { Assert.Empty(result.Diagnostics); Assert.Contains(":pg", Assert.Single(result.GeneratedTrees).ToString(), StringComparison.Ordinal); }
        else Assert.Equal("QIANSDK002", Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("registry.Register(\"pg\", new object());", true)]
    [InlineData("unrelated.Register(\"pg\", new object());", false)]
    [InlineData("registry.Register(config.GetProperty(\"name\").GetString()!, new object());", false)]
    [InlineData("registry.Register(\"mysql\", new object());", false)]
    public void 独立贡献绑定实际注册表符号_同名无关方法不能冒充(string registration, bool valid)
    {
        var source = Source.Replace("[QianPlugin(\"qian.example\")]", "[QianPlugin(\"qian.example\")][Contributes(typeof(Keys), nameof(Keys.Dialects), \"pg\", RegistryType = typeof(IRegistry))]", StringComparison.Ordinal)
            .Replace("ctx.Provide(new ServiceKey(\"Visible\"), new object());", "IRegistry registry = null!; Other unrelated = null!; " + registration, StringComparison.Ordinal) + """
            public static class Keys { public static readonly ServiceKey Dialects = new("Data.Dialects"); }
            public interface IRegistry { IDisposable Register(string name, object value); }
            public sealed class Other { public IDisposable Register(string name, object value) => null!; }
            """;
        var result = Generate(source);
        if (valid) Assert.Empty(result.Diagnostics); else Assert.Contains(result.Diagnostics, d => d.Id == "QIANSDK002");
    }

    [Theory]
    [InlineData("qian.audit", "Audit", true)]
    [InlineData("qian.authz", "Authorization", true)]
    [InlineData("qian.tenancy", "Tenancy", true)]
    [InlineData("qian.example", "Audit", false)]
    public void CommandPolicies按实际SDK语义与组件归属派生(string component, string kind, bool valid)
    {
        var source = Source.Replace("qian.example", component, StringComparison.Ordinal).Replace("ctx.Provide(new ServiceKey(\"Visible\"), new object());",
            "CommandPolicies.Register(ctx, CommandPolicyKind." + kind + ", (invocation, next) => next());", StringComparison.Ordinal);
        var result = Generate(source);
        if (valid) { Assert.Empty(result.Diagnostics); Assert.Contains(kind + "Key.Name", Assert.Single(result.GeneratedTrees).ToString(), StringComparison.Ordinal); }
        else Assert.Contains(result.Diagnostics, d => d.Id == "QIANSDK002");
    }

    [Theory]
    [InlineData("new Implementation().ApplyAsync(ctx, config, ct)", true)]
    [InlineData("new Implementation().ApplyAsync(ctx, default, ct)", false)]
    [InlineData("new Implementation().ApplyAsync(ctx, config, default)", false)]
    [InlineData("config.ValueKind == JsonValueKind.Object ? new Implementation().ApplyAsync(ctx, config, ct) : ValueTask.CompletedTask", false)]
    public void 稳定委托入口必须转交当前全部参数且不漏执行(string call, bool valid)
    {
        var source = """
            using System; using System.Collections.Generic; using System.Text.Json; using System.Threading; using System.Threading.Tasks; using Qian.Runtime; using Qian.Plugin.Sdk;
            [QianPlugin("qian.example")][DelegatePlugin(typeof(Implementation))][Provides(typeof(Keys), nameof(Keys.Value))]
            public sealed class Entry : IQianPlugin {
                public IReadOnlyList<ServiceKey> Inject => [];
                public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => CALL;
            }
            public static class Keys { public static readonly ServiceKey Value = new("Value"); }
            public sealed class Implementation : IQianPlugin {
                public IReadOnlyList<ServiceKey> Inject => [];
                public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) {ctx.Provide(Keys.Value, new object()); return ValueTask.CompletedTask;}
            }
            """.Replace("CALL", call, StringComparison.Ordinal);
        var result = Generate(source);
        if (valid) Assert.Empty(result.Diagnostics); else Assert.Contains(result.Diagnostics, d => d.Id == "QIANSDK002");
    }

    [Fact]
    public void 重复组件ID不能生成竞争入口()
    {
        var source = Source + """
            [QianPlugin("qian.example")] public sealed class Second : IQianPlugin {
                public IReadOnlyList<ServiceKey> Inject => [];
                public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var result = Generate(source); Assert.Equal(2, result.Diagnostics.Count(d => d.Id == "QIANSDK002")); Assert.Empty(result.GeneratedTrees);
    }

    private static GeneratorDriverRunResult Generate(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("ComponentFixture", [CSharpSyntaxTree.ParseText(source, parse)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(ImmutableArray.Create(new ComponentMetadataGenerator().AsSourceGenerator()), parseOptions: parse);
        return driver.RunGenerators(compilation).GetRunResult();
    }
}
