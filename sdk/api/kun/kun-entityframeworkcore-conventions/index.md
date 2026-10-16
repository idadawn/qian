> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EntityFrameworkCore.Conventions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[KunDataConventions](/sdk/api/kun/kun-entityframeworkcore-conventions/kundataconventions.md)

数据层的列命名、Guid v7 主键与 PostgreSQL xmin 并发约定。

[KunGuidV7ValueGenerator](/sdk/api/kun/kun-entityframeworkcore-conventions/kunguidv7valuegenerator.md)

只为未赋值的 Guid 主键生成有时间顺序的版本 7 标识。
