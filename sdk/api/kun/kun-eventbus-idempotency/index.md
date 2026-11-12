> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EventBus.Idempotency

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[InMemoryProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/inmemoryprocessedmessagestore.md)

In-memory implementation of [IProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md) backed by a
[`ConcurrentDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2). Intended for testing and
single-instance scenarios only — state is lost on restart and is not shared
across multiple processes.

[RedisProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/redisprocessedmessagestore.md)

Redis-backed implementation of [IProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md) using
`SET … NX EX` for atomic first-write semantics. Suitable for distributed,
multi-instance deployments.

### Interfaces

[IProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md)

Tracks message IDs that have already been successfully processed by a given consumer,
enabling idempotent at-least-once delivery. Implementations are broker-agnostic
(Redis, in-memory, SQL).
