using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qian.Plugin.Sdk.Generators;

namespace Qian.Plugin.Sdk.Tests;

public sealed class GeneratorDiagnosticTests
{
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(CapabilityAttribute).Assembly.Location).Append(typeof(Qian.Runtime.IQianPlugin).Assembly.Location).Distinct(StringComparer.Ordinal)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private const string Source = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using Qian.Runtime;
        using Qian.Plugin.Sdk;
        namespace Example;
        [QianPlugin("qian.example")]
        public sealed class Plugin : IQianPlugin
        {
            public IReadOnlyList<ServiceKey> Inject => [];
            public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
        }
        [Capability("customer.create", typeof(Response), typeof(Json), Description = "创建当前租户的一条客户记录。", SideEffect = "write", Idempotent = true)]
        public sealed partial record Request(Guid IdempotencyKey, string Name);
        public sealed record Response(Guid Id);
        [JsonSerializable(typeof(Request))]
        [JsonSerializable(typeof(Response))]
        public abstract partial class Json : JsonSerializerContext;
        """;

    [Theory]
    [InlineData("Idempotent = true", "Idempotent = false", "幂等")]
    [InlineData("partial record Request", "record Request", "partial")]
    [InlineData("Description = \"创建当前租户的一条客户记录。\"", "Description = \"短\"", "描述")]
    [InlineData("[JsonSerializable(typeof(Request))]", "", "显式登记")]
    [InlineData("[QianPlugin(\"qian.example\")]", "", "一个 QianPlugin")]
    public void 错误声明产生明确编译诊断(string before, string after, string expected)
    {
        var result = Generate(Source.Replace(before, after, StringComparison.Ordinal));
        var diagnostic = Assert.Single(result.Diagnostics); Assert.Equal("QIANSDK001", diagnostic.Id);
        Assert.Contains(expected, diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal); Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void 删除能力属性会删除全部派生能力_同名声明被拒绝()
    {
        Assert.Single(Generate(Source).GeneratedTrees);
        var without = Source[(Source.IndexOf("using System;", StringComparison.Ordinal))..];
        without = without.Replace("[Capability(\"customer.create\", typeof(Response), typeof(Json), Description = \"创建当前租户的一条客户记录。\", SideEffect = \"write\", Idempotent = true)]", "", StringComparison.Ordinal);
        Assert.Empty(Generate(without).GeneratedTrees);
        var duplicate = Source + "\n[Capability(\"customer.create\", typeof(Response), typeof(Json), Description = \"查询当前租户的一条客户记录。\")] public sealed partial record Other(Guid Id);";
        Assert.Equal(2, Generate(duplicate).Diagnostics.Count(d => d.Id == "QIANSDK001"));
    }

    private static GeneratorDriverRunResult Generate(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("GeneratorFixture", [CSharpSyntaxTree.ParseText(source, parse)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(ImmutableArray.Create(new CapabilityGenerator().AsSourceGenerator()), parseOptions: parse);
        return driver.RunGenerators(compilation).GetRunResult();
    }
}
