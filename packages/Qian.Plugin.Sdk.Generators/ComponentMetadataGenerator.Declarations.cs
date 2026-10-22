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
        var explicitTypes = MetadataTypes(plugin);
        var sharedTypes = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
        foreach (var type in explicitTypes)
        {
            if (plugins.Count(p => MetadataTypes(p).Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, type))) != 1)
            { Report(output, type, "同一 MetadataTypes 声明不能归属多个组件。"); return null; }
            symbols.Add(type); symbols.AddRange(type.GetMembers());
        }
        foreach (var shared in plugin.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.SharedEventsAttribute"))
        {
            var type = shared.ConstructorArguments[0].Value as INamedTypeSymbol;
            var definitionComponent = shared.ConstructorArguments[1].Value as string;
            if (type is null || definitionComponent is null || sharedTypes.ContainsKey(type) || explicitTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, type)))
            { Report(output, plugin, "SharedEvents 必须引用一个非重复的事件 Definition 类型，不能同时声明为自身 MetadataTypes。"); return null; }
            var owners = plugins.Where(p => MetadataTypes(p).Any(t => SymbolEqualityComparer.Default.Equals(t, type))).ToArray();
            if (owners.Length != 1 || Id(owners[0]) != definitionComponent || definitionComponent == Id(plugin) || plugins.Count(p => Id(p) == definitionComponent) != 1)
            { Report(output, plugin, "SharedEvents 须引用同程序集内由一个其它组件通过 MetadataTypes 唯一拥有的事件契约。"); return null; }
            var members = new ISymbol[] { type }.Concat(type.GetMembers()).ToArray();
            if (members.Any(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Qian.Authz.Abstractions.PermissionAttribute")) ||
                !members.Any(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Qian.Runtime.QianEventAttribute")))
            { Report(output, plugin, "SharedEvents 只共享非空的事件声明类型，不能携带 Permission 权限声明。"); return null; }
            sharedTypes.Add(type, definitionComponent); symbols.AddRange(members);
        }
        var permissions = new List<string>(); var events = new List<string>(); var permissionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in symbols.Distinct(SymbolEqualityComparer.Default).OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal))
        {
            foreach (var attribute in symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() is "Qian.Authz.Abstractions.PermissionAttribute" or "Qian.Runtime.QianEventAttribute"))
            {
                var explicitOwner = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.ComponentOwnerAttribute")?.ConstructorArguments[0].Value as string
                    ?? symbol.ContainingType?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.ComponentOwnerAttribute")?.ConstructorArguments[0].Value as string;
                bool Contains(INamedTypeSymbol type) => SymbolEqualityComparer.Default.Equals(type, symbol) || SymbolEqualityComparer.Default.Equals(type, symbol.ContainingType);
                var imported = explicitTypes.Any(Contains);
                var owners = plugins.Where(p => MetadataTypes(p).Any(Contains)).ToArray();
                if (owners.Length > 1) { Report(output, symbol, "同一声明的 MetadataTypes 拥有者不唯一。"); return null; }
                var shared = sharedTypes.Where(pair => Contains(pair.Key)).Select(pair => pair.Value).SingleOrDefault();
                var owner = explicitOwner ?? (owners.Length == 1 ? Id(owners[0]) : plugins.Length == 1 ? Id(plugins[0]) : null);
                if (owner is null || !plugins.Any(p => Id(p) == owner)) { Report(output, symbol, "权限或事件声明必须唯一归属当前程序集的组件；多组件程序集使用 ComponentOwner 或 MetadataTypes。"); return null; }
                if (shared is not null && shared != owner) { Report(output, symbol, "SharedEvents 引用的拥有者与实际声明归属不符。"); return null; }
                if (owner != Id(plugin) && shared is null) { if (imported) { Report(output, symbol, "MetadataTypes 不能引用归属其它组件的声明。"); return null; } continue; }
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
                    events.Add("global::Qian.Plugin.Sdk.ComponentMetadataDeclarations.Event(" + reference + ", (global::Qian.Runtime.DispatchMode)" + mode + ", " + Literal(symbol.ToDisplayString()) + ", " + Literal(owner) + ")");
                }
            }
        }
        var config = plugin.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianConfigSchemaAttribute")?.ConstructorArguments[0].Value as string;
        return (Literal("[" + string.Join(",", permissions) + "]"), "new global::System.Text.Json.Nodes.JsonArray(" + string.Join(", ", events) + ").ToJsonString()", config is null ? "null" : Literal(config));
    }
    private static INamedTypeSymbol[] MetadataTypes(INamedTypeSymbol plugin)
    {
        var included = plugin.GetAttributes().Single(a => a.AttributeClass?.ToDisplayString() == "Qian.Plugin.Sdk.QianPluginAttribute")
            .NamedArguments.FirstOrDefault(p => p.Key == "MetadataTypes").Value;
        return included.IsNull || included.Kind != TypedConstantKind.Array ? [] : included.Values.Select(v => v.Value).OfType<INamedTypeSymbol>().ToArray();
    }
    private static string Json(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
}
