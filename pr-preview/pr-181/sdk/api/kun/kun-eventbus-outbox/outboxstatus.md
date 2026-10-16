> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Enum OutboxStatus

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Outbox](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/index.md)\
Assembly: Kun.EventBus.dll

Lifecycle state of a persisted Outbox message.

```csharp
public enum OutboxStatus
```

## Fields

`Failed = 2`

Publish permanently failed after exhausting retries.

`Pending = 0`

Persisted but not yet published to the message bus.

`Published = 1`

Successfully published.
