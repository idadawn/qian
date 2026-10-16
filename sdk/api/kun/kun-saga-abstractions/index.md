> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Saga.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Interfaces

[ISaga\<TContext>](/sdk/api/kun/kun-saga-abstractions/isaga-1.md)

Defines an orchestrated saga — an ordered sequence of compensatable [ISagaStep\<TContext>](/sdk/api/kun/kun-saga-abstractions/isagastep-1.md)s.
The orchestrator executes steps in forward order and compensates in reverse order on failure.

[ISagaContext](/sdk/api/kun/kun-saga-abstractions/isagacontext.md)

Marker interface for saga context objects. Every saga context carries a unique
[ISagaContext.SagaId](/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId) and an optional [ISagaContext.TenantId](/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_TenantId) for multi-tenant scenarios.
Implementations should be serializable to JSON (used for durable state storage).

[ISagaOrchestrator](/sdk/api/kun/kun-saga-abstractions/isagaorchestrator.md)

Starts new saga instances and persists the initial state atomically within the caller's
ambient unit-of-work transaction. The background `SagaStepExecutor` then drives
step execution and compensation asynchronously.

[ISagaStep\<TContext>](/sdk/api/kun/kun-saga-abstractions/isagastep-1.md)

A single compensatable step in a saga. Each step must implement both a forward
[ISagaStep\<TContext>.ExecuteAsync](/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_ExecuteAsync__0_System_Threading_CancellationToken_) and a reverse
[ISagaStep\<TContext>.CompensateAsync](/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_CompensateAsync__0_System_Threading_CancellationToken_) path, both of which must be idempotent.
