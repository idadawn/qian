> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class TenantRlsConnectionInterceptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Interceptors](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Defence-in-depth interceptor that pushes the current [ITenantContext.TenantId](/pr-preview/pr-122/sdk/api/kun/kun-core-multitenancy/itenantcontext.md#Kun_Core_MultiTenancy_ITenantContext_TenantId)
into the PostgreSQL session variable `app.current_tenant_id` on every
connection open, and clears it on connection close. Used in combination with
Row-Level Security policies on the database side — EF Core's global query
filter remains the first line of defence; this interceptor is the second.

```csharp
public sealed class TenantRlsConnectionInterceptor : DbConnectionInterceptor, IDbConnectionInterceptor, IInterceptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DbConnectionInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor) ←
[TenantRlsConnectionInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlsconnectioninterceptor.md)

#### Implements

[IDbConnectionInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.idbconnectioninterceptor),
[IInterceptor](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.iinterceptor)

#### Inherited Members

[DbConnectionInterceptor.ConnectionCreating(ConnectionCreatingEventData, InterceptionResult\<DbConnection>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectioncreating),
[DbConnectionInterceptor.ConnectionCreated(ConnectionCreatedEventData, DbConnection)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectioncreated),
[DbConnectionInterceptor.ConnectionOpening(DbConnection, ConnectionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionopening),
[DbConnectionInterceptor.ConnectionOpeningAsync(DbConnection, ConnectionEventData, InterceptionResult, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionopeningasync),
[DbConnectionInterceptor.ConnectionOpened(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionopened),
[DbConnectionInterceptor.ConnectionOpenedAsync(DbConnection, ConnectionEndEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionopenedasync),
[DbConnectionInterceptor.ConnectionClosing(DbConnection, ConnectionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionclosing),
[DbConnectionInterceptor.ConnectionClosingAsync(DbConnection, ConnectionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionclosingasync),
[DbConnectionInterceptor.ConnectionClosed(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionclosed),
[DbConnectionInterceptor.ConnectionClosedAsync(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionclosedasync),
[DbConnectionInterceptor.ConnectionDisposing(DbConnection, ConnectionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectiondisposing),
[DbConnectionInterceptor.ConnectionDisposingAsync(DbConnection, ConnectionEventData, InterceptionResult)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectiondisposingasync),
[DbConnectionInterceptor.ConnectionDisposed(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectiondisposed),
[DbConnectionInterceptor.ConnectionDisposedAsync(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectiondisposedasync),
[DbConnectionInterceptor.ConnectionFailed(DbConnection, ConnectionErrorEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionfailed),
[DbConnectionInterceptor.ConnectionFailedAsync(DbConnection, ConnectionErrorEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectionfailedasync),
[DbConnectionInterceptor.ConnectionCanceled(DbConnection, ConnectionEndEventData)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectioncanceled),
[DbConnectionInterceptor.ConnectionCanceledAsync(DbConnection, ConnectionEndEventData, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.dbconnectioninterceptor.connectioncanceledasync),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Only activates when the underlying [`DbConnection`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection) is an Npgsql
connection; for other providers (MySQL, SQL Server) the interceptor is a
no-op, because the `SET app.current_tenant_id = ...` syntax is
PostgreSQL-specific. The check is nominal (namespace-prefix on the connection
type) so this module can remain free of a direct Npgsql package dependency.

SQL injection guard: `SET` statements in PostgreSQL cannot be parameterised,
so the tenant id is inlined into the command text. We therefore refuse to emit
the command unless the value matches the strict whitelist
[TenantRlsConnectionInterceptor.SafeTenantIdPattern](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlsconnectioninterceptor.md#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_SafeTenantIdPattern). A business tenant id that does not match
(spaces, quotes, semicolons, etc.) will throw instead of being silently coerced
— fail loudly rather than hand a crafted value to `SET`.

Registered as Scoped by [KunEntityFrameworkCoreServiceCollectionExtensions.AddKunEntityFrameworkCore](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-dependencyinjection/kunentityframeworkcoreservicecollectionextensions.md#Kun_EntityFrameworkCore_DependencyInjection_KunEntityFrameworkCoreServiceCollectionExtensions_AddKunEntityFrameworkCore_Microsoft_Extensions_DependencyInjection_IServiceCollection_);
callers opt in by adding the interceptor to their `DbContextOptionsBuilder`:

```csharp
services.AddDbContext<MyDbContext>((sp, opts) =>
{
    opts.UseNpgsql(connectionString);
    opts.AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>());
});
```

## Constructors

### TenantRlsConnectionInterceptor(ITenantContext, ILogger\<TenantRlsConnectionInterceptor>) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor__ctor_Kun_Core_MultiTenancy_ITenantContext_Microsoft_Extensions_Logging_ILogger_Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor\_\_}

Initializes a new instance of the interceptor.

```csharp
public TenantRlsConnectionInterceptor(ITenantContext tenantContext, ILogger<TenantRlsConnectionInterceptor> logger)
```

#### Parameters

`tenantContext` ITenantContext

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[TenantRlsConnectionInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlsconnectioninterceptor.md)>

## Fields

### SafeTenantIdPattern \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_SafeTenantIdPattern}

Whitelist for tenant-id values allowed inside `SET app.current_tenant_id = '...'`.
ASCII letters, digits, hyphen, underscore, dot; 1..128 chars. Covers GUIDs,
slugs, and short numeric ids while rejecting any value that could escape
the single-quoted literal.

```csharp
public const string SafeTenantIdPattern = "^[A-Za-z0-9._-]{1,128}$"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ConnectionClosing(DbConnection, ConnectionEventData, InterceptionResult) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_ConnectionClosing_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_ConnectionEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult\_}

Called just before EF intends to call [`Close`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.close).

```csharp
public override InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [ConnectionEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.connectioneventdata)

Contextual information about the connection.

`result` [InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult)

Represents the current result if one exists.
This value will have [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) set to [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if some previous
interceptor suppressed execution by calling [`Suppress`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.suppress).
This value is typically used as the return value for the implementation of this method.

#### Returns

[InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult)

If [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) is [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), then EF will continue as normal.
If [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) is [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), then EF will suppress the operation
it was about to perform.
An implementation of this method for any interceptor that is not attempting to suppress
the operation is to return the `result` value passed in.

### ConnectionClosingAsync(DbConnection, ConnectionEventData, InterceptionResult) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_ConnectionClosingAsync_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_ConnectionEventData_Microsoft_EntityFrameworkCore_Diagnostics_InterceptionResult\_}

Called just before EF intends to call [`CloseAsync`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.closeasync) in an async context.

```csharp
public override ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [ConnectionEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.connectioneventdata)

Contextual information about the connection.

`result` [InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult)

Represents the current result if one exists.
This value will have [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) set to [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if some previous
interceptor suppressed execution by calling [`Suppress`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.suppress).
This value is typically used as the return value for the implementation of this method.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[InterceptionResult](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult)>

If [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) is [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), then EF will continue as normal.
If [`IsSuppressed`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.interceptionresult.issuppressed) is [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), then EF will suppress the operation
it was about to perform.
An implementation of this method for any interceptor that is not attempting to suppress
the operation is to return the `result` value passed in.

### ConnectionOpened(DbConnection, ConnectionEndEventData) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_ConnectionOpened_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_ConnectionEndEventData\_}

Called just after EF has called [`Open`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.open).

```csharp
public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [ConnectionEndEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.connectionendeventdata)

Contextual information about the connection.

### ConnectionOpenedAsync(DbConnection, ConnectionEndEventData, CancellationToken) \{#Kun_EntityFrameworkCore_Interceptors_TenantRlsConnectionInterceptor_ConnectionOpenedAsync_System_Data_Common_DbConnection_Microsoft_EntityFrameworkCore_Diagnostics_ConnectionEndEventData_System_Threading_CancellationToken\_}

Called just after EF has called [`OpenAsync`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection.openasync).

```csharp
public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
```

#### Parameters

`connection` [DbConnection](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection)

The connection.

`eventData` [ConnectionEndEventData](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.connectionendeventdata)

Contextual information about the connection.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

A [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) to observe while waiting for the task to complete.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

A [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) representing the asynchronous operation.

#### Exceptions

[OperationCanceledException](https://learn.microsoft.com/dotnet/api/system.operationcanceledexception)

If the [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) is canceled.
