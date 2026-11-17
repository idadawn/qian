> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class AuditFieldsSaveChangesInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Interceptors](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

在业务保存前填充审计字段，保留创建字段，拒绝无法转换的已认证主体。

```csharp
public sealed class AuditFieldsSaveChangesInterceptor : SaveChangesInterceptor, ISaveChangesInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SaveChangesInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) ←
[AuditFieldsSaveChangesInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/auditfieldssavechangesinterceptor.md)

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

### AuditFieldsSaveChangesInterceptor(ICurrentUser, TimeProvider) \{#Kun_EntityFrameworkCore_Interceptors_AuditFieldsSaveChangesInterceptor__ctor_Kun_Core_Security_ICurrentUser_System_TimeProvider\_}

在业务保存前填充审计字段，保留创建字段，拒绝无法转换的已认证主体。

```csharp
public AuditFieldsSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider clock)
```

#### Parameters

`currentUser` ICurrentUser

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### SavingChanges(DbContextEventData, InterceptionResult\<int>) \{#Kun_EntityFrameworkCore_Interceptors_AuditFieldsSaveChangesInterceptor_SavingChanges_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32\_\_}

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

### SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken) \{#Kun_EntityFrameworkCore_Interceptors_AuditFieldsSaveChangesInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken\_}

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
