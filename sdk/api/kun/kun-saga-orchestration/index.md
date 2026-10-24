> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Saga.Orchestration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Saga)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[SagaOrchestrator](/sdk/api/kun/kun-saga-orchestration/sagaorchestrator.md)

Default implementation of [ISagaOrchestrator](/sdk/api/kun/kun-saga-abstractions/isagaorchestrator.md).
Persists the initial [SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md) and enqueues the first step entry within
the caller's ambient unit-of-work transaction. All generic dispatch is delegated to
AOT-safe typed delegates captured in the [SagaRegistry](/sdk/api/kun/kun-saga/sagaregistry.md) at startup.

[SagaStepExecutor](/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md)

Background service that polls [ISagaInstanceStore.FetchPendingStepsAsync](/sdk/api/kun/kun-saga-persistence/isagainstancestore.md#Kun_Saga_Persistence_ISagaInstanceStore_FetchPendingStepsAsync_System_Int32_System_Threading_CancellationToken_) and
drives saga step execution and compensation. Creates an [`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource) span
for each step to enable distributed tracing.

[SagaStepExecutorOptions](/sdk/api/kun/kun-saga-orchestration/sagastepexecutoroptions.md)

Configuration options for [SagaStepExecutor](/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md).

[SagaTimeoutDetector](/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md)

Background service that periodically scans for timed-out saga instances and triggers
compensation. An instance is considered timed-out when its `TimeoutAt` is in the past
and its state is [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running).

[SagaTimeoutDetectorOptions](/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetectoroptions.md)

Configuration options for [SagaTimeoutDetector](/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md).
