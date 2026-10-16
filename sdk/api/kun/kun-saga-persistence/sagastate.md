> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Enum SagaState

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence](/sdk/api/kun/kun-saga-persistence/index.md)\
Assembly: Kun.Saga.dll

Lifecycle state of a [SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md).

```csharp
public enum SagaState
```

## Fields

`Compensated = 3`

All compensation steps completed after a forward failure.

`Compensating = 1`

A step failed and the saga is executing compensation steps in reverse order.

`Completed = 2`

All forward steps completed successfully.

`Failed = 4`

The saga failed and compensation itself encountered an unrecoverable error.
Manual intervention is required.

`Running = 0`

The saga is executing its forward steps.
