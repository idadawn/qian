using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Qian.Plugin.Sdk.Generators;

public sealed partial class ComponentMetadataGenerator
{
    private static (string Permissions, string Events, string Config)? Declarations(SourceProductionContext output, INamedTypeSymbol plugin,
        ImmutableArray<INamedTypeSymbol> plugins, Compilation compilation)
    {
        var symbols = new List<ISymbol>();
        void Visit(INamespaceOrTypeSymbol container)
        {
            foreach (var member in container.GetMembers())
            {
                if (member is INamespaceOrTypeSymbol nested) Visit(nested);
                symbols.Add(member);
            }
        }
        Visit(compilation.Assembly.GlobalNamespace);
        var declaration = plugin.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianPluginAttribute");
        var included = declaration.NamedArguments.FirstOrDefault(p => p.Key == "MetadataTypes").Value;
        var explicitTypes = included.IsNull || included.Kind != TypedConstantKind.Array ? [] : included.Values.Select(v => v.Value).OfType<INamedTypeSymbol>().ToArray();
        foreach (var type in explicitTypes)
        {
            if (plugins.Where(p => !SymbolEqualityComparer.Default.Equals(p, plugin)).Any(p => p.GetAttributes()
                .Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianPluginAttribute").NamedArguments
                .Any(pair => pair.Key == "MetadataTypes" && pair.Value.Values.Any(value => SymbolEqualityComparer.Default.Equals(value.Value as INamedTypeSymbol, type)))))
            { Report(output, type, "同一 MetadataTypes 声明不能归属多个组件。"); return null; }
            symbols.Add(type); symbols.AddRange(type.GetMembers());
        }
        var permissions = new List<string>(); var events = new List<string>(); var permissionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols.Distinct(SymbolEqualityComparer.Default).OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal))
        {
            foreach (var attribute in symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() is "Qian.Authz.Abstractions.PermissionAttribute" or "Qian.Runtime.QianEventAttribute"))
            {
                var explicitOwner = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.ComponentOwnerAttribute")?.ConstructorArguments[0].Value as string
                    ?? symbol.ContainingType?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.ComponentOwnerAttribute")?.ConstructorArguments[0].Value as string;
                var imported = explicitTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, symbol) || SymbolEqualityComparer.Default.Equals(t, symbol.ContainingType));
                var owner = explicitOwner ?? (imported ? Id(plugin) : plugins.Length == 1 ? Id(plugins[0]) : null);
                if (owner is null || !plugins.Any(p => Id(p) == owner)) { Report(output, symbol, "权限或事件声明必须唯一归属当前程序集的组件；多组件程序集使用 ComponentOwner 或 MetadataTypes。"); return null; }
                if (owner != Id(plugin)) { if (imported) { Report(output, symbol, "MetadataTypes 不能引用归属其它组件的声明。"); return null; } continue; }
                if (attribute.AttributeClass!.Name == "PermissionAttribute")
                {
                    var resource = attribute.ConstructorArguments[0].Value as string; var action = attribute.ConstructorArguments[1].Value as string;
                    bool Valid(string? value) => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z][a-z0-9_-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    if (!Valid(resource) || !Valid(action) || !permissionNames.Add(resource + ":" + action)) { Report(output, symbol, "权限资源/动作必须合法且不重复。"); return null; }
                    var riskValue = attribute.NamedArguments.FirstOrDefault(p => p.Key == "Risk").Value.Value;
                    var risk = riskValue is int number ? number : 0;
                    if (risk < 0 || risk > 3) { Report(output, symbol, "权限风险不合法。"); return null; }
                    var step = attribute.NamedArguments.FirstOrDefault(p => p.Key == "RequiresStepUp").Value.Value is true;
                    var terminals = attribute.NamedArguments.FirstOrDefault(p => p.Key == "Terminals").Value;
                    var names = terminals.IsNull || terminals.Kind != TypedConstantKind.Array ? [] : terminals.Values.Select(v => v.Value as string).ToArray();
                    if (names.Any(n => !Valid(n))) { Report(output, symbol, "权限终端名不合法。"); return null; }
                    permissions.Add("{\"resource\":" + Json(resource!) + ",\"actions\":[" + Json(action!) + "],\"risk\":{" + Json(action!) + ":" + Json(risk switch { 0 => "low", 1 => "medium", 2 => "high", _ => "critical" }) + "},\"stepUp\":[" + (step ? Json(action!) : "") + "],\"terminals\":{" + Json(action!) + ":[" + string.Join(",", names.Select(n => Json(n!))) + "]}}");
                }
                else
                {
                    if (symbol is not IFieldSymbol { IsStatic: true, IsReadOnly: true, DeclaredAccessibility: Accessibility.Public } field ||
                        field.Type is not INamedTypeSymbol { Name: "QianEvent", TypeArguments.Length: 2 } eventType || eventType.ContainingNamespace.ToDisplayString() != "Qian.Runtime")
                    { Report(output, symbol, "QianEvent 声明须为公开静态只读的强类型事件字段。"); return null; }
                    var reference = field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + field.Name;
                    var mode = attribute.NamedArguments.FirstOrDefault(p => p.Key == "Mode").Value.Value is int value ? value : 0;
                    events.Add("global::Qian.Plugin.Sdk.ComponentMetadataDeclarations.Event(" + reference + ", (global::Qian.Runtime.DispatchMode)" + mode + ", " + Literal(symbol.ToDisplayString()) + ")");
                }
            }
        }
        var config = plugin.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianConfigSchemaAttribute")?.ConstructorArguments[0].Value as string;
        return (Literal("[" + string.Join(",", permissions) + "]"), "new global::System.Text.Json.Nodes.JsonArray(" + string.Join(", ", events) + ").ToJsonString()", config is null ? "null" : Literal(config));
    }
    private static string Json(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}
