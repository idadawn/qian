> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SystemTextJsonCacheSerializer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.Implementations](/pr-preview/pr-181/sdk/api/kun/kun-caching-implementations/index.md)\
Assembly: Kun.Caching.dll

Default [IKunCacheSerializer](/pr-preview/pr-181/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md) backed by
[`JsonSerializer`](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializer). Uses web-style options (camelCase,
case-insensitive reads) and respects the caller's
[`JsonSerializerOptions`](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions) when supplied.

```csharp
public sealed class SystemTextJsonCacheSerializer : IKunCacheSerializer
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SystemTextJsonCacheSerializer](/pr-preview/pr-181/sdk/api/kun/kun-caching-implementations/systemtextjsoncacheserializer.md)

#### Implements

[IKunCacheSerializer](/pr-preview/pr-181/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Reflection-based; annotate any non-AOT-safe call site with the same
attributes or swap this impl for a source-generated one registered
via `services.AddSingleton<IKunCacheSerializer, MyCtxSerializer>()`.

## Constructors

### SystemTextJsonCacheSerializer() \{#Kun_Caching_Implementations_SystemTextJsonCacheSerializer__ctor}

Creates a serializer with the default web-style options.

```csharp
public SystemTextJsonCacheSerializer()
```

### SystemTextJsonCacheSerializer(JsonSerializerOptions) \{#Kun_Caching_Implementations_SystemTextJsonCacheSerializer__ctor_System_Text_Json_JsonSerializerOptions\_}

Creates a serializer with custom `options`.

```csharp
public SystemTextJsonCacheSerializer(JsonSerializerOptions options)
```

#### Parameters

`options` [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

## Methods

### Deserialize\<T>(byte\[]) \{#Kun_Caching_Implementations_SystemTextJsonCacheSerializer_Deserialize__1_System_Byte\_\_\_}

Deserializes `data` to a `T` instance.

```csharp
[RequiresUnreferencedCode("Reflection-based JSON serialization.")]
[RequiresDynamicCode("Reflection-based JSON serialization.")]
public T? Deserialize<T>(byte[] data)
```

#### Parameters

`data` [byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

#### Returns

T?

#### Type Parameters

`T`

### Serialize\<T>(T) \{#Kun_Caching_Implementations_SystemTextJsonCacheSerializer_Serialize__1___0\_}

Serializes `value` to a newly allocated byte array.

```csharp
[RequiresUnreferencedCode("Reflection-based JSON serialization.")]
[RequiresDynamicCode("Reflection-based JSON serialization.")]
public byte[] Serialize<T>(T value)
```

#### Parameters

`value` T

#### Returns

[byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

#### Type Parameters

`T`
