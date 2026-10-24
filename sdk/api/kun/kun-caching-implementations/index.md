> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Caching.Implementations

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Caching)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[MemoryKunCache](/sdk/api/kun/kun-caching-implementations/memorykuncache.md)

In-process [IKunCache](/sdk/api/kun/kun-caching-abstractions/ikuncache.md) backed by
[`IMemoryCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.memory.imemorycache). Stores values directly — no
serialization round-trip — so mutations on cached reference
types are visible to other consumers. Callers that care about
isolation should cache immutable records or value types.

[RedisKunCache](/sdk/api/kun/kun-caching-implementations/rediskuncache.md)

Distributed [IKunCache](/sdk/api/kun/kun-caching-abstractions/ikuncache.md) backed by an
[`IDistributedCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache) instance (typically
`Microsoft.Extensions.Caching.StackExchangeRedis`). Values are
serialized via the registered [IKunCacheSerializer](/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md).

[SystemTextJsonCacheSerializer](/sdk/api/kun/kun-caching-implementations/systemtextjsoncacheserializer.md)

Default [IKunCacheSerializer](/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md) backed by
[`JsonSerializer`](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializer). Uses web-style options (camelCase,
case-insensitive reads) and respects the caller's
[`JsonSerializerOptions`](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions) when supplied.
