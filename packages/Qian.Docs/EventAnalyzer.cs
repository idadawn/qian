using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qian.Docs.Abstractions;
using Qian.Runtime;

namespace Qian.Docs;

internal sealed class EventAnalyzer(string root, Diagnostics diagnostics)
{
    private readonly Dictionary<ISymbol, Definition> _definitions = new(SymbolEqualityComparer.Default);
    private readonly List<(ISymbol Symbol, EventSite Site)> _annotated = [];
    private CSharpCompilation _compilation = null!;
    private sealed record Definition(string Name, DispatchMode Mode, EventSite Site)
    {
        internal List<EventSite> Producers { get; } = [];
        internal List<EventSite> Consumers { get; } = [];
    }
    internal EventMap[] Analyze(IReadOnlyList<string> directories)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            var full = Paths.Within(root, directory, allowRoot: true);
            if (Directory.Exists(full)) Discover(full, files, 0);
        }
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview), path: file)).ToArray();
        var runtimeAssembly = typeof(QianEvent).Assembly.Location;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(runtimeAssembly).Distinct(StringComparer.Ordinal).Select(file => MetadataReference.CreateFromFile(file));
        var globals = CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.Threading; global using System.Threading.Tasks;", new CSharpParseOptions(LanguageVersion.Preview));
        _compilation = CSharpCompilation.Create("QianDocsInspection", trees.Append(globals), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        foreach (var tree in trees)
        {
            foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                diagnostics.Add(Display(tree.FilePath), error.Location.GetLineSpan().StartLinePosition.Line + 1, DocumentErrors.Source, "C# 源码语法无效，不能生成可信映射。");
            var model = _compilation.GetSemanticModel(tree);
            foreach (var variable in tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Parent?.Parent is not FieldDeclarationSyntax || variable.Initializer is null) continue;
                Register(model.GetDeclaredSymbol(variable), variable.Initializer.Value, variable);
            }
            foreach (var property in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var value = property.Initializer?.Value ?? property.ExpressionBody?.Expression;
                if (value is not null) Register(model.GetDeclaredSymbol(property), value, property);
            }
        }
        foreach (var (symbol, site) in _annotated)
        {
            var definition = _definitions.GetValueOrDefault(symbol);
            if (definition is null && Initializer(symbol) is { } expression) definition = Resolve(expression, 0);
            foreach (var attribute in symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Qian.Runtime.QianEventAttribute"))
            {
                var value = attribute.NamedArguments.FirstOrDefault(p => p.Key == "Mode").Value.Value;
                var annotated = value is int mode ? (DispatchMode)mode : DispatchMode.Emit;
                if (definition is null) diagnostics.Add(site.File, site.Line, DocumentErrors.Declaration, "带 QianEvent 特性的事件值不能静态解析。");
                else if (annotated != definition.Mode) diagnostics.Add(site.File, site.Line, DocumentErrors.ModeMismatch,
                    $"事件 {definition.Name} 的 QianEvent 特性声明为 {annotated}，初始化值为 {definition.Mode}。");
            }
        }
        foreach (var group in _definitions.Values.GroupBy(d => d.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var definition in group) diagnostics.Add(definition.Site.File, definition.Site.Line, DocumentErrors.Declaration, "同名事件存在多个定义；请复用 Definition 的同一实例。");
        foreach (var tree in trees)
        {
            var model = _compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(call) is not IInvocationOperation operation) continue;
                var method = operation.TargetMethod;
                if (method.Name is not ("On" or "DispatchAsync") || method.Parameters.Length == 0 || !IsEvent(method.Parameters[0].Type) || !IsContext(method.ContainingType)) continue;
                // 按绑定后的参数序号识别，支持 using alias、限定名、命名实参与局部事件别名。
                var argument = operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value.Syntax as ExpressionSyntax;
                if (argument is null) continue;
                var definition = Resolve(argument, 0);
                var site = Site(call, model.GetEnclosingSymbol(call.SpanStart));
                if (definition is null)
                {
                    diagnostics.Add(site.File, site.Line, DocumentErrors.Dynamic, "事件来自动态参数或外部二进制，未能静态解析；此调用不计入已验证映射。", warning: true);
                    continue;
                }
                if (method.Name == "DispatchAsync") definition.Producers.Add(site);
                else
                {
                    definition.Consumers.Add(site);
                    var modeArgument = operation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "mode")?.Value.Syntax as ExpressionSyntax;
                    var mode = modeArgument is null ? null : Mode(modeArgument, 0);
                    if (mode is null) diagnostics.Add(site.File, site.Line, DocumentErrors.ModeMismatch, "监听模式无法静态确定；请使用常量或事件的 Mode。");
                    else if (mode != definition.Mode) diagnostics.Add(site.File, site.Line, DocumentErrors.ModeMismatch,
                        $"事件 {definition.Name} 声明为 {definition.Mode}，监听注册使用 {mode}。");
                }
            }
        }
        return _definitions.Values.OrderBy(d => d.Name, StringComparer.Ordinal).ThenBy(d => d.Site.File, StringComparer.Ordinal)
            .Select(d => new EventMap(d.Name, d.Mode.ToString().ToLowerInvariant(), d.Site,
                d.Producers.OrderBy(s => s.File, StringComparer.Ordinal).ThenBy(s => s.Line).ToArray(),
                d.Consumers.OrderBy(s => s.File, StringComparer.Ordinal).ThenBy(s => s.Line).ToArray())).ToArray();
    }
    private void Register(ISymbol? symbol, ExpressionSyntax expression, SyntaxNode declaration)
    {
        var type = symbol switch { IFieldSymbol field => field.Type, IPropertySymbol property => property.Type, _ => null };
        if (symbol is null || !IsEvent(type)) return;
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Qian.Runtime.QianEventAttribute")) _annotated.Add((symbol, Site(declaration, symbol)));
        if (Mutated(symbol))
        {
            var mutableSite = Site(declaration, symbol);
            diagnostics.Add(mutableSite.File, mutableSite.Line, DocumentErrors.Declaration, "事件 Definition 须使用只读字段或只读属性，不能把可变初始化值当成固定契约。");
            return;
        }
        var model = _compilation.GetSemanticModel(expression.SyntaxTree);
        if (expression is InvocationExpressionSyntax factory &&
            model.GetSymbolInfo(factory).Symbol is IMethodSymbol method &&
            (method.Name != "Define" || method.ContainingType.ToDisplayString() != "Qian.Runtime.QianEvent"))
        {
            var location = Site(declaration, symbol);
            diagnostics.Add(location.File, location.Line, DocumentErrors.Declaration, "事件工厂不能静态解析；请使用 QianEvent.Define 或事件构造器。");
            return;
        }
        var args = expression switch
        {
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments ?? default,
            ImplicitObjectCreationExpressionSyntax creation => creation.ArgumentList.Arguments,
            _ => default
        };
        // 别名字段交给 Resolve 追踪，不把它变成第二份事件定义。
        if (args.Count == 0)
        {
            if (expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or ParenthesizedExpressionSyntax))
            {
                var unsupported = Site(declaration, symbol);
                diagnostics.Add(unsupported.File, unsupported.Line, DocumentErrors.Declaration, "事件初始化表达式无法静态解析，请使用直接定义或只读别名。");
            }
            return;
        }
        var site = Site(declaration, symbol);
        var nameArg = args.FirstOrDefault(a => string.Equals(a.NameColon?.Name.Identifier.ValueText, "name", StringComparison.OrdinalIgnoreCase)) ?? args[0];
        var modeArg = args.FirstOrDefault(a => string.Equals(a.NameColon?.Name.Identifier.ValueText, "mode", StringComparison.OrdinalIgnoreCase)) ?? (args.Count > 1 ? args[1] : null);
        var name = model.GetConstantValue(nameArg.Expression);
        var mode = modeArg is null ? null : Mode(modeArg.Expression, 0);
        if (!name.HasValue || name.Value is not string eventName || string.IsNullOrWhiteSpace(eventName) || mode is null)
        {
            diagnostics.Add(site.File, site.Line, DocumentErrors.Declaration, "事件定义须有静态名称与分发模式，不能根据执行结果推测。"); return;
        }
        _definitions[symbol] = new Definition(eventName, mode.Value, site);
    }
    private Definition? Resolve(ExpressionSyntax expression, int depth)
    {
        if (depth > 32) return null;
        if (expression is ParenthesizedExpressionSyntax paren) return Resolve(paren.Expression, depth + 1);
        var model = _compilation.GetSemanticModel(expression.SyntaxTree);
        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is null || Mutated(symbol)) return null;
        if (_definitions.TryGetValue(symbol, out var result)) return result;
        var initializer = Initializer(symbol);
        return initializer is null ? null : Resolve(initializer, depth + 1);
    }
    private DispatchMode? Mode(ExpressionSyntax expression, int depth)
    {
        if (depth > 32) return null;
        var model = _compilation.GetSemanticModel(expression.SyntaxTree);
        var constant = model.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is int value && Enum.IsDefined(typeof(DispatchMode), value)) return (DispatchMode)value;
        if (expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Mode" } member) return Resolve(member.Expression, depth + 1)?.Mode;
        var symbol = model.GetSymbolInfo(expression).Symbol;
        var initializer = symbol is null || Mutated(symbol) ? null : Initializer(symbol);
        return initializer is null ? null : Mode(initializer, depth + 1);
    }
    private bool Mutated(ISymbol symbol)
    {
        if (symbol is IFieldSymbol field) return !field.IsReadOnly && !field.IsConst;
        if (symbol is IPropertySymbol property) return property.SetMethod is not null;
        if (symbol is not ILocalSymbol || symbol.DeclaringSyntaxReferences.FirstOrDefault() is not { } reference) return false;
        var model = _compilation.GetSemanticModel(reference.SyntaxTree);
        foreach (var node in reference.SyntaxTree.GetRoot().DescendantNodes())
        {
            ExpressionSyntax? written = node switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left,
                ArgumentSyntax argument when !argument.RefKindKeyword.IsKind(SyntaxKind.None) => argument.Expression,
                PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.PreIncrementExpression) || unary.IsKind(SyntaxKind.PreDecrementExpression) => unary.Operand,
                PostfixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.PostIncrementExpression) || unary.IsKind(SyntaxKind.PostDecrementExpression) => unary.Operand,
                _ => null
            };
            if (written is not null && SymbolEqualityComparer.Default.Equals(symbol, model.GetSymbolInfo(written).Symbol)) return true;
        }
        return false;
    }
    private static bool IsContext(INamedTypeSymbol type) => type.ToDisplayString() == "Qian.Runtime.IPluginContext" || type.AllInterfaces.Any(i => i.ToDisplayString() == "Qian.Runtime.IPluginContext");
    private static ExpressionSyntax? Initializer(ISymbol symbol) => symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() switch
    {
        VariableDeclaratorSyntax variable => variable.Initializer?.Value,
        PropertyDeclarationSyntax property => property.Initializer?.Value ?? property.ExpressionBody?.Expression,
        _ => null
    };
    private static bool IsEvent(ITypeSymbol? type) => type is INamedTypeSymbol named && named.OriginalDefinition.Name == "QianEvent" &&
        named.Arity == 2 && named.ContainingNamespace.ToDisplayString() == "Qian.Runtime";
    private EventSite Site(SyntaxNode node, ISymbol? symbol) => new(Display(node.SyntaxTree.FilePath), node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, symbol?.ToDisplayString() ?? "<顶层>");
    private string Display(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static void Discover(string directory, ISet<string> files, int depth)
    {
        if (depth > 32 || files.Count > 4096) throw new IOException("源码目录超过限制。");
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("源码目录不能为符号链接。");
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint) || new FileInfo(file).Length > 1_048_576) throw new IOException("源码文件无效。");
            files.Add(file);
        }
        foreach (var child in Directory.EnumerateDirectories(directory))
            if (Path.GetFileName(child) is not ("bin" or "obj" or "node_modules" or ".git" or "dist" or "tests")) Discover(child, files, depth + 1);
    }
}
