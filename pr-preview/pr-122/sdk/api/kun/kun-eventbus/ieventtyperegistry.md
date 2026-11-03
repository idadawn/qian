> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IEventTypeRegistry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.Abstractions.dll

Maps stable event-type tokens to their CLR types at runtime.

```csharp
public interface IEventTypeRegistry
```

## Methods

### GetAll() \{#Kun_EventBus_IEventTypeRegistry_GetAll}

Enumerates every `(eventId, clrType)` pair in the registry, ordered by
`eventId` (ordinal, case-sensitive) for stable output. Intended primarily for
tooling — JSON-Schema export, registry dumps, diagnostic inspection. Consumers on the
hot path should continue to use [IEventTypeRegistry.Resolve](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistry.md#Kun_EventBus_IEventTypeRegistry_Resolve_System_String_) or
[IEventTypeRegistry.GetEventType](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistry.md#Kun_EventBus_IEventTypeRegistry_GetEventType_System_Type_).

```csharp
IReadOnlyCollection<(string EventId, Type ClrType)> GetAll()
```

#### Returns

[IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<([string](https://learn.microsoft.com/dotnet/api/system.string) [EventId](https://learn.microsoft.com/dotnet/api/system.valuetuple-system.string,system.type-.eventid), [Type](https://learn.microsoft.com/dotnet/api/system.type) [ClrType](https://learn.microsoft.com/dotnet/api/system.valuetuple-system.string,system.type-.clrtype))>

### GetEventType(Type) \{#Kun_EventBus_IEventTypeRegistry_GetEventType_System_Type\_}

Resolves the stable token for a CLR type.

```csharp
string? GetEventType(Type clrType)
```

#### Parameters

`clrType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)?

[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the type is not registered.

### GetJsonTypeInfo(Type) \{#Kun_EventBus_IEventTypeRegistry_GetJsonTypeInfo_System_Type\_}

Returns the source-generated [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo) registered for
`clrType` via [IEventTypeRegistrar.Register](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__).
Returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the type was registered via the token-only overload
— in that case, callers fall back to reflection-based serialization (not AOT-safe).

```csharp
JsonTypeInfo? GetJsonTypeInfo(Type clrType)
```

#### Parameters

`clrType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

#### Returns

[JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo)?

### Resolve(string) \{#Kun_EventBus_IEventTypeRegistry_Resolve_System_String\_}

Resolves a CLR type from a stable event-type token.

```csharp
Type? Resolve(string eventType)
```

#### Parameters

`eventType` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Type](https://learn.microsoft.com/dotnet/api/system.type)?

[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the token is not registered.
