> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IEventTypeRegistrar

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EventBus)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

Fluent builder for populating the [IEventTypeRegistry](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistry.md).

```csharp
public interface IEventTypeRegistrar
```

## Methods

### Register\<TEvent>(string) \{#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String\_}

Registers a mapping between `eventType` token and `TEvent`.

```csharp
IEventTypeRegistrar Register<TEvent>(string eventType) where TEvent : class
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[IEventTypeRegistrar](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md)

#### Type Parameters

`TEvent`

#### Remarks

Serialization falls back to reflection-based `System.Text.Json` defaults. AOT-targeting
consumers should prefer the
[IEventTypeRegistrar.Register](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__) overload instead to thread a
source-generated [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) through the outbox.

### Register\<TEvent>(string, JsonTypeInfo\<TEvent>) \{#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0\_\_}

Registers a mapping between `eventType` token and
`TEvent`, plus the source-generated
[`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) used to serialize outbox payloads without runtime reflection.

```csharp
IEventTypeRegistrar Register<TEvent>(string eventType, JsonTypeInfo<TEvent> typeInfo) where TEvent : class
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable event-id token (e.g. `order.placed.v1`).

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TEvent>

JsonTypeInfo for `TEvent`, typically obtained from a
`[JsonSerializable(typeof(TEvent))]`-decorated [`JsonSerializerContext`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext).

#### Returns

[IEventTypeRegistrar](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md)

#### Type Parameters

`TEvent`

### Register\<TEvent>() \{#Kun_EventBus_IEventTypeRegistrar_Register__1}

Registers `TEvent` using the token declared on its
[IntegrationEventIdAttribute](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/integrationeventidattribute.md). Throws
[`InvalidOperationException`](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) when the attribute is absent.

```csharp
[RequiresUnreferencedCode("Reads [IntegrationEventId] via reflection on typeof(TEvent). Under trimming, IntegrationEventIdAttribute instances must be preserved. Use Register<TEvent>(string) to avoid reflection entirely.")]
IEventTypeRegistrar Register<TEvent>() where TEvent : class
```

#### Returns

[IEventTypeRegistrar](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md)

#### Type Parameters

`TEvent`

#### Remarks

Reads the attribute reflectively once at DI-registration time; the resulting
registry is the same O(1) [EventTypeRegistry](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/eventtyperegistry.md) used at steady state.
AOT-strict callers can stick with [IEventTypeRegistrar.Register](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_) or
[IEventTypeRegistrar.Register](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__) to avoid the reflection entirely.
