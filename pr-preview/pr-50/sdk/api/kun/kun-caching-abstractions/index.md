> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.Caching.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Caching)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Interfaces

[IKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-abstractions/ikuncache.md)

Unified cache abstraction for Kun Framework consumers. Implementations
back to an in-process [`IMemoryCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.memory.imemorycache)
or to a distributed [`IDistributedCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache)
(Redis via StackExchange) — see
`Kun.Caching.Implementations.MemoryKunCache` and
`Kun.Caching.Implementations.RedisKunCache`.

[IKunCacheSerializer](/pr-preview/pr-50/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md)

Pluggable serializer used by the Redis-backed cache to convert
cache entries to and from byte arrays. The default registration is
`Kun.Caching.Implementations.SystemTextJsonCacheSerializer`,
which is reflection-based; AOT-strict consumers provide a
source-generated implementation.
