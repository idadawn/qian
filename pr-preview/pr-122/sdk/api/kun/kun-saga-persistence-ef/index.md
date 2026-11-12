> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Saga.Persistence.Ef

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[EfSagaInstanceStore](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/efsagainstancestore.md)

EF Core-backed implementation of [ISagaInstanceStore](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/isagainstancestore.md).
Operates against `saga_instances` and `saga_step_queue` tables
via `DbContext.Set<T>`.

[KunSagaDbContextExtensions](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/kunsagadbcontextextensions.md)

Extension methods for [`ModelBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder) to apply Kun Saga entity configurations.
Call [KunSagaDbContextExtensions.ApplyKunSagaConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/kunsagadbcontextextensions.md#Kun_Saga_Persistence_Ef_KunSagaDbContextExtensions_ApplyKunSagaConfiguration_Microsoft_EntityFrameworkCore_ModelBuilder_) from your `DbContext.OnModelCreating`
override to register `saga_instances` and `saga_step_queue` tables.

[SagaInstanceConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/sagainstanceconfiguration.md)

EF Core entity type configuration for [SagaInstance](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagainstance.md).
Table: `saga_instances`.
Indexes: `(state, timeout_at)` for timeout scanning; `(saga_name)` for name filtering.

[SagaStepQueueEntryConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/sagastepqueueentryconfiguration.md)

EF Core entity type configuration for [SagaStepQueueEntry](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md).
Table: `saga_step_queue`.
Indexes: `(processed_at, enqueued_at)` for executor polling.
