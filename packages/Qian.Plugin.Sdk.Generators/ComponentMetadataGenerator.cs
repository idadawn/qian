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
public sealed partial class ComponentMetadataGenerator : IIncrementalGenerator
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
        foreach (var request in requests)
            if (CapabilityOwner(request, plugins) is not { } owner || plugins.Count(plugin => Id(plugin) == owner) != 1)
            { Report(output, request, "能力必须唯一归属一个 QianPlugin；多组件程序集必须声明 ComponentId。"); return; }
        foreach (var group in plugins.GroupBy(Id, StringComparer.Ordinal))
        {
            if (group.Count() != 1) { foreach (var plugin in group) Report(output, plugin, "组件 ID 重复；每个 ID 必须只有一个入口。"); continue; }
            GeneratePlugin(output, group.Single(), requests, plugins, compilation);
        }
    }
    private static string Id(INamedTypeSymbol type) => type.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianPluginAttribute").ConstructorArguments[0].Value as string ?? "";
    private static void GeneratePlugin(SourceProductionContext output, INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> requests, ImmutableArray<INamedTypeSymbol> plugins, Compilation compilation)
    {
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
        var delegateType = type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.DelegatePluginAttribute")?.ConstructorArguments[0].Value as INamedTypeSymbol;
        var contributions = new List<(string Key, string Name, INamedTypeSymbol? Registry)>();
        foreach (var declaration in type.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() is "Qian.Plugin.Sdk.ProvidesAttribute" or "Qian.Plugin.Sdk.ContributesAttribute"))
        {
            var key = DeclaredKey(declaration);
            if (key is null) { Report(output, type, "独立服务声明须引用公开静态只读 ServiceKey 字段。"); return; }
            if (declaration.AttributeClass!.Name == "ProvidesAttribute")
            {
                if (delegateType is null) { Report(output, type, "Provides 仅用于明确的 DelegatePlugin 入口，不能覆盖直接 Apply 分析。"); return; }
                provides.Add(key + ".Name");
            }
            else
            {
                var name = declaration.ConstructorArguments[2].Value as string;
                if (name is null || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                { Report(output, type, "Contributes 须声明固定贡献名。"); return; }
                contributions.Add((key, name, declaration.NamedArguments.FirstOrDefault(p => p.Key == "RegistryType").Value.Value as INamedTypeSymbol));
                if (delegateType is not null) provides.Add(key + ".Name + " + Literal(":" + name));
            }
        }
        if (provides.Distinct(StringComparer.Ordinal).Count() != provides.Count || contributions.Select(c => c.Key + ":" + c.Name).Distinct(StringComparer.Ordinal).Count() != contributions.Count)
        { Report(output, type, "独立提供项或贡献声明不能重复。"); return; }
        if (delegateType is not null && (!delegateType.AllInterfaces.Any(i => i.ToDisplayString() == "Qian.Runtime.IQianPlugin") || delegateType.IsAbstract || provides.Count == 0))
        { Report(output, type, "DelegatePlugin 必须指定具体 IQianPlugin 实现，并独立声明完整的非空输出。"); return; }
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
            if (model.GetOperation(identifier) is not IParameterReferenceOperation contextReference || !SymbolEqualityComparer.Default.Equals(contextReference.Parameter, methods[0].Parameters[0])) continue;
            IOperation use = contextReference;
            while (use.Parent is IConversionOperation conversion) use = conversion;
            if (use.Parent is IPropertyReferenceOperation property && ReferenceEquals(property.Instance, use)) continue;
            if (use.Parent is IInvocationOperation member && ReferenceEquals(member.Instance, use)) continue;
            if (use.Parent is IArgumentOperation argument && argument.Parent is IInvocationOperation guard &&
                (guard.TargetMethod.ContainingType.ToDisplayString() == "System.ArgumentNullException" && guard.TargetMethod.Name == "ThrowIfNull" ||
                 guard.TargetMethod.ContainingAssembly.Name == "Qian.Plugin.Sdk" && guard.TargetMethod.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CommandPolicies" && guard.TargetMethod.Name == "Register" ||
                 delegateType is not null && SymbolEqualityComparer.Default.Equals(guard.TargetMethod.ContainingType, delegateType) && guard.TargetMethod.Name == "ApplyAsync")) continue;
            if (use.Parent is IArgumentOperation capabilityArgument && capabilityArgument.Parent is IInvocationOperation capability &&
                capability.TargetMethod.ContainingAssembly.Name == "Qian.Plugin.Sdk" && capability.TargetMethod.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CapabilityBindingRegistration" && capability.TargetMethod.Name == "ProvideCapability") continue;
            Report(output, type, "Apply 上下文不能逃逸到辅助方法或局部别名；否则无法证明提供的服务与代码一致。"); return;
        }
        var usedContributions = new HashSet<string>(StringComparer.Ordinal); var delegated = 0;
        var registrationKinds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var syntax in apply.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetOperation(syntax) is not IInvocationOperation invocation) continue;
            var method = invocation.TargetMethod;
            if (delegateType is not null && SymbolEqualityComparer.Default.Equals(method.ContainingType, delegateType) && method.Name == "ApplyAsync")
            {
                if (Conditional(syntax, apply) || invocation.Arguments.Length != 3 || Unwrap(invocation.Arguments[0].Value) is not IParameterReferenceOperation reference || !SymbolEqualityComparer.Default.Equals(reference.Parameter, methods[0].Parameters[0]) || invocation.Arguments.Skip(1).Any(a => Unwrap(a.Value) is not IParameterReferenceOperation p || !SymbolEqualityComparer.Default.Equals(p.Parameter, methods[0].Parameters[a.Parameter!.Ordinal])) ||
                    !SymbolEqualityComparer.Default.Equals(delegateType.FindImplementationForInterfaceMember(delegateType.AllInterfaces.Single(i => i.ToDisplayString() == "Qian.Runtime.IQianPlugin").GetMembers("ApplyAsync").Single()), method))
                { Report(output, type, "委托必须无条件且只传递当前 Apply 上下文。"); return; }
                delegated++; continue;
            }
            string? provided = null; var registrationKind = 1;
            if (method.Name == "Provide" && method.ContainingType.ToDisplayString() == "Qian.Runtime.IPluginContext")
            {
                if (delegateType is not null || invocation.Instance is not IParameterReferenceOperation parameter || parameter.Parameter.Ordinal != 0 || Conditional(syntax, apply))
                { Report(output, type, "Provide 必须在当前 Apply 中直接、无条件注册；委托入口不能混入另一组提供项。"); return; }
                var key = Key(invocation.Arguments[0].Value);
                if (key is null) { Report(output, type, "Provide 的键必须是静态只读 ServiceKey 字段或常量构造。"); return; }
                provided = key + ".Name";
                if (invocation.Arguments.Length == 3)
                {
                    var name = invocation.Arguments[1].Value.ConstantValue;
                    if (!name.HasValue || name.Value is not string text) { Report(output, type, "具名 Provide 的名称必须是常量，动态名称不能静默丢弃。"); return; }
                    provided += " + " + Literal(":" + text);
                    usedContributions.Add(key + ":" + text);
                }
            }
            else if (method.Name == "Register" && method.ContainingAssembly.Name == "Qian.Plugin.Sdk" && method.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CommandPolicies")
            {
                if (delegateType is not null || Conditional(syntax, apply) || invocation.Arguments[1].Value.ConstantValue.Value is not int kind || kind < 0 || kind > 2)
                { Report(output, type, "CommandPolicies.Register 必须使用直接、无条件的固定策略种类。"); return; }
                var owner = new[] { "qian.tenancy", "qian.authz", "qian.audit" }[kind];
                if (id != owner) { Report(output, type, "命令策略只能属于对应的平台组件。"); return; }
                provided = "global::Qian.Plugin.Sdk.CommandPolicies." + new[] { "TenancyKey", "AuthorizationKey", "AuditKey" }[kind] + ".Name";
            }
            else if (method.Name == "Register" && invocation.Arguments.Length >= 2 && invocation.Arguments[0].Parameter?.Type.SpecialType == SpecialType.System_String)
            {
                var name = invocation.Arguments[0].Value.ConstantValue;
                var matches = contributions.Where(c => name.HasValue && name.Value is string text && text == c.Name && c.Registry is not null &&
                    (SymbolEqualityComparer.Default.Equals(method.ContainingType, c.Registry) || SymbolEqualityComparer.Default.Equals(invocation.Instance?.Type, c.Registry))).ToArray();
                if (matches.Length == 0) { Report(output, type, "Register 的名称或注册表无法匹配独立 Contributes 声明；动态空结果不允许。"); return; }
                if (matches.Length != 1 || Conditional(syntax, apply)) { Report(output, type, "注册表 Register 须唯一匹配一个固定 Contributes 声明，并在 Apply 中直接登记。"); return; }
                registrationKind = 2;
                provided = matches[0].Key + ".Name + " + Literal(":" + matches[0].Name); usedContributions.Add(matches[0].Key + ":" + matches[0].Name);
            }
            if (provided is not null)
            {
                if (registrationKinds.TryGetValue(provided, out var existingKind) && (existingKind & registrationKind) != 0)
                { Report(output, type, "同一入口不能重复提供同一键。"); return; }
                registrationKinds[provided] = existingKind | registrationKind;
                if (!provides.Contains(provided)) provides.Add(provided);
            }
        }
        if (delegateType is not null && delegated != 1) { Report(output, type, "DelegatePlugin 入口须恰好调用一次所声明实现的 ApplyAsync。"); return; }
        if (delegateType is null && contributions.Any(c => !usedContributions.Contains(c.Key + ":" + c.Name)))
        { Report(output, type, "Contributes 声明没有对应的具名 Provide 或 Register 调用。"); return; }
        var declarations = Declarations(output, type, plugins, compilation);
        if (declarations is null) return;
        var capabilityExpressions = requests.Where(r => CapabilityOwner(r, plugins) == id).OrderBy(r => r.ToDisplayString(), StringComparer.Ordinal)
            .Select(r => r.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".QianCapability").ToList();
        foreach (var attribute in type.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.CapabilitySourceAttribute"))
        {
            var owner = attribute.ConstructorArguments[0].Value as INamedTypeSymbol; var memberName = attribute.ConstructorArguments[1].Value as string;
            var member = owner?.GetMembers(memberName ?? "").SingleOrDefault();
            var memberType = member is IFieldSymbol field ? field.Type : (member as IPropertySymbol)?.Type;
            if (member is null || !member.IsStatic || member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal) ||
                memberType?.ToDisplayString() != "Qian.Plugin.Sdk.CapabilityContract" || member is IFieldSymbol { IsReadOnly: false } || member is IPropertySymbol { SetMethod: not null })
            { Report(output, type, "CapabilitySource 必须引用同程序集可访问的静态只读 CapabilityContract 声明。"); return; }
            var capabilityExpression = owner!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + member.Name;
            if (capabilityExpressions.Contains(capabilityExpression)) { Report(output, type, "同一能力来源不能重复登记。"); return; }
            capabilityExpressions.Add(capabilityExpression);
        }
        var jobRequests = requests.Where(r => CapabilityOwner(r, plugins) == id && r.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianJobAttribute")).ToArray();
        var jobCodes = jobRequests.Select(r => r.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianJobAttribute").ConstructorArguments[0].Value as string).ToArray();
        if (jobCodes.Distinct(StringComparer.Ordinal).Count() != jobCodes.Length) { Report(output, type, "组件内 QianJob code 重复。"); return; }
        var jobs = jobRequests.Select(r => "new global::Qian.Plugin.Sdk.JobContract(" + Literal(r.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianJobAttribute").ConstructorArguments[0].Value as string ?? "") + ", " + r.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".QianCapability)");
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
            "        [" + string.Join(", ", capabilityExpressions) + "],\n        " + declarations.Value.Permissions + ",\n        " + declarations.Value.Events + ",\n        " + declarations.Value.Config + ", [" + string.Join(", ", jobs) + "]);\n}\n";
        output.AddSource(type.ToDisplayString().Replace('.', '_') + ".g.cs", SourceText.From(source, Encoding.UTF8));
    }
    private static IOperation? Unwrap(IOperation? value) { while (value is IConversionOperation conversion) value = conversion.Operand; return value; }
    private static bool Conditional(SyntaxNode syntax, SyntaxNode apply) =>
        syntax.Ancestors().TakeWhile(a => a != apply).Any(a => a is IfStatementSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or ConditionalExpressionSyntax or ConditionalAccessExpressionSyntax or TryStatementSyntax ||
            a is BinaryExpressionSyntax binary && (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression))) ||
        apply.DescendantNodes().Any(node => node is GotoStatementSyntax || node is ReturnStatementSyntax && node.SpanStart < syntax.SpanStart && !node.Span.Contains(syntax.Span) &&
            !node.Ancestors().TakeWhile(parent => parent != apply).Any(parent => parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax));
    private static string? DeclaredKey(AttributeData attribute)
    {
        var owner = attribute.ConstructorArguments[0].Value as INamedTypeSymbol; var name = attribute.ConstructorArguments[1].Value as string;
        var field = owner?.GetMembers(name ?? "").OfType<IFieldSymbol>().SingleOrDefault();
        return field is { IsStatic: true, IsReadOnly: true, DeclaredAccessibility: Accessibility.Public } && field.Type.ToDisplayString() == "Qian.Runtime.ServiceKey"
            ? owner!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + field.Name : null;
    }
    private static string? CapabilityOwner(INamedTypeSymbol request, ImmutableArray<INamedTypeSymbol> plugins)
    {
        var attribute = request.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.CapabilityAttribute");
        return attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "ComponentId").Value.Value as string ?? (plugins.Length == 1 ? Id(plugins[0]) : null);
    }
    private static bool IsCapabilityCall(SemanticModel model, IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is not MemberAccessExpressionSyntax member || member.Expression != identifier || member.Parent is not InvocationExpressionSyntax call) return false;
        var info = model.GetSymbolInfo(call);
        var method = info.Symbol as IMethodSymbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] as IMethodSymbol : null);
        return method?.ContainingAssembly.Name == "Qian.Plugin.Sdk" && method.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.JobBindingRegistration" && method.Name == "ProvideJob" || method?.ContainingAssembly.Name == "Qian.Plugin.Sdk" && method.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.CapabilityBindingRegistration" && method.Name == "ProvideCapability" || method?.ContainingAssembly.Name == "Qian.Plugin.Sdk" && method.ContainingType.ToDisplayString() == "Qian.Plugin.Sdk.HookCapabilityRegistration" && method.Name == "ExposeCapabilityHook";
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
