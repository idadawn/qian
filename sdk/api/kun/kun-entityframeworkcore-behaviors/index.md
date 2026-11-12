> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EntityFrameworkCore.Behaviors

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[UnitOfWorkPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-entityframeworkcore-behaviors/unitofworkpipelinebehavior-2.md)

Mediator pipeline behavior that commits the unit-of-work after a command handler returns
successfully. Place this behavior after validation but before logging so the commit
participates in the correct observability scope.
