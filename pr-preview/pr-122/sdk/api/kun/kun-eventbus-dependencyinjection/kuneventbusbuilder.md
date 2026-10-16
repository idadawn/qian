> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunEventBusBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-dependencyinjection/index.md)\
Assembly: Kun.EventBus.dll

Fluent builder for configuring the Kun event bus.

```csharp
public sealed class KunEventBusBuilder : IEventTypeRegistrar
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunEventBusBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusbuilder.md)

#### Implements

IEventTypeRegistrar

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddHandler\<TEvent, THandler>() \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_AddHandler__2}

Registers an [IIntegrationEventHandler\<TEvent>](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/iintegrationeventhandler-1.md) for `TEvent`.

```csharp
public KunEventBusBuilder AddHandler<TEvent, THandler>() where TEvent : class where THandler : class, IIntegrationEventHandler<TEvent>
```

#### Returns

[KunEventBusBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusbuilder.md)

#### Type Parameters

`TEvent`

`THandler`

### Register\<TEvent>(string) \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_Register__1_System_String\_}

Registers a mapping between `eventType` token and `TEvent`.

```csharp
public IEventTypeRegistrar Register<TEvent>(string eventType) where TEvent : class
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

IEventTypeRegistrar

#### Type Parameters

`TEvent`

#### Remarks

Serialization falls back to reflection-based `System.Text.Json` defaults. AOT-targeting
consumers should prefer the
[IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__) overload instead to thread a
source-generated [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) through the outbox.

### Register\<TEvent>(string, JsonTypeInfo\<TEvent>) \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0\_\_}

Registers a mapping between `eventType` token and
`TEvent`, plus the source-generated
[`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) used to serialize outbox payloads without runtime reflection.

```csharp
public IEventTypeRegistrar Register<TEvent>(string eventType, JsonTypeInfo<TEvent> typeInfo) where TEvent : class
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable event-id token (e.g. `order.placed.v1`).

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TEvent>

JsonTypeInfo for `TEvent`, typically obtained from a
`[JsonSerializable(typeof(TEvent))]`-decorated [`JsonSerializerContext`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext).

#### Returns

IEventTypeRegistrar

#### Type Parameters

`TEvent`

### Register\<TEvent>() \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_Register__1}

Registers `TEvent` using the token declared on its
[IntegrationEventIdAttribute](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/integrationeventidattribute.md). Throws
[`InvalidOperationException`](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) when the attribute is absent.

```csharp
[RequiresUnreferencedCode("Reads [IntegrationEventId] via reflection on typeof(TEvent). Under trimming, IntegrationEventIdAttribute instances must be preserved. Use Register<TEvent>(string) to avoid reflection entirely.")]
public IEventTypeRegistrar Register<TEvent>() where TEvent : class
```

#### Returns

IEventTypeRegistrar

#### Type Parameters

`TEvent`

#### Remarks

Reads the attribute reflectively once at DI-registration time; the resulting
registry is the same O(1) [IEventTypeRegistry](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistry.md) used at steady state.
AOT-strict callers can stick with [IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_) or
[IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__) to avoid the reflection entirely.

### UseInMemoryIdempotency() \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_UseInMemoryIdempotency}

Registers the in-memory idempotency store (testing only).

```csharp
public KunEventBusBuilder UseInMemoryIdempotency()
```

#### Returns

[KunEventBusBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusbuilder.md)

### UseRedisIdempotency() \{#Kun_EventBus_DependencyInjection_KunEventBusBuilder_UseRedisIdempotency}

Registers the Redis idempotency store.
The caller must register `IConnectionMultiplexer` separately
(e.g. via `services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(...))`).

```csharp
public KunEventBusBuilder UseRedisIdempotency()
```

#### Returns

[KunEventBusBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusbuilder.md)
