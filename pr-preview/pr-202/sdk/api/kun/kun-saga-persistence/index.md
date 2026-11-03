> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Saga.Persistence

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Saga.Persistence.Ef](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence-ef/index.md)

### Classes

[SagaInstance](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/sagainstance.md)

Durable state for a running or completed saga instance.
ORM mapping lives in `SagaInstanceConfiguration` — no ORM attributes here.

[SagaStepQueueEntry](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

A queued work item representing a single step execution or compensation for a saga.
The background `SagaStepExecutor` polls this table and processes entries in order.
ORM mapping lives in `SagaStepQueueEntryConfiguration` — no ORM attributes here.

### Interfaces

[ISagaInstanceStore](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/isagainstancestore.md)

Persistence contract for saga instance state and step queue entries.
Implementations are responsible for durable storage and querying of saga lifecycle data.

### Enums

[SagaState](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/sagastate.md)

Lifecycle state of a [SagaInstance](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/sagainstance.md).

[StepDirection](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/stepdirection.md)

Indicates whether a queued saga step entry is a forward execution or a compensation.
