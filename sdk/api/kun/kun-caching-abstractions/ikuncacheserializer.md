> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IKunCacheSerializer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.Abstractions](/sdk/api/kun/kun-caching-abstractions/index.md)\
Assembly: Kun.Caching.dll

Pluggable serializer used by the Redis-backed cache to convert
cache entries to and from byte arrays. The default registration is
`Kun.Caching.Implementations.SystemTextJsonCacheSerializer`,
which is reflection-based; AOT-strict consumers provide a
source-generated implementation.

```csharp
public interface IKunCacheSerializer
```

## Methods

### Deserialize\<T>(byte\[]) \{#Kun_Caching_Abstractions_IKunCacheSerializer_Deserialize__1_System_Byte\_\_\_}

Deserializes `data` to a `T` instance.

```csharp
[RequiresUnreferencedCode("Reflection-based default. Provide a source-generated IKunCacheSerializer for AOT safety.")]
[RequiresDynamicCode("Reflection-based default. Provide a source-generated IKunCacheSerializer for AOT safety.")]
T? Deserialize<T>(byte[] data)
```

#### Parameters

`data` [byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

#### Returns

T?

#### Type Parameters

`T`

### Serialize\<T>(T) \{#Kun_Caching_Abstractions_IKunCacheSerializer_Serialize__1___0\_}

Serializes `value` to a newly allocated byte array.

```csharp
[RequiresUnreferencedCode("Reflection-based default. Provide a source-generated IKunCacheSerializer for AOT safety.")]
[RequiresDynamicCode("Reflection-based default. Provide a source-generated IKunCacheSerializer for AOT safety.")]
byte[] Serialize<T>(T value)
```

#### Parameters

`value` T

#### Returns

[byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

#### Type Parameters

`T`
