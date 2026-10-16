> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class IntegrationEventOutboxAccumulator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

[IIntegrationEventBus](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/iintegrationeventbus.md) implementation that serializes integration events and
appends them to the [IntegrationEventBuffer](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/integrationeventbuffer.md) with current trace and tenant context.
The buffer is flushed atomically by [EfUnitOfWork](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/efunitofwork.md) during `SaveChangesAsync`.

```csharp
public sealed class IntegrationEventOutboxAccumulator : IIntegrationEventBus, IOutboxWriter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[IntegrationEventOutboxAccumulator](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/integrationeventoutboxaccumulator.md)

#### Implements

IIntegrationEventBus,
IOutboxWriter

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### IntegrationEventOutboxAccumulator(IntegrationEventBuffer, IEventTypeRegistry, ITenantContext) \{#Kun_EntityFrameworkCore_IntegrationEventOutboxAccumulator__ctor_Kun_EventBus_Outbox_IntegrationEventBuffer_Kun_EventBus_IEventTypeRegistry_Kun_Core_MultiTenancy_ITenantContext\_}

Creates a new [IntegrationEventOutboxAccumulator](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/integrationeventoutboxaccumulator.md).

```csharp
public IntegrationEventOutboxAccumulator(IntegrationEventBuffer buffer, IEventTypeRegistry registry, ITenantContext tenantContext)
```

#### Parameters

`buffer` IntegrationEventBuffer

`registry` IEventTypeRegistry

`tenantContext` ITenantContext

## Methods

### Enqueue\<TEvent>(TEvent) \{#Kun_EntityFrameworkCore_IntegrationEventOutboxAccumulator_Enqueue__1___0\_}

Enqueues an integration event for atomic commit with the pending DbContext change set.
Produces the same [OutboxMessage](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) shape as
[IIntegrationEventBus.Publish](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/iintegrationeventbus.md#Kun_EventBus_IIntegrationEventBus_Publish__1___0_): stable event id via
[IEventTypeRegistry](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/ieventtyperegistry.md), current tenant and trace context captured.

```csharp
[RequiresUnreferencedCode("Delegates to Publish<TEvent>; see Publish for AOT-safety requirements.")]
[RequiresDynamicCode("Delegates to Publish<TEvent>; see Publish for AOT-safety requirements.")]
public void Enqueue<TEvent>(TEvent @event) where TEvent : class
```

#### Parameters

`event` TEvent

The event instance to enqueue. Must not be [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

#### Type Parameters

`TEvent`

The integration event type. Must be registered via [IEventTypeRegistrar](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md).

### Publish\<TEvent>(TEvent) \{#Kun_EntityFrameworkCore_IntegrationEventOutboxAccumulator_Publish__1___0\_}

Stages `event` as an outbox message within the current unit-of-work.

```csharp
[RequiresUnreferencedCode("Falls back to reflection-based JSON serialization when the event type was registered without a source-generated JsonTypeInfo. AOT consumers must use IEventTypeRegistrar.Register<TEvent>(string, JsonTypeInfo<TEvent>) at registration time.")]
[RequiresDynamicCode("Falls back to reflection-based JSON serialization when the event type was registered without a source-generated JsonTypeInfo.")]
public void Publish<TEvent>(TEvent @event) where TEvent : class
```

#### Parameters

`event` TEvent

#### Type Parameters

`TEvent`
