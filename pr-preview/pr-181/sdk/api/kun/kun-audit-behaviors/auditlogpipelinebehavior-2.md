> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class AuditLogPipelineBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.Behaviors](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/index.md)\
Assembly: Kun.Audit.dll

Mediator pipeline behaviour that records one [AuditEntry](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md) per handler
invocation and appends it to the per-tenant hash chain. Chain-state mutation and
the [IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md) write are serialised inside
[IAuditChainState.AppendAsync](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md#Kun_Audit_IAuditChainState_AppendAsync_System_String_System_Func_System_String_System_Threading_Tasks_ValueTask_System_String___System_Threading_CancellationToken_) so concurrent commands on the same
tenant cannot fork the chain.

```csharp
public sealed class AuditLogPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

`TResponse`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditLogPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)

#### Implements

IPipelineBehavior\<TRequest, TResponse>

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Failure handling: if the handler throws, the original exception is preserved and
rethrown _after_ the audit write so the audit row exists and records
[AuditOutcome.Exception](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditoutcome.md#Kun_Audit_AuditOutcome_Exception). If the audit write itself fails (sink error,
chain-state lock abandoned), the failure is logged at warning level and swallowed
— audit must never convert a successful business outcome into an error nor replace
the handler's own exception with an audit-pipeline one.

Registered explicitly per command type via
[KunAuditServiceCollectionExtensions.AddAuditBehavior](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditservicecollectionextensions.md#Kun_Audit_DependencyInjection_KunAuditServiceCollectionExtensions_AddAuditBehavior__2_Kun_Mediator_KunMediatorBuilder_)
so queries are not audited by default.

## Constructors

### AuditLogPipelineBehavior(IAuditSink, IAuditChainState, ITenantContext, ICurrentUser, TimeProvider, ILogger\<AuditLogPipelineBehavior\<TRequest, TResponse>>) \{#Kun_Audit_Behaviors_AuditLogPipelineBehavior_2__ctor_Kun_Audit_IAuditSink_Kun_Audit_IAuditChainState_Kun_Core_MultiTenancy_ITenantContext_Kun_Core_Security_ICurrentUser_System_TimeProvider_Microsoft_Extensions_Logging_ILogger_Kun_Audit_Behaviors_AuditLogPipelineBehavior__0__1\_\_\_}

Initialises a new [AuditLogPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md).

```csharp
public AuditLogPipelineBehavior(IAuditSink sink, IAuditChainState chain, ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider clock, ILogger<AuditLogPipelineBehavior<TRequest, TResponse>> logger)
```

#### Parameters

`sink` [IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md)

`chain` [IAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md)

`tenantContext` ITenantContext

`currentUser` ICurrentUser

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[AuditLogPipelineBehavior](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)\<TRequest, TResponse>>

### AuditLogPipelineBehavior(IAuditSink, IAuditChainState, ITenantContext, ICurrentUser, TimeProvider, ILogger\<AuditLogPipelineBehavior\<TRequest, TResponse>>, IOptions\<KunAuditOptions>) \{#Kun_Audit_Behaviors_AuditLogPipelineBehavior_2__ctor_Kun_Audit_IAuditSink_Kun_Audit_IAuditChainState_Kun_Core_MultiTenancy_ITenantContext_Kun_Core_Security_ICurrentUser_System_TimeProvider_Microsoft_Extensions_Logging_ILogger_Kun_Audit_Behaviors_AuditLogPipelineBehavior__0__1___Microsoft_Extensions_Options_IOptions_Kun_Audit_DependencyInjection_KunAuditOptions\_\_}

从宿主审计选项选择 user、service、agent 或 system 主体类型。

```csharp
public AuditLogPipelineBehavior(IAuditSink sink, IAuditChainState chain, ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider clock, ILogger<AuditLogPipelineBehavior<TRequest, TResponse>> logger, IOptions<KunAuditOptions> options)
```

#### Parameters

`sink` [IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md)

`chain` [IAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md)

`tenantContext` ITenantContext

`currentUser` ICurrentUser

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[AuditLogPipelineBehavior](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)\<TRequest, TResponse>>

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunAuditOptions](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md)>

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Audit_Behaviors_AuditLogPipelineBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` RequestHandlerDelegate\<TResponse>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

The response from the inner pipeline.
