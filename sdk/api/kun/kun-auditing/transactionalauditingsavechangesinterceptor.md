> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class TransactionalAuditingSaveChangesInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing](/sdk/api/kun/kun-auditing/index.md)\
Assembly: Kun.Auditing.dll

在业务上下文中同时暂存 AuditLog 与 Outbox，任何一方失败均使整个保存失败。

```csharp
public sealed class TransactionalAuditingSaveChangesInterceptor : SaveChangesInterceptor, ISaveChangesInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SaveChangesInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) ←
[TransactionalAuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/transactionalauditingsavechangesinterceptor.md)

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

## Constructors

### TransactionalAuditingSaveChangesInterceptor(IAuditableEntityRegistry, ITenantContext, ICurrentUser, AuditContext, TimeProvider) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor__ctor_Kun_Auditing_IAuditableEntityRegistry_Kun_Core_MultiTenancy_ITenantContext_Kun_Core_Security_ICurrentUser_Kun_Auditing_AuditContext_System_TimeProvider\_}

在业务上下文中同时暂存 AuditLog 与 Outbox，任何一方失败均使整个保存失败。

```csharp
public TransactionalAuditingSaveChangesInterceptor(IAuditableEntityRegistry registry, ITenantContext tenant, ICurrentUser user, AuditContext auditContext, TimeProvider clock)
```

#### Parameters

`registry` [IAuditableEntityRegistry](/sdk/api/kun/kun-auditing/iauditableentityregistry.md)

`tenant` ITenantContext

`user` ICurrentUser

`auditContext` [AuditContext](/sdk/api/kun/kun-auditing/auditcontext.md)

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### SaveChangesCanceled(DbContextEventData) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SaveChangesCanceled_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData\_}

Called when `DbContext.SaveChanges` was canceled.

```csharp
public override void SaveChangesCanceled(DbContextEventData eventData)
```

#### Parameters

`eventData` [DbContextEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexteventdata)

Contextual information about the failure.

### SaveChangesCanceledAsync(DbContextEventData, CancellationToken) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SaveChangesCanceledAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_System_Threading_CancellationToken\_}

Called when `DbContext.SaveChangesAsync` was canceled.

```csharp
public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
```

#### Parameters

`eventData` [DbContextEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexteventdata)

Contextual information about the failure.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

A [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) representing the asynchronous operation.

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.

### SaveChangesFailed(DbContextErrorEventData) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SaveChangesFailed_Microsoft_EntityFrameworkCore_Diagnostics_DbContextErrorEventData\_}

Called when an exception has been thrown in `DbContext.SaveChanges`.

```csharp
public override void SaveChangesFailed(DbContextErrorEventData eventData)
```

#### Parameters

`eventData` [DbContextErrorEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbcontexterroreventdata)

Contextual information about the failure.

### SaveChangesFailedAsync(DbContextErrorEventData, CancellationToken) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SaveChangesFailedAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextErrorEventData_System_Threading_CancellationToken\_}

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

### SavedChanges(SaveChangesCompletedEventData, int) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SavedChanges_Microsoft_EntityFrameworkCore_Diagnostics_SaveChangesCompletedEventData_System_Int32\_}

Called at the end of `DbContext.SaveChanges`.

```csharp
public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
```

#### Parameters

`eventData` [SaveChangesCompletedEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangescompletedeventdata)

Contextual information about the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) being used.

`result` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The result of the call to `DbContext.SaveChanges`.
This value is typically used as the return value for the implementation of this method.

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

The result that EF will use.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in.

#### Remarks

This method is still called if an interceptor suppressed creation of a command in [`SavingChanges`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor.savingchanges).
In this case, `result` is the result returned by [`SavingChanges`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor.savingchanges).

### SavedChangesAsync(SaveChangesCompletedEventData, int, CancellationToken) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SavedChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_SaveChangesCompletedEventData_System_Int32_System_Threading_CancellationToken\_}

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

### SavingChanges(DbContextEventData, InterceptionResult\<int>) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SavingChanges_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32\_\_}

Called at the start of `DbContext.SaveChanges`.

```csharp
public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
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

### SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken) \{#Kun_Auditing_TransactionalAuditingSaveChangesInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken\_}

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
