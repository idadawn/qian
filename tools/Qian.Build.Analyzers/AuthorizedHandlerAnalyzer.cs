using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qian.Build.Analyzers;

/// <summary>受保护请求不可绕开原子注册入口；泛型封装同样不能掩盖实际请求类型。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AuthorizedHandlerAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "QIAN0005", "受保护命令必须同时登记权限行为",
        "请求 {0} 必须使用 AddAuthorizedHandler 注册，不能直接调用或引用 AddRequestHandler",
        "Security", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var builder = start.Compilation.GetTypeByMetadataName("Kun.Mediator.KunMediatorBuilder");
            var contract = start.Compilation.GetTypeByMetadataName("Kun.Authorization.IPermissionProtectedRequest");
            if (builder is null || contract is null) return;
            start.RegisterSyntaxNodeAction(node =>
            {
                var name = (GenericNameSyntax)node.Node;
                if (name.Identifier.ValueText != "AddRequestHandler") return;
                if (node.SemanticModel.GetSymbolInfo(name, node.CancellationToken).Symbol is not IMethodSymbol method ||
                    !SymbolEqualityComparer.Default.Equals(method.ContainingType, builder) || method.TypeArguments.Length != 3) return;
                // 唯一可信桥接位于 Definition 程序集本身；应用里复制同名类型不会得到豁免。
                var enclosing = node.SemanticModel.GetEnclosingSymbol(name.SpanStart, node.CancellationToken);
                if (enclosing?.ContainingAssembly?.Name == "Qian.Authz.Abstractions" &&
                    enclosing.ContainingType?.ToDisplayString() == "Qian.Authz.Abstractions.AuthorizedHandlerRegistration" &&
                    enclosing.Name == "AddAuthorizedHandler") return;
                var request = method.TypeArguments[0];
                if (request is ITypeParameterSymbol || request.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)))
                    node.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), request.ToDisplayString()));
            }, SyntaxKind.GenericName);
        });
    }
}
