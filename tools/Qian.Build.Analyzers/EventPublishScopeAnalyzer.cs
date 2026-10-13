using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qian.Build.Analyzers;

/// <summary>
/// 集成事件只能在命令 handler 同 scope 发出（ADR-008 D8.2、PRD 7.1 第 2 项）：
/// 允许写命令的处理器与随工作单元同 scope 派发的领域事件处理器调用 Publish / Enqueue，其余位置一律报错。
/// 直接向 Kun 缓冲器追加消息会绕过 v1 信封，只允许 Outbox 写入端自身这样做。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventPublishScopeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "QIAN0006", "集成事件只能在命令 handler 同 scope 发出",
        "{0} 不是写命令的处理器或领域事件处理器，不能调用 {1}；把发布移进命令 handler，或经领域事件在同一工作单元内发出",
        "Reliability", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // 测试直接驱动 Outbox 验证数据会话本身；xUnit v3 项目不一定设置 IsTestProject，按测试框架引用识别。
            var isTest = (start.Options.AnalyzerConfigOptionsProvider.GlobalOptions
                .TryGetValue("build_property.IsTestProject", out var testValue) &&
                string.Equals(testValue, "true", StringComparison.OrdinalIgnoreCase)) ||
                start.Compilation.GetTypeByMetadataName("Xunit.FactAttribute") is not null;
            var symbols = Symbols.Create(start.Compilation);
            if (isTest || symbols is null) return;
            start.RegisterOperationAction(operation =>
            {
                var method = operation.Operation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IMethodReferenceOperation reference => reference.Method,
                    _ => null
                };
                if (method is null || !symbols.IsPublish(method, out var raw)) return;
                var owner = operation.ContainingSymbol as INamedTypeSymbol ?? operation.ContainingSymbol.ContainingType;
                if (owner is null || symbols.IsWriter(owner) || (!raw && symbols.IsPublishScope(owner))) return;
                operation.ReportDiagnostic(Diagnostic.Create(Rule, operation.Operation.Syntax.GetLocation(),
                    owner.ToDisplayString(), method.ContainingType.Name + "." + method.Name));
            }, OperationKind.Invocation, OperationKind.MethodReference);
        });
    }

    private sealed class Symbols
    {
        private readonly ImmutableArray<IMethodSymbol> _publish;
        private readonly ImmutableArray<IMethodSymbol> _raw;
        private readonly ImmutableArray<INamedTypeSymbol> _writers;
        private readonly INamedTypeSymbol? _requestHandler;
        private readonly INamedTypeSymbol? _command;
        private readonly INamedTypeSymbol? _commandOf;
        private readonly INamedTypeSymbol? _notificationHandler;
        private readonly INamedTypeSymbol? _domainEvent;
        private readonly INamedTypeSymbol? _capability;

        private Symbols(Compilation compilation, ImmutableArray<INamedTypeSymbol> writers, ImmutableArray<IMethodSymbol> publish, ImmutableArray<IMethodSymbol> raw)
        {
            _writers = writers;
            _publish = publish;
            _raw = raw;
            _requestHandler = compilation.GetTypeByMetadataName("Kun.Core.Cqrs.IRequestHandler`2");
            _command = compilation.GetTypeByMetadataName("Kun.Core.Cqrs.ICommand");
            _commandOf = compilation.GetTypeByMetadataName("Kun.Core.Cqrs.ICommand`1");
            _notificationHandler = compilation.GetTypeByMetadataName("Kun.Mediator.INotificationHandler`1");
            _domainEvent = compilation.GetTypeByMetadataName("Kun.Core.Domain.IDomainEvent");
            _capability = compilation.GetTypeByMetadataName("Qian.Plugin.Sdk.CapabilityAttribute");
        }

        public static Symbols? Create(Compilation compilation)
        {
            var bus = compilation.GetTypeByMetadataName("Kun.EventBus.IIntegrationEventBus");
            var writer = compilation.GetTypeByMetadataName("Kun.EventBus.Outbox.IOutboxWriter");
            if (bus is null && writer is null) return null;
            var writers = new[] { bus, writer }.Where(type => type is not null).Select(type => type!).ToImmutableArray();
            var publish = writers.SelectMany(type => type.GetMembers().OfType<IMethodSymbol>().Where(m => m.Name is "Publish" or "Enqueue")).ToImmutableArray();
            var raw = new[] { compilation.GetTypeByMetadataName("Kun.EventBus.Outbox.IIntegrationEventOutbox"), compilation.GetTypeByMetadataName("Kun.EventBus.Outbox.IntegrationEventBuffer") }
                .Where(type => type is not null).SelectMany(type => type!.GetMembers("Add").OfType<IMethodSymbol>()).ToImmutableArray();
            return new Symbols(compilation, writers, publish, raw);
        }

        // 接口方法本身，或某个类型对它的实现；后者覆盖经具体类型调用的情形。
        public bool IsPublish(IMethodSymbol method, out bool raw)
        {
            raw = false;
            if (method.Name is not ("Publish" or "Enqueue" or "Add")) return false;
            var original = method.OriginalDefinition;
            raw = _raw.Any(item => SymbolEqualityComparer.Default.Equals(item, original) || Implements(original, item));
            return raw || _publish.Any(item => SymbolEqualityComparer.Default.Equals(item, original) || Implements(original, item));
        }

        public bool IsWriter(INamedTypeSymbol type) => type.AllInterfaces.Any(item => _writers.Contains(item.OriginalDefinition, SymbolEqualityComparer.Default));

        public bool IsPublishScope(INamedTypeSymbol type)
        {
            foreach (var item in type.AllInterfaces)
            {
                var definition = item.OriginalDefinition;
                if (SymbolEqualityComparer.Default.Equals(definition, _requestHandler) && item.TypeArguments.Length == 2 && IsCommand(item.TypeArguments[0])) return true;
                if (SymbolEqualityComparer.Default.Equals(definition, _notificationHandler) && item.TypeArguments.Length == 1 &&
                    item.TypeArguments[0].AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, _domainEvent))) return true;
            }
            return false;
        }

        private bool IsCommand(ITypeSymbol request)
        {
            if (request.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, _command) ||
                SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, _commandOf))) return true;
            // SDK 能力请求以 [Capability] 的 SideEffect 区分命令与查询，默认 read 为查询。
            var attribute = request.GetAttributes().FirstOrDefault(item => SymbolEqualityComparer.Default.Equals(item.AttributeClass, _capability));
            if (attribute is null) return false;
            var sideEffect = attribute.NamedArguments.FirstOrDefault(item => item.Key == "SideEffect").Value.Value as string ?? "read";
            return sideEffect != "read";
        }

        private static bool Implements(IMethodSymbol method, IMethodSymbol interfaceMethod) =>
            method.ContainingType is { } type && SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(interfaceMethod)?.OriginalDefinition, method);
    }
}
