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
    public void 无法静态证明的注册明确报错_不生成空提供项(string before, string after)
    {
        var result = Generate(Source.Replace(before, after, StringComparison.Ordinal));
        Assert.Equal("QIANSDK002", Assert.Single(result.Diagnostics).Id); Assert.Empty(result.GeneratedTrees);
    }
    [Fact]
    public void 直接注册及空注入可生成组件元数据() => Assert.Single(Generate(Source).GeneratedTrees);
    private static GeneratorDriverRunResult Generate(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("ComponentFixture", [CSharpSyntaxTree.ParseText(source, parse)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(ImmutableArray.Create(new ComponentMetadataGenerator().AsSourceGenerator()), parseOptions: parse);
        return driver.RunGenerators(compilation).GetRunResult();
    }
}
