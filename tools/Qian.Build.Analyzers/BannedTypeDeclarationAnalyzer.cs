using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qian.Build.Analyzers;

/// <summary>补充标准 Banned API 的声明边界，与它共用 BannedSymbols.txt，不维护第二份禁用清单。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedTypeDeclarationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "QIAN0002", "旧 API 类型声明被禁止", "类型 {0} 已被禁用：{1}", "Compatibility", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var banned = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in start.Options.AdditionalFiles.Where(file => Path.GetFileName(file.Path) == "BannedSymbols.txt"))
            {
                var text = file.GetText(start.CancellationToken);
                if (text is null) continue;
                foreach (var line in text.Lines)
                {
                    var declaration = line.ToString().Trim();
                    if (!declaration.StartsWith("T:", StringComparison.Ordinal)) continue;
                    var separator = declaration.IndexOf(';');
                    var id = separator < 0 ? declaration : declaration.Substring(0, separator).Trim();
                    banned[id] = separator < 0 ? "使用当前接口" : declaration.Substring(separator + 1);
                }
            }
            if (banned.Count == 0) return;
            start.RegisterSyntaxNodeAction(node =>
            {
                var name = (SimpleNameSyntax)node.Node;
                var symbol = node.SemanticModel.GetSymbolInfo(name, node.CancellationToken).Symbol;
                if (symbol is IAliasSymbol alias) symbol = alias.Target;
                if (symbol is not ITypeSymbol type) return;
                var id = type.OriginalDefinition.GetDocumentationCommentId();
                if (id is not null && banned.TryGetValue(id, out var reason))
                    node.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), type.ToDisplayString(), reason));
            }, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        });
    }
}
