> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class TenantRlsTransactionInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Interceptors](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Emits `set_config('app.current_tenant_id', @tenant, true)` at the start of every
EF Core transaction so PostgreSQL Row-Level Security policies can use
`current_setting('app.current_tenant_id')` to enforce tenant isolation even when a
caller bypasses the global query filter (e.g. `IgnoreQueryFilters()`, admin scenarios).

```csharp
public sealed class TenantRlsTransactionInterceptor : DbTransactionInterceptor, IDbTransactionInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DbTransactionInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor) ←
[TenantRlsTransactionInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlstransactioninterceptor.md)

#### Implements

[IDbTransactionInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbtransactioninterceptor),
[IInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.iinterceptor)

#### Inherited Members

[DbTransactionInterceptor.TransactionStarting(DbConnection, TransactionStartingEventData, InterceptionResult\<DbTransaction>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionstarting),
[DbTransactionInterceptor.TransactionStarted(DbConnection, TransactionEndEventData, DbTransaction)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionstarted),
[DbTransactionInterceptor.TransactionStartingAsync(DbConnection, TransactionStartingEventData, InterceptionResult\<DbTransaction>, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionstartingasync),
[DbTransactionInterceptor.TransactionStartedAsync(DbConnection, TransactionEndEventData, DbTransaction, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionstartedasync),
[DbTransactionInterceptor.TransactionUsed(DbConnection, TransactionEventData, DbTransaction)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionused),
[DbTransactionInterceptor.TransactionUsedAsync(DbConnection, TransactionEventData, DbTransaction, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionusedasync),
[DbTransactionInterceptor.TransactionCommitting(DbTransaction, TransactionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactioncommitting),
[DbTransactionInterceptor.TransactionCommitted(DbTransaction, TransactionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactioncommitted),
[DbTransactionInterceptor.TransactionCommittingAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactioncommittingasync),
[DbTransactionInterceptor.TransactionCommittedAsync(DbTransaction, TransactionEndEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactioncommittedasync),
[DbTransactionInterceptor.TransactionRollingBack(DbTransaction, TransactionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionrollingback),
[DbTransactionInterceptor.TransactionRolledBack(DbTransaction, TransactionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionrolledback),
[DbTransactionInterceptor.TransactionRollingBackAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionrollingbackasync),
[DbTransactionInterceptor.TransactionRolledBackAsync(DbTransaction, TransactionEndEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionrolledbackasync),
[DbTransactionInterceptor.CreatingSavepoint(DbTransaction, TransactionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.creatingsavepoint),
[DbTransactionInterceptor.CreatedSavepoint(DbTransaction, TransactionEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.createdsavepoint),
[DbTransactionInterceptor.CreatingSavepointAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.creatingsavepointasync),
[DbTransactionInterceptor.CreatedSavepointAsync(DbTransaction, TransactionEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.createdsavepointasync),
[DbTransactionInterceptor.RollingBackToSavepoint(DbTransaction, TransactionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.rollingbacktosavepoint),
[DbTransactionInterceptor.RolledBackToSavepoint(DbTransaction, TransactionEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.rolledbacktosavepoint),
[DbTransactionInterceptor.RollingBackToSavepointAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.rollingbacktosavepointasync),
[DbTransactionInterceptor.RolledBackToSavepointAsync(DbTransaction, TransactionEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.rolledbacktosavepointasync),
[DbTransactionInterceptor.ReleasingSavepoint(DbTransaction, TransactionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.releasingsavepoint),
[DbTransactionInterceptor.ReleasedSavepoint(DbTransaction, TransactionEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.releasedsavepoint),
[DbTransactionInterceptor.ReleasingSavepointAsync(DbTransaction, TransactionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.releasingsavepointasync),
[DbTransactionInterceptor.ReleasedSavepointAsync(DbTransaction, TransactionEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.releasedsavepointasync),
[DbTransactionInterceptor.TransactionFailed(DbTransaction, TransactionErrorEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionfailed),
[DbTransactionInterceptor.TransactionFailedAsync(DbTransaction, TransactionErrorEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbtransactioninterceptor.transactionfailedasync),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Hooks `TransactionStartedAsync` / `TransactionStarted` rather than
`ConnectionOpenedAsync`: PgBouncer in transaction-pooling mode recycles the backend
connection at transaction boundaries, so setting a GUC outside an explicit transaction
would leak across tenants. Scoping to `SET LOCAL` (equivalently
`set_config(..., is_local=true)`) ties the setting to the current transaction.

Uses `set_config(...)` instead of `SET LOCAL app.current_tenant_id = '...'` because
Postgres's `SET` utility statement does not accept parameter binding; the function form
does, keeping the tenant id out of the SQL text (defence against log leakage / injection).

No-op when [ITenantContext.TenantId](/pr-preview/pr-122/sdk/api/kun/kun-core-multitenancy/itenantcontext.md#Kun_Core_MultiTenancy_ITenantContext_TenantId) is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) or empty
(background jobs / setup scripts). RLS policies must be written to accept
`current_setting('app.current_tenant_id', true) IS NULL` as the unscoped path if those
flows need to read cross-tenant data; otherwise they must set the GUC manually.

## Constructors

### TenantRlsTransactionInterceptor(ITenantContext, ILogger\<TenantRlsTransactionInterceptor>?) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsTransactionInterceptor__ctor_Kun_Core_MultiTenancy_ITenantContext_Microsoft_Extensions_Logging_ILogger_Kun_EntityFrameworkCore_Interceptors_TenantRlsTransactionInterceptor\_\_}

Creates a new [TenantRlsTransactionInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlstransactioninterceptor.md).

```csharp
public TenantRlsTransactionInterceptor(ITenantContext tenantContext, ILogger<TenantRlsTransactionInterceptor>? logger = null)
```

#### Parameters

`tenantContext` ITenantContext

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[TenantRlsTransactionInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlstransactioninterceptor.md)>?

## Methods

### TransactionStarted(DbConnection, TransactionEndEventData, DbTransaction) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsTransactionInterceptor_TransactionStarted_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_TransactionEndEventData_System_Data_Common_DbTransaction\_}

Called immediately after EF calls [`BeginTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.begintransaction).

```csharp
public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [TransactionEndEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.transactionendeventdata)

Contextual information about connection and transaction.

`result` [DbTransaction](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction)

The result of the call to [`BeginTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.begintransaction).
This value is typically used as the return value for the implementation of this method.

#### Returns

[DbTransaction](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction)

The result that EF will use.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in.

#### Remarks

This method is still called if an interceptor suppressed creation in [`TransactionStarting`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbtransactioninterceptor.transactionstarting).
In this case, `result` is the result returned by [`TransactionStarting`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbtransactioninterceptor.transactionstarting).

### TransactionStartedAsync(DbConnection, TransactionEndEventData, DbTransaction, CancellationToken) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsTransactionInterceptor_TransactionStartedAsync_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_TransactionEndEventData_System_Data_Common_DbTransaction_System_Threading_CancellationToken\_}

Called immediately after EF calls [`BeginTransactionAsync`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.begintransactionasync).

```csharp
public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [TransactionEndEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.transactionendeventdata)

Contextual information about connection and transaction.

`result` [DbTransaction](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction)

The result of the call to
[`BeginTransactionAsync`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.begintransactionasync).
This value is typically used as the return value for the implementation of this method.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[DbTransaction](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction)>

A [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) providing the result that EF will use.
An implementation of this method for any interceptor that is not attempting to change the result
is to return the `result` value passed in, often using [`FromResult`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.fromresult--1)

#### Remarks

This method is still called if an interceptor suppressed creation in [`TransactionStarting`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbtransactioninterceptor.transactionstarting).
In this case, `result` is the result returned by [`TransactionStarting`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbtransactioninterceptor.transactionstarting).

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.
