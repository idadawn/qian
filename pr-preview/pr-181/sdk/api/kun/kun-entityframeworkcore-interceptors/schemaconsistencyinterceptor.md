> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SchemaConsistencyInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Interceptors](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-interceptors/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Fails fast when a [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)'s `DefaultSchema` diverges from the EF Core
`MigrationsHistoryTable` schema. Throws at the first `SaveChangesAsync` so developers
catch mis-wired `UseKunSchema` calls before any business write lands in the wrong schema.

```csharp
public sealed class SchemaConsistencyInterceptor : ISaveChangesInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SchemaConsistencyInterceptor](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-interceptors/schemaconsistencyinterceptor.md)

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

The check runs at most once per interceptor instance (gated by
[`CompareExchange`](https://learn.microsoft.com/dotnet/api/system.threading.interlocked.compareexchange)). Per `kun-gap-closure-v1.md`
M5, the comparison cannot run at `OnModelCreating` time because the relational model is
not yet finalized — `SavingChangesAsync` is the earliest correct hook point.

## Constructors

### SchemaConsistencyInterceptor() \{#Kun_EntityFrameworkCore_Interceptors_SchemaConsistencyInterceptor__ctor}

```csharp
public SchemaConsistencyInterceptor()
```

## Methods

### SavingChanges(DbContextEventData, InterceptionResult\<int>) \{#Kun_EntityFrameworkCore_Interceptors_SchemaConsistencyInterceptor_SavingChanges_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32\_\_}

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

### SavingChangesAsync(DbContextEventData, InterceptionResult\<int>, CancellationToken) \{#Kun_EntityFrameworkCore_Interceptors_SchemaConsistencyInterceptor_SavingChangesAsync_Microsoft_EntityFrameworkCore_Diagnostics_DbContextEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult_System_Int32__System_Threading_CancellationToken\_}

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
