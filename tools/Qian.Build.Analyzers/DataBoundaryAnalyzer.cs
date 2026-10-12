using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qian.Build.Analyzers;

/// <summary>禁止在线上下文绕开 Qian 安全基类，也禁止直接或委托调用 EF 查询过滤器旁路。</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ContextRule = new(
        "QIAN0003", "在线数据上下文必须安全装配", "上下文 {0} 必须继承 Qian.Data.QianDbContext",
        "Security", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor FilterRule = new(
        "QIAN0004", "禁止绕过数据过滤器", "禁止使用 EF Core 的 IgnoreQueryFilters；迁移和运维使用受控的独立数据库连接",
        "Security", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ContextRule, FilterRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var efContext = start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
            if (efContext is null) return;
            var qianContext = start.Compilation.GetTypeByMetadataName("Qian.Data.QianDbContext");
            var isTest = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions
                .TryGetValue("build_property.IsTestProject", out var testValue) &&
                string.Equals(testValue, "true", StringComparison.OrdinalIgnoreCase);
            if (!isTest)
                start.RegisterSymbolAction(symbol =>
                {
                    var type = (INamedTypeSymbol)symbol.Symbol;
                    var bases = new List<INamedTypeSymbol>();
                    for (var current = type; current is not null; current = current.BaseType) bases.Add(current);
                    if (!bases.Any(item => SymbolEqualityComparer.Default.Equals(item, efContext)) ||
                        bases.Any(item => SymbolEqualityComparer.Default.Equals(item, qianContext))) return;
                    foreach (var location in type.Locations.Where(location => location.IsInSource))
                        symbol.ReportDiagnostic(Diagnostic.Create(ContextRule, location, type.Name));
                }, SymbolKind.NamedType);
            start.RegisterOperationAction(operation =>
            {
                var method = operation.Operation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IMethodReferenceOperation reference => reference.Method,
                    _ => null
                };
                if (method?.Name == "IgnoreQueryFilters" &&
                    method.ContainingType.ToDisplayString() == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")
                    operation.ReportDiagnostic(Diagnostic.Create(FilterRule, operation.Operation.Syntax.GetLocation()));
            }, OperationKind.Invocation, OperationKind.MethodReference);
        });
    }
}
