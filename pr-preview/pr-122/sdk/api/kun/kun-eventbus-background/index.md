> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.EventBus.Background

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[NullOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md)

No-op [IOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md) for single-instance deployments.
Always succeeds and returns a disposable that does nothing.
This is the default provider registered by `AddKunOutboxPublisher`.

[OutboxPublisherBackgroundService](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisherbackgroundservice.md)

Hosted background service that polls the outbox store for pending messages and
publishes them via [IIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/iintegrationeventpublisher.md).

[OutboxPublisherOptions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisheroptions.md)

Configures the outbox publisher background service.

[RedisOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/redisoutboxlockprovider.md)

Redis-backed [IOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md) using `SET key value NX EX` semantics.
Safe for multi-pod deployments: only one pod holds the lock per TTL window.

### Interfaces

[IOutboxBatchStore](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxbatchstore.md)

事务内领取并持有消息行锁的 Outbox 存储；释放作用域前必须完成批次。

[IOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md)

Provides a distributed lease that prevents multiple pods from publishing
the same outbox batch simultaneously.

[IOutboxMessageStore](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxmessagestore.md)

Abstracts access to the outbox persistence layer. The concrete implementation
lives in Kun.EntityFrameworkCore (or any other persistence module).
