> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class AuditSaveChangesInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

EF Core [`SaveChangesInterceptor`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) that emits one
[AuditLog](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditlog.md) per [IAggregateRoot](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iaggregateroot.md) (or
[IMultiTenant](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/imultitenant.md)) that was added / modified / deleted in
the current `SaveChangesAsync`.

```csharp
[Obsolete("请迁移到 Kun.Auditing 的 AddKunAuditing<TContext>()，在同事务写入 AuditLog 与 Outbox。", DiagnosticId = "KUNAUDIT001")]
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor, ISaveChangesInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SaveChangesInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) ←
[AuditSaveChangesInterceptor](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md)

#### Implements

[ISaveChangesInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor),
[IInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.iinterceptor)

#### Inherited Members

[SaveChangesInterceptor.SavingChanges(DbContextEventData, InterceptionResult\<int>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savingchanges),
[SaveChangesInterceptor.SavedChanges(SaveChangesCompletedEventData, int)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savedchanges),
[SaveChangesInterceptor.SaveChangesFailed(DbContextErrorEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savechangesfailed),
[SaveChangesInterceptor.SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savingchangesasync),
[SaveChangesInterceptor.SavedChangesAsync(SaveChangesCompletedEventData, int, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savedchangesasync),
[SaveChangesInterceptor.SaveChangesFailedAsync(DbContextErrorEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savechangesfailedasync),
[SaveChangesInterceptor.SaveChangesCanceled(DbContextEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savechangescanceled),
[SaveChangesInterceptor.SaveChangesCanceledAsync(DbContextEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.savechangescanceledasync),
[SaveChangesInterceptor.ThrowingConcurrencyException(ConcurrencyExceptionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.throwingconcurrencyexception),
[SaveChangesInterceptor.ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor.throwingconcurrencyexceptionasync),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Change metadata is captured in [AuditSaveChangesInterceptor.SavingChangesAsync](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken_)
(before EF finalises keys, so we have the original / current values)
and replayed into [IAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) after
`SaveChangesAsync` succeeds. This ordering avoids two hazards:

- An audit-writer exception no longer rolls back the business transaction (unless the consumer sets [KunAuditOptions.SwallowWriterExceptions](/pr-preview/pr-50/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md#Kun_Audit_DependencyInjection_KunAuditOptions_SwallowWriterExceptions) to [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool)).
- Server-generated primary keys (PostgreSQL `IDENTITY`, DB-side GUIDs) are populated on the entity by the time we read them, so [AuditLog.ResourceId](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_ResourceId) is never blank.

No reflection is used: the interceptor only inspects
[`ChangeTracker`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.changetracker) entries and Kun.Core marker interfaces.
Payload JSON is built with [`Utf8JsonWriter`](https://learn.microsoft.com/dotnet/api/system.text.json.utf8jsonwriter) so the
interceptor stays portable to a future AOT-candidate module.

## Constructors

### AuditSaveChangesInterceptor(IAuditWriter, ITenantContext, ICurrentUser, ISystemClock, IOptions\<KunAuditOptions>, ILogger\<AuditSaveChangesInterceptor>) \{#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor__ctor_Kun_Audit_Abstractions_IAuditWriter_Kun_Core_MultiTenancy_ITenantContext_Kun_Core_Security_ICurrentUser_Kun_Core_Time_ISystemClock_Microsoft_Extensions_Options_IOptions_Kun_Audit_DependencyInjection_KunAuditOptions__Microsoft_Extensions_Logging_ILogger_Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor\_\_}

Creates a new [AuditSaveChangesInterceptor](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md).

```csharp
public AuditSaveChangesInterceptor(IAuditWriter writer, ITenantContext tenantContext, ICurrentUser currentUser, ISystemClock clock, IOptions<KunAuditOptions> options, ILogger<AuditSaveChangesInterceptor> logger)
```

#### Parameters

`writer` IAuditWriter

`tenantContext` ITenantContext

`currentUser` ICurrentUser

`clock` ISystemClock

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<KunAuditOptions>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[AuditSaveChangesInterceptor](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md)>

## Methods

### SaveChangesFailed(DbContextErrorEventData) \{#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor_SaveChangesFailed_Microsoft_EntityFrameworkCore_Diagnostics_DbContextErrorEventData\_}

Called when an exception has been thrown in `DbContext.SaveChanges`.

```csharp
public override void SaveChangesFailed(DbContextErrorEventData eventData)
```

#### Parameters

`eventData` [DbContextErrorEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexterroreventdata)

Contextual information about the failure.

### SaveChangesFailedAsync(DbContextErrorEventData, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor_SaveChangesFailedAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextErrorEventData_System_Threading_CancellationToken\_}

Called when an exception has been thrown in `DbContext.SaveChangesAsync`.

```csharp
public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
```

#### Parameters

`eventData` [DbContextErrorEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexterroreventdata)

Contextual information about the failure.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

A [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) representing the asynchronous operation.

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.

### SavedChangesAsync(SaveChangesCompletedEventData, int, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor_SavedChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_SaveChangesCompletedEventData_System_Int32_System_Threading_CancellationToken\_}

Called at the end of `DbContext.SaveChangesAsync`.

```csharp
public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
```

#### Parameters

`eventData` [SaveChangesCompletedEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangescompletedeventdata)

Contextual information about the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) being used.

`result` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The result of the call to `DbContext.SaveChangesAsync`.
This value is typically used as the return value for the implementation of this method.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

The result that EF will use.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in.

#### Remarks

This method is still called if an interceptor suppressed creation of a command in [`SavingChangesAsync`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor.savingchangesasync).
In this case, `result` is the result returned by [`SavingChangesAsync`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor.savingchangesasync).

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.

### SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_AuditSaveChangesInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken\_}

Called at the start of `DbContext.SaveChangesAsync`.

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
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
