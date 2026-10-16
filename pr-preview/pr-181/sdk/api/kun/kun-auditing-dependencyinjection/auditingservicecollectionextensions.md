> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class AuditingServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-auditing-dependencyinjection/index.md)\
Assembly: Kun.Auditing.dll

DI extension methods for wiring the Kun.Auditing module.

```csharp
public static class AuditingServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditingServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-auditing-dependencyinjection/auditingservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

**Explicit registration (plan A3 path c)**. Auditable entity types must be declared
up front via [AuditingServiceCollectionExtensions.AddAuditable](/pr-preview/pr-181/sdk/api/kun/kun-auditing-dependencyinjection/auditingservicecollectionextensions.md#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddAuditable__1_Microsoft_Extensions_DependencyInjection_IServiceCollection_); the
[AuditableAttribute](/pr-preview/pr-181/sdk/api/kun/kun-auditing/auditableattribute.md) marker on the entity class is not sufficient — it is
docs-only and never scanned at runtime. This design eliminates reflection and keeps
the module AOT-publish-clean.

## Fields

### AuditEventStableId \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AuditEventStableId}

Stable Outbox event id for [AuditEventIntegrationEvent](/pr-preview/pr-181/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md).

```csharp
public const string AuditEventStableId = "kun.audit.event.v1"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### AuditEventV2StableId \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AuditEventV2StableId}

规范字符串租户审计事件的稳定标识；v1 类型仍保留供迁移。

```csharp
public const string AuditEventV2StableId = "kun.audit.event.v2"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### AddAuditable\<TEntity>(IServiceCollection) \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddAuditable__1_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers `TEntity` as an auditable type so that mutations of
it tracked by any [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) will emit
[AuditEventIntegrationEvent](/pr-preview/pr-181/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) on `SaveChangesAsync`. Safe to call
repeatedly for the same type.

```csharp
public static IServiceCollection AddAuditable<TEntity>(this IServiceCollection services) where TEntity : class
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

#### Type Parameters

`TEntity`

Entity class to audit.

### AddAuditingInterceptor(DbContextOptionsBuilder, IServiceProvider) \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddAuditingInterceptor_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_System_IServiceProvider\_}

Convenience method to attach the [AuditingSaveChangesInterceptor](/pr-preview/pr-181/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md) to a
[`DbContextOptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder) via DI resolution.

```csharp
public static DbContextOptionsBuilder AddAuditingInterceptor(this DbContextOptionsBuilder builder, IServiceProvider serviceProvider)
```

#### Parameters

`builder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The options builder.

`serviceProvider` [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider)

Service provider from the `AddDbContext` factory.

#### Returns

[DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The same `builder` for chaining.

#### Remarks

Callers use this inside their `AddDbContext<T>((sp, options) => ...)`
factory, e.g.

```csharp
services.AddDbContext<CatalogDbContext>((sp, options) =>
    options.UseNpgsql(cs)
           .UseKunSchema("catalog")
           .AddAuditingInterceptor(sp));
```

### AddKunAuditing\<TContext>(IServiceCollection) \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddKunAuditing__1_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

规范实体审计入口：AuditLog、Outbox 与业务使用同一 DbContext 和事务。

```csharp
public static IServiceCollection AddKunAuditing<TContext>(this IServiceCollection services) where TContext : KunDbContext
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Type Parameters

`TContext`

### AddKunAuditing(IServiceCollection) \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddKunAuditing_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers audit infrastructure into `services`: the registry, the
default outbox-backed [IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-auditing/iauditsink.md), the EF interceptor, the request-scoped
[AuditContext](/pr-preview/pr-181/sdk/api/kun/kun-auditing/auditcontext.md), and the [AuditContextMiddleware](/pr-preview/pr-181/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md).

```csharp
public static IServiceCollection AddKunAuditing(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

#### Remarks

Consumers must also:

- Call `AddAuditable<TEntity>()` for each auditable entity type.
- Register the stable event id with `AddKunEventBus(b => b.RegisterAuditing())`.
- Attach the interceptor to the relevant DbContext via `options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>())` or use [AuditingServiceCollectionExtensions.AddAuditingInterceptor](/pr-preview/pr-181/sdk/api/kun/kun-auditing-dependencyinjection/auditingservicecollectionextensions.md#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_AddAuditingInterceptor_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_System_IServiceProvider_) on the [`DbContextOptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder).
- Mount [AuditContextMiddleware](/pr-preview/pr-181/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md) into the ASP.NET Core pipeline.

### RegisterAuditing(IEventTypeRegistrar) \{#Kun_Auditing_DependencyInjection_AuditingServiceCollectionExtensions_RegisterAuditing_Kun_EventBus_IEventTypeRegistrar\_}

Registers [AuditEventIntegrationEvent](/pr-preview/pr-181/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) with the Kun event-type registry
under the stable id `kun.audit.event.v1`. Call from inside
`services.AddKunEventBus(b => b.RegisterAuditing())` so the outbox publisher
emits the correct `EventTypeId` for audit messages.

```csharp
public static IEventTypeRegistrar RegisterAuditing(this IEventTypeRegistrar registrar)
```

#### Parameters

`registrar` IEventTypeRegistrar

The event-type registrar.

#### Returns

IEventTypeRegistrar

The same `registrar` for chaining.
