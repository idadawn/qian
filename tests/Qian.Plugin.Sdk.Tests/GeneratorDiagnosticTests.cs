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
    [InlineData("[QianPlugin(\"qian.example\")]", "", "唯一归属")]
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

    [Theory]
    [InlineData("Example")]
    [InlineData("SystemDisguised")]
    public void 不会忽略嵌套敏感字段_也不能靠命名空间绕过(string ns)
    {
        var source = Source.Replace("namespace Example;", "namespace " + ns + ";", StringComparison.Ordinal)
            .Replace("public sealed partial record Request(Guid IdempotencyKey, string Name);", "public sealed partial record Request(Guid IdempotencyKey, SecretChild Child); public sealed record SecretChild([property: Sensitive] string Secret);", StringComparison.Ordinal);
        var result = Generate(source); Assert.Equal("QIANSDK001", Assert.Single(result.Diagnostics).Id); Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void 顶层容器明确敏感时允许整个嵌套结构统一遮盖()
    {
        var source = Source.Replace("public sealed partial record Request(Guid IdempotencyKey, string Name);", "public sealed partial record Request(Guid IdempotencyKey, [property: Sensitive] SecretChild Child); public sealed record SecretChild([property: Sensitive] string Secret);", StringComparison.Ordinal);
        var result = Generate(source); Assert.Empty(result.Diagnostics); Assert.Single(result.GeneratedTrees);
    }

    [Theory]
    [InlineData("qian.example", "qian.example", true)]
    [InlineData("qian.example", "qian.missing", false)]
    [InlineData("INVALID", "INVALID", false)]
    public void 能力归属必须指向合法且唯一的入口(string entryId, string owner, bool valid)
    {
        var source = Source.Replace("[QianPlugin(\"qian.example\")]", "[QianPlugin(\"" + entryId + "\")]", StringComparison.Ordinal)
            .Replace("Description =", "ComponentId = \"" + owner + "\", Description =", StringComparison.Ordinal);
        var result = Generate(source);
        if (valid) Assert.Empty(result.Diagnostics); else Assert.Contains(result.Diagnostics, d => d.Id == "QIANSDK001");
    }

    [Fact]
    public void 多组件的能力必须明确归属_不凭类型所在程序集猜测()
    {
        var source = Source + """
            [QianPlugin("qian.second")] public sealed class Second : IQianPlugin {
                public IReadOnlyList<ServiceKey> Inject => [];
                public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        Assert.Equal("QIANSDK001", Assert.Single(Generate(source).Diagnostics).Id);
        var explicitOwner = Generate(source.Replace("Description =", "ComponentId = \"qian.example\", Description =", StringComparison.Ordinal));
        Assert.Empty(explicitOwner.Diagnostics); Assert.Single(explicitOwner.GeneratedTrees);
        var duplicateId = source.Replace("qian.second", "qian.example", StringComparison.Ordinal);
        Assert.Equal("QIANSDK001", Assert.Single(Generate(duplicateId).Diagnostics).Id);
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
