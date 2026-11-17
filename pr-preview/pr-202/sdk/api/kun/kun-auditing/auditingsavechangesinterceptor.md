> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class AuditingSaveChangesInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing](/pr-preview/pr-202/sdk/api/kun/kun-auditing/index.md)\
Assembly: Kun.Auditing.dll

EF Core [`ISaveChangesInterceptor`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor) that emits one
[AuditEventIntegrationEvent](/pr-preview/pr-202/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) per tracked change of an entity type registered
via `services.AddAuditable<TEntity>()`. The event is forwarded to
[IAuditSink](/pr-preview/pr-202/sdk/api/kun/kun-auditing/iauditsink.md) (default: outbox writer) so it commits atomically with the
ambient `SaveChangesAsync` transaction.

```csharp
public sealed class AuditingSaveChangesInterceptor : ISaveChangesInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditingSaveChangesInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md)

#### Implements

[ISaveChangesInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor),
[IInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.iinterceptor)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

**AOT guarantee**: membership is decided purely by [IAuditableEntityRegistry](/pr-preview/pr-202/sdk/api/kun/kun-auditing/iauditableentityregistry.md)
— `entry.Entity.GetType()` is matched against a [`FrozenSet`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozenset-1)
of types. No runtime reflection over [AuditableAttribute](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditableattribute.md) is performed.

Scoped lifetime — the interceptor resolves the scoped [AuditContext](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditcontext.md) and
writes to a scoped [IAuditSink](/pr-preview/pr-202/sdk/api/kun/kun-auditing/iauditsink.md).

## Constructors

### AuditingSaveChangesInterceptor(IAuditableEntityRegistry, IAuditSink, AuditContext, TimeProvider, ILogger\<AuditingSaveChangesInterceptor>?) \{#Kun_Auditing_AuditingSaveChangesInterceptor__ctor_Kun_Auditing_IAuditableEntityRegistry_Kun_Auditing_IAuditSink_Kun_Auditing_AuditContext_System_TimeProvider_Microsoft_Extensions_Logging_ILogger_Kun_Auditing_AuditingSaveChangesInterceptor\_\_}

Creates a new [AuditingSaveChangesInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md).

```csharp
public AuditingSaveChangesInterceptor(IAuditableEntityRegistry registry, IAuditSink sink, AuditContext auditContext, TimeProvider timeProvider, ILogger<AuditingSaveChangesInterceptor>? logger = null)
```

#### Parameters

`registry` [IAuditableEntityRegistry](/pr-preview/pr-202/sdk/api/kun/kun-auditing/iauditableentityregistry.md)

`sink` [IAuditSink](/pr-preview/pr-202/sdk/api/kun/kun-auditing/iauditsink.md)

`auditContext` [AuditContext](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditcontext.md)

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[AuditingSaveChangesInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md)>?

## Methods

### SavingChanges(DbContextEventData, InterceptionResult\<int>) \{#Kun_Auditing_AuditingSaveChangesInterceptor_SavingChanges_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32\_\_}

Called at the start of `DbContext.SaveChanges`.

```csharp
public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
```

#### Parameters

`eventData` [DbContextEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexteventdata)

Contextual information about the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) being used.

`result` [InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

Represents the current result if one exists.
This value will have [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) set to [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if some previous
interceptor suppressed execution by calling [`SuppressWithResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.suppresswithresult).
This value is typically used as the return value for the implementation of this method.

#### Returns

[InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

If [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) is false, the EF will continue as normal.
If [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) is true, then EF will suppress the operation it
was about to perform and use [`Result`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.result) instead.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in.

### SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken) \{#Kun_Auditing_AuditingSaveChangesInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken\_}

Called at the start of `DbContext.SaveChangesAsync`.

```csharp
public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
```

#### Parameters

`eventData` [DbContextEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexteventdata)

Contextual information about the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) being used.

`result` [InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

Represents the current result if one exists.
This value will have [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) set to [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if some previous
interceptor suppressed execution by calling [`SuppressWithResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.suppresswithresult).
This value is typically used as the return value for the implementation of this method.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>>

If [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) is false, the EF will continue as normal.
If [`HasResult`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.hasresult) is true, then EF will suppress the operation it
was about to perform and use [`Result`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult-1.result) instead.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in.

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.
