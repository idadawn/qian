> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunAuditServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/index.md)\
Assembly: Kun.Audit.dll

Service-collection extensions for Kun.Audit. Wires up both halves of the module:
the writer/reader abstraction ([IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) / [IAuditReader](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditreader.md))
and the tamper-evident hash-chain primitives ([IAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md) /
[IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md) / [AuditLogPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)).

```csharp
public static class KunAuditServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunAuditServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddAuditBehavior\<TRequest, TResponse>(KunMediatorBuilder) \{#Kun_Audit_DependencyInjection_KunAuditServiceCollectionExtensions_AddAuditBehavior__2_Kun_Mediator_KunMediatorBuilder\_}

Registers [AuditLogPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md) for the
given request/response pair on a [KunMediatorBuilder](/pr-preview/pr-181/sdk/api/kun/kun-mediator/kunmediatorbuilder.md). Call once
per command type that should be audited; queries left unregistered will not
generate audit noise.

```csharp
public static KunMediatorBuilder AddAuditBehavior<TRequest, TResponse>(this KunMediatorBuilder builder) where TRequest : IRequest<TResponse>
```

#### Parameters

`builder` KunMediatorBuilder

#### Returns

KunMediatorBuilder

#### Type Parameters

`TRequest`

`TResponse`

### AddKunAudit(IServiceCollection, Action\<KunAuditOptions>?) \{#Kun_Audit_DependencyInjection_KunAuditServiceCollectionExtensions_AddKunAudit_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Audit_DependencyInjection_KunAuditOptions\_\_}

Registers the Kun.Audit defaults: [KunAuditOptions](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md), a
[NullAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/nullauditwriter.md) writer fallback, plus the in-memory hash-chain
state/sink and a [`TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider). All registrations use
`TryAdd` so downstream callers can override (e.g. `UseEventBus()`
replaces the writer; production deployments replace the in-memory chain
state/sink with durable implementations).

```csharp
public static KunAuditBuilder AddKunAudit(this IServiceCollection services, Action<KunAuditOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunAuditOptions](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md)>?

Optional options configurer.

#### Returns

[KunAuditBuilder](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md)

A [KunAuditBuilder](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md) for further wiring.

### UseEventBus(KunAuditBuilder) \{#Kun_Audit_DependencyInjection_KunAuditServiceCollectionExtensions_UseEventBus_Kun_Audit_DependencyInjection_KunAuditBuilder\_}

Replaces the registered [IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) with
[EventBusAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md), routing audit entries through
the consumer's Outbox as integration events.

```csharp
[Obsolete("kun.audit.record.v1 已废弃；实体审计请迁移到 Kun.Auditing.AddKunAuditing<TContext>()。", DiagnosticId = "KUNAUDIT001")]
public static KunAuditBuilder UseEventBus(this KunAuditBuilder builder)
```

#### Parameters

`builder` [KunAuditBuilder](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md)

#### Returns

[KunAuditBuilder](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md)

#### Remarks

The consumer is responsible for registering [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md)
in their `IEventTypeRegistry` under the stable id
[EventBusAuditWriter.EventTypeId](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md#Kun_Audit_EventBusAuditWriter_EventTypeId) and for providing a
source-generated `JsonTypeInfo<AuditLog>`.
