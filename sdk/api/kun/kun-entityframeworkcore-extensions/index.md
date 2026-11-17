> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EntityFrameworkCore.Extensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[KunDbContextOptionsBuilderExtensions](/sdk/api/kun/kun-entityframeworkcore-extensions/kundbcontextoptionsbuilderextensions.md)

Provides Kun-opinionated configuration helpers for [`DbContextOptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder).

[KunSaveChangesResultExtensions](/sdk/api/kun/kun-entityframeworkcore-extensions/kunsavechangesresultextensions.md)

在 Result 边界显式处理乐观并发冲突，其他系统异常仍抛出。
