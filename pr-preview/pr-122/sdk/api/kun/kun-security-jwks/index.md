> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Security.Jwks

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[FileKunJwksProvider](/pr-preview/pr-122/sdk/api/kun/kun-security-jwks/filekunjwksprovider.md)

Loads a JSON Web Key Set from a local file and caches it in memory.
The cache is invalidated when the file's last-write time changes, enabling
key rotation without service restart.

### Interfaces

[IKunJwksProvider](/pr-preview/pr-122/sdk/api/kun/kun-security-jwks/ikunjwksprovider.md)

Provides the JSON Web Key Set used for S2S token signature verification.
