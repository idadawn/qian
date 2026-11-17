> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Saga

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Saga)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Saga.Abstractions](/sdk/api/kun/kun-saga-abstractions/index.md)

[Kun.Saga.DependencyInjection](/sdk/api/kun/kun-saga-dependencyinjection/index.md)

[Kun.Saga.Exceptions](/sdk/api/kun/kun-saga-exceptions/index.md)

[Kun.Saga.Orchestration](/sdk/api/kun/kun-saga-orchestration/index.md)

[Kun.Saga.Persistence](/sdk/api/kun/kun-saga-persistence/index.md)

### Classes

[SagaRegistration](/sdk/api/kun/kun-saga/sagaregistration.md)

Holds AOT-safe, closed-over delegates for a single saga type.
Generic types are captured at registration time so no runtime `MakeGenericType`
is needed during execution.

[SagaRegistry](/sdk/api/kun/kun-saga/sagaregistry.md)

Immutable, AOT-safe registry mapping saga names to their execution delegates.
Built in the current execution scope by [KunSagaBuilder](/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md) and
resolved after tenant scope restoration. Uses a [`FrozenDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozendictionary-2) for O(1) lookup.
