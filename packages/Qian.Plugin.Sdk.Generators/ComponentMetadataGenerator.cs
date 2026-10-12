using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Qian.Plugin.Sdk.Generators;

/// <summary>静态分析组件 Inject 与 Apply 的服务注册，生成唯一清单的构建期输入。</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ComponentMetadataGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Invalid = new("QIANSDK002", "组件注册不能静态派生", "{0}", "QianSdk", DiagnosticSeverity.Error, true);
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var plugins = context.SyntaxProvider.ForAttributeWithMetadataName("Qian.Plugin.Sdk.QianPluginAttribute", static (_, _) => true,
            static (syntax, _) => (INamedTypeSymbol)syntax.TargetSymbol).Collect();
        var requests = context.SyntaxProvider.ForAttributeWithMetadataName("Qian.Plugin.Sdk.CapabilityAttribute", static (_, _) => true,
            static (syntax, _) => (INamedTypeSymbol)syntax.TargetSymbol).Collect();
        context.RegisterSourceOutput(plugins.Combine(requests).Combine(context.CompilationProvider), static (output, input) => Generate(output, input.Left.Left, input.Left.Right, input.Right));
    }
    private static void Generate(SourceProductionContext output, ImmutableArray<INamedTypeSymbol> plugins, ImmutableArray<INamedTypeSymbol> requests, Compilation compilation)
    {
        if (plugins.Length == 0) return;
        if (plugins.Length != 1) { foreach (var plugin in plugins) Report(output, plugin, "一个组件程序集只能有一个 QianPlugin。"); return; }
        var type = plugins[0];
        if (!type.AllInterfaces.Any(i => i.ToDisplayString() == "Qian.Runtime.IQianPlugin") || type.IsAbstract || type.IsGenericType || type.ContainingType is not null)
        { Report(output, type, "QianPlugin 必须是顶层、非泛型、可实例化的 IQianPlugin 实现。"); return; }
        var id = type.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianPluginAttribute").ConstructorArguments[0].Value as string;
        if (id is null || id.Length > 128 || !System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        { Report(output, type, "组件 ID 必须符合冻结清单格式。"); return; }
        var injectProperty = type.GetMembers("Inject").OfType<IPropertySymbol>().SingleOrDefault();
        if (injectProperty?.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not PropertyDeclarationSyntax injectDeclaration)
        { Report(output, type, "组件须在本类型直接声明可静态分析的 Inject 属性。"); return; }
        var expression = injectDeclaration.ExpressionBody?.Expression ?? injectDeclaration.AccessorList?.Accessors.SingleOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration))?.ExpressionBody?.Expression;
        if (expression is not CollectionExpressionSyntax collection)
        { Report(output, type, "Inject 目前须使用服务键集合表达式，不能调用动态代码。"); return; }
        var inject = new List<string>(); var provides = new List<string>();
        var model = compilation.GetSemanticModel(injectDeclaration.SyntaxTree);
        foreach (var element in collection.Elements)
        {
            var key = element is ExpressionElementSyntax item ? Key(model.GetOperation(item.Expression)) : null;
            if (key is null) { Report(output, type, "Inject 项必须是静态只读 ServiceKey 字段或常量构造。"); return; }
            inject.Add(key);
        }
        var methods = type.GetMembers("ApplyAsync").OfType<IMethodSymbol>().Where(m => m.DeclaringSyntaxReferences.Length > 0).ToArray();
        if (methods.Length != 1) { Report(output, type, "组件须直接实现一个 ApplyAsync，不能隐藏实际服务注册。"); return; }
        var apply = methods[0].DeclaringSyntaxReferences[0].GetSyntax(); model = compilation.GetSemanticModel(apply.SyntaxTree);
        foreach (var identifier in apply.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            // 同轮生成的请求接口尚未加入当前语义模型，约束未满足时从已绑定候选方法核验 SDK 所有权。
            if (IsCapabilityCall(model, identifier)) continue;
            if (model.GetOperation(identifier) is not IParameterReferenceOperation contextReference || contextReference.Parameter.Ordinal != 0) continue;
            IOperation use = contextReference;
            while (use.Parent is IConversionOperation conversion) use = conversion;
            if (use.Parent is IPropertyReferenceOperation property && ReferenceEquals(property.Instance, use)) continue;
            if (use.Parent is IInvocationOperation member && ReferenceEquals(member.Instance, use)) continue;
            if (use.Parent is IArgumentOperation argument && argument.Parent is IInvocationOperation guard && guard.TargetMethod.ContainingType.ToDisplayString() == "System.ArgumentNullException" && guard.TargetMethod.Name == "ThrowIfNull") continue;
            if (use.Parent is IArgumentOperation capabilityArgument && capabilityArgument.Parent is IInvocationOperation capability &&
                capability.TargetMethod.ContainingAssembly.Name == "Qian.Plugin.Sdk" && capability.TargetMethod.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CapabilityBindingRegistration" && capability.TargetMethod.Name == "ProvideCapability") continue;
            Report(output, type, "Apply 上下文不能逃逸到辅助方法或局部别名；否则无法证明提供的服务与代码一致。"); return;
        }
        foreach (var syntax in apply.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetOperation(syntax) is not IInvocationOperation invocation || invocation.TargetMethod.Name != "Provide" || invocation.TargetMethod.ContainingType.ToDisplayString() != "Qian.Runtime.IPluginContext") continue;
            if (invocation.Arguments.Length != 2 || invocation.Arguments[0].Parameter?.Name != "key")
            { Report(output, type, "具名 seam 注册须由独立声明支持，不能静默丢弃其提供项。"); return; }
            var key = Key(invocation.Arguments[0].Value);
            if (key is null) { Report(output, type, "Provide 的键必须是静态只读 ServiceKey 字段或常量构造。"); return; }
            if (invocation.Instance is not IParameterReferenceOperation parameter || parameter.Parameter.Ordinal != 0 ||
                syntax.Ancestors().TakeWhile(a => a != apply).Any(a => a is IfStatementSyntax or SwitchStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or ConditionalExpressionSyntax) || provides.Contains(key))
            { Report(output, type, "Provide 必须在当前 Apply 上下文中直接注册，不能条件注册、延迟注册或重复声明同一键。"); return; }
            provides.Add(key);
        }
        var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
        var factory = type.Name + "QianCodeMetadata";
        if (type.ContainingNamespace.GetTypeMembers(factory).Length != 0) { Report(output, type, "生成元数据工厂名称已被占用。"); return; }
        var qualifiedFactory = "global::" + (ns.Length == 0 ? "" : ns + ".") + factory;
        var entry = type.ToDisplayString() + ", " + compilation.AssemblyName;
        var source = "// <auto-generated/>\n#nullable enable\n[assembly: global::Qian.Plugin.Sdk.QianCodeMetadataAttribute(typeof(" + qualifiedFactory + "))]\n" +
            (ns.Length == 0 ? "" : "namespace " + ns + ";\n") + "internal sealed class " + factory + " : global::Qian.Plugin.Sdk.IComponentCodeMetadataFactory\n{\n" +
            "    public " + factory + "() { }\n" +
            "    public global::Qian.Plugin.Sdk.ComponentCodeMetadata Create() => new(\n" +
            "        " + Literal(id ?? "") + ", " + Literal(entry) + ",\n" +
            "        [" + string.Join(", ", inject) + "],\n        [" + string.Join(", ", provides.Distinct(StringComparer.Ordinal)) + "],\n" +
            "        [" + string.Join(", ", requests.OrderBy(r => r.ToDisplayString(), StringComparer.Ordinal).Select(r => r.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".QianCapability")) + "]);\n}\n";
        output.AddSource(factory + ".g.cs", SourceText.From(source, Encoding.UTF8));
    }
    private static bool IsCapabilityCall(SemanticModel model, IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is not MemberAccessExpressionSyntax member || member.Expression != identifier || member.Parent is not InvocationExpressionSyntax call) return false;
        var info = model.GetSymbolInfo(call);
        var method = info.Symbol as IMethodSymbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] as IMethodSymbol : null);
        return method?.ContainingAssembly.Name == "Qian.Plugin.Sdk" && method.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CapabilityBindingRegistration" && method.Name == "ProvideCapability";
    }

    private static string? Key(IOperation? operation)
    {
        while (operation is IConversionOperation conversion) operation = conversion.Operand;
        if (operation is IFieldReferenceOperation field && field.Field.IsStatic && field.Field.IsReadOnly && field.Field.DeclaredAccessibility == Accessibility.Public && field.Field.Type.ToDisplayString() == "Qian.Runtime.ServiceKey")
            return field.Field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + field.Field.Name;
        if (operation is IObjectCreationOperation creation && creation.Type?.ToDisplayString() == "Qian.Runtime.ServiceKey" && creation.Arguments.Length == 1 && creation.Arguments[0].Value.ConstantValue.Value is string value)
            return "new global::Qian.Runtime.ServiceKey(" + Literal(value) + ")";
        return null;
    }
    private static string Literal(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, true);
    private static void Report(SourceProductionContext output, ISymbol symbol, string message) => output.ReportDiagnostic(Diagnostic.Create(Invalid, symbol.Locations.FirstOrDefault(), message));
}
