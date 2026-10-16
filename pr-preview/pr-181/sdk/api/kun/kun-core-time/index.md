> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.Core.Time

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Classes

[SystemClock](/pr-preview/pr-181/sdk/api/kun/kun-core-time/systemclock.md)

Default [ISystemClock](/pr-preview/pr-181/sdk/api/kun/kun-core-time/isystemclock.md) implementation backed by [`System`](https://learn.microsoft.com/dotnet/api/system.timeprovider.system).

### Interfaces

[ISystemClock](/pr-preview/pr-181/sdk/api/kun/kun-core-time/isystemclock.md)

Abstraction over the system clock. Used by Kun framework and business code in place of
[`UtcNow`](https://learn.microsoft.com/dotnet/api/system.datetimeoffset.utcnow) so tests can inject a deterministic clock.
