> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Enum StepDirection

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/index.md)\
Assembly: Kun.Saga.dll

Indicates whether a queued saga step entry is a forward execution or a compensation.

```csharp
public enum StepDirection
```

## Fields

`Compensate = 1`

The step should call `ISagaStep.CompensateAsync`.

`Execute = 0`

The step should call `ISagaStep.ExecuteAsync`.
