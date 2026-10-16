> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Identity.Implementations.Session

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[InMemorySessionStore](/sdk/api/kun/kun-identity-implementations-session/inmemorysessionstore.md)

Process-local session registry for tests/development; all index, renewal and revocation changes share a lock.

[RedisSessionStore](/sdk/api/kun/kun-identity-implementations-session/redissessionstore.md)

Redis session registry with atomic user/provider indexes, renewal and logout replay protection.
