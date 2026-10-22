using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Qian.Plugin.Sdk.Generators;

/// <summary>从能力属性生成强类型契约；生成器不执行用户代码或写清单文件。</summary>
[Generator(LanguageNames.CSharp)]
public sealed class CapabilityGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Invalid = new("QIANSDK001", "组件或能力声明不完整", "{0}", "QianSdk", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var plugins = context.SyntaxProvider.ForAttributeWithMetadataName("Qian.Plugin.Sdk.QianPluginAttribute",
            static (_, _) => true, static (syntax, _) => (INamedTypeSymbol)syntax.TargetSymbol).Collect();
        var capabilities = context.SyntaxProvider.ForAttributeWithMetadataName("Qian.Plugin.Sdk.CapabilityAttribute",
            static (_, _) => true, static (syntax, _) => (INamedTypeSymbol)syntax.TargetSymbol).Collect();
        context.RegisterSourceOutput(plugins.Combine(capabilities), static (output, input) => Generate(output, input.Left, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<INamedTypeSymbol> plugins, ImmutableArray<INamedTypeSymbol> capabilities)
    {
        if (capabilities.Length == 0) return;
        var owners = plugins.GroupBy(p => Attribute(p, "QianPluginAttribute").ConstructorArguments[0].Value as string ?? "", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var resolved = new List<(INamedTypeSymbol Type, string Component)>();
        foreach (var capability in capabilities)
        {
            var attribute = Attribute(capability, "CapabilityAttribute");
            var owner = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "ComponentId").Value.Value as string;
            owner ??= plugins.Length == 1 ? owners.Keys.Single() : null;
            if (owner is null || !owners.TryGetValue(owner, out var matches) || matches.Length != 1)
            { Report(output, capability, "能力须唯一归属本程序集的 QianPlugin；多组件程序集必须声明 ComponentId。"); continue; }
            var plugin = matches[0];
            if (!Matches(owner, @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$") || owner.Length > 128 || plugin.IsAbstract || plugin.IsGenericType || plugin.ContainingType is not null || !plugin.AllInterfaces.Any(i => i.ToDisplayString() == "Qian.Runtime.IQianPlugin"))
            { Report(output, capability, "QianPlugin 必须具有合法组件 ID，并实现 IQianPlugin。"); continue; }
            resolved.Add((capability, owner));
        }
        foreach (var group in resolved.GroupBy(item => item.Component + "/" + Attribute(item.Type, "CapabilityAttribute").ConstructorArguments[0].Value, StringComparer.Ordinal))
        {
            if (group.Count() > 1) { foreach (var duplicate in group) Report(output, duplicate.Type, "组件内能力短名重复。"); continue; }
            var item = group.Single(); GenerateCapability(output, item.Component, item.Type);
        }
    }

    private static void GenerateCapability(SourceProductionContext output, string component, INamedTypeSymbol request)
    {
        var syntax = request.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>().ToArray();
        if (request.ContainingType is not null || request.TypeParameters.Length != 0 || syntax.Any(s => !s.Modifiers.Any(SyntaxKind.PartialKeyword)))
        { Report(output, request, "能力请求必须是顶层非泛型 partial 类型。"); return; }
        if (request.GetMembers("QianCapability").Length != 0 || request.GetMembers("RequiredPermissions").Length != 0)
        { Report(output, request, "QianCapability 与 RequiredPermissions 名称保留给生成契约。"); return; }
        var attribute = Attribute(request, "CapabilityAttribute"); var args = attribute.ConstructorArguments;
        var name = args[0].Value as string ?? "";
        if (args[1].Value is not INamedTypeSymbol response || args[2].Value is not INamedTypeSymbol serializer)
        { Report(output, request, "响应与序列化上下文必须是封闭类型。"); return; }
        var description = Text(attribute, "Description", ""); var effect = Text(attribute, "SideEffect", "read");
        var policy = Text(attribute, "PolicyHint", effect == "destructive" ? "confirm" : "allow");
        var version = Text(attribute, "ContractVersion", "v1"); var classification = Text(attribute, "DataClassification", "internal");
        var cost = Text(attribute, "CostTier", "low"); var asyncKind = Text(attribute, "Async", "none");
        var permission = Text(attribute, "RequiredPermission", ""); var idempotent = Boolean(attribute, "Idempotent", false);
        var scopes = Strings(attribute, "RequiredScopes"); var secrets = Strings(attribute, "RequiredSecrets");
        if (!Matches(name, @"^[a-z][a-z0-9_-]*(\.[a-z][a-z0-9_-]*)*$") || name.Length > 100 || description.Length < 10 || description.Length > 2000 ||
            effect is not ("read" or "write" or "external" or "destructive") || policy is not ("allow" or "confirm" or "deny") ||
            classification is not ("public" or "internal" or "pii" or "finance" or "secret") || cost is not ("low" or "medium" or "high") ||
            asyncKind is not ("none" or "task") || version.Length > 64 || !Matches(version, @"^[a-z][a-z0-9.-]*$") ||
            (permission.Length != 0 && !Matches(permission, @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+:[a-z][a-z0-9_-]*:[a-z][a-z0-9_-]*$")) ||
            scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Length || scopes.Any(s => s.Length > 200 || !Matches(s, @"^[\x21\x23-\x5B\x5D-\x7E]+$")) ||
            secrets.Distinct(StringComparer.Ordinal).Count() != secrets.Length || secrets.Any(s => s.Length > 128 || !Matches(s, @"^[A-Za-z][A-Za-z0-9_.-]*$")) ||
            (effect != "read" && !idempotent))
        { Report(output, request, "能力声明不符合冻结契约；写操作必须声明幂等性，描述、权限与 Agent 字段必须合法。"); return; }
        if (!DerivesFrom(serializer, "System.Text.Json.Serialization.JsonSerializerContext") || serializer.IsUnboundGenericType || response.IsUnboundGenericType)
        { Report(output, request, "能力必须使用 System.Text.Json 源生成上下文和封闭响应类型。"); return; }
        var serializable = serializer.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonSerializableAttribute")
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol).ToArray();
        if (!serializable.Any(t => SymbolEqualityComparer.Default.Equals(t, request)) || !serializable.Any(t => SymbolEqualityComparer.Default.Equals(t, response)))
        { Report(output, request, "源生成序列化上下文必须显式登记能力的请求与响应类型。"); return; }
        var sensitive = SensitiveProperties(request);
        if (sensitive is null) { Report(output, request, "嵌套敏感属性须将其顶层输入容器标记 Sensitive；不能忽略未派生的敏感字段。"); return; }
        var audit = Boolean(attribute, "Audit", effect != "read");
        var fields = new[] {
            "\"name\":" + Quote(name), "\"description\":" + Quote(description), "\"sideEffect\":" + Quote(effect), "\"policyHint\":" + Quote(policy),
            "\"idempotent\":" + Bool(idempotent), "\"async\":" + Quote(asyncKind), "\"requiredScopes\":" + Array(scopes), "\"requiredSecrets\":" + Array(secrets),
            "\"dataClassification\":" + Quote(classification), "\"costTier\":" + Quote(cost), "\"contractVersion\":" + Quote(version), "\"audit\":" + Bool(audit),
            "\"agent\":{\"expose\":" + Bool(Boolean(attribute, "AgentExpose", false)) + ",\"requiresConfirmation\":" + Bool(Boolean(attribute, "RequiresConfirmation", false)) + "}",
            "\"binding\":{\"kind\":\"command\",\"id\":" + Quote(component + "/" + name) + "}" };
        var metadata = "{" + string.Join(",", fields) + (permission.Length == 0 ? "" : ",\"requiredPermission\":" + Quote(permission)) + "}";
        var kind = request.IsRecord ? (request.TypeKind == TypeKind.Struct ? "record struct" : "record") : (request.TypeKind == TypeKind.Struct ? "struct" : "class");
        var ns = request.ContainingNamespace.IsGlobalNamespace ? "" : "namespace " + request.ContainingNamespace.ToDisplayString() + ";\n";
        var requestName = request.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); var responseName = response.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var contextName = serializer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sensitiveNames = sensitive.Select(property =>
        {
            var alias = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute");
            if (alias?.ConstructorArguments[0].Value is string jsonName) return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(jsonName, true);
            var clrName = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(property.Name, true);
            return "(" + contextName + ".Default.Options.PropertyNamingPolicy?.ConvertName(" + clrName + ") ?? " + clrName + ")";
        });
        var source = "// <auto-generated/>\n#nullable enable\n" + ns + "partial " + kind + " @" + request.Name + " : global::Qian.Plugin.Sdk.ICapabilityRequest<" + responseName + ">\n{\n" +
            "    public static global::System.Collections.Generic.IReadOnlyList<string> RequiredPermissions { get; } = global::System.Array.AsReadOnly<string>([" + (permission.Length == 0 ? "" : Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(permission, true)) + "]);\n" +
            "    public static global::Qian.Plugin.Sdk.CapabilityContract QianCapability { get; } = new(\n" +
            "        " + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(component, true) + ",\n" +
            "        " + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(metadata, true) + ",\n" +
            "        " + contextName + ".Default.GetTypeInfo(typeof(" + requestName + "))!,\n" +
            "        " + contextName + ".Default.GetTypeInfo(typeof(" + responseName + "))!, [" + string.Join(", ", sensitiveNames) + "]);\n}\n";
        output.AddSource(request.ToDisplayString().Replace('.', '_') + ".QianCapability.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static List<IPropertySymbol>? SensitiveProperties(INamedTypeSymbol request)
    {
        var marked = new List<IPropertySymbol>(); var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default); var budget = 256;
        bool Scan(ITypeSymbol type, bool root)
        {
            if (--budget < 0) return false;
            if (!root && (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum || !visited.Add(type))) return true;
            if (type is IArrayTypeSymbol array) return Scan(array.ElementType, false);
            if (type is not INamedTypeSymbol named) return true;
            if (!named.TypeArguments.All(argument => Scan(argument, false))) return false;
            for (var owner = named; owner is not null && owner.SpecialType != SpecialType.System_Object; owner = owner.BaseType)
                foreach (var property in owner.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic && !p.IsIndexer && (p.DeclaredAccessibility == Accessibility.Public || p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIncludeAttribute"))))
                {
                    var ignored = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute");
                    if (ignored is not null && (!ignored.NamedArguments.Any(p => p.Key == "Condition") || ignored.NamedArguments.First(p => p.Key == "Condition").Value.Value is 1)) continue;
                    var sensitive = false;
                    for (var current = property; current is not null; current = current.OverriddenProperty)
                        sensitive |= current.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.SensitiveAttribute");
                    if (sensitive)
                    {
                        if (!root) return false;
                        marked.Add(property); // 整个顶层对象/数组都会遮盖，其子字段不再需要单独派生。
                    }
                    else if (!Scan(property.Type, false)) return false;
                }
            return true;
        }
        return Scan(request, true) ? marked : null;
    }

    private static bool DerivesFrom(INamedTypeSymbol symbol, string name)
    {
        for (var current = symbol.BaseType; current is not null; current = current.BaseType) if (current.ToDisplayString() == name) return true;
        return false;
    }
    private static AttributeData Attribute(INamedTypeSymbol symbol, string name) => symbol.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk." + name);
    private static string Text(AttributeData data, string name, string fallback) => data.NamedArguments.FirstOrDefault(p => p.Key == name).Value.Value as string ?? fallback;
    private static bool Boolean(AttributeData data, string name, bool fallback) => data.NamedArguments.FirstOrDefault(p => p.Key == name).Value.Value is bool value ? value : fallback;
    private static string[] Strings(AttributeData data, string name)
    {
        var item = data.NamedArguments.FirstOrDefault(p => p.Key == name).Value;
        return item.Kind == TypedConstantKind.Array && !item.IsNull ? item.Values.Select(v => v.Value as string ?? "").ToArray() : [];
    }
    private static bool Matches(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static void Report(SourceProductionContext output, ISymbol symbol, string message) => output.ReportDiagnostic(Diagnostic.Create(Invalid, symbol.Locations.FirstOrDefault(), message));
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Array(string[] values) => "[" + string.Join(",", values.Select(Quote)) + "]";
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c == '"' || c == '\\') result.Append('\\').Append(c);
            else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else result.Append(c);
        }
        return result.Append('"').ToString();
    }
}
