> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Core.Security

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AnonymousCurrentUser](/sdk/api/kun/kun-core-security/anonymouscurrentuser.md)

[ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md) implementation used when no authentication context is available
(for instance, before S2S JWT validation or in background workers with no caller identity).

### Interfaces

[ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md)

当前用户上下文，从 S2S JWT 的 `sub` / `roles` claim 中解析。

[ITracedCurrentUser](/sdk/api/kun/kun-core-security/itracedcurrentuser.md)

Extension of [ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md) that exposes the ambient OpenTelemetry trace id.
Cast or inject this sub-interface when trace-continuity access is required.
