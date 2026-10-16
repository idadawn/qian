> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Security

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Security.Authentication](/pr-preview/pr-122/sdk/api/kun/kun-security-authentication/index.md)

[Kun.Security.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-security-dependencyinjection/index.md)

[Kun.Security.Extensions](/pr-preview/pr-122/sdk/api/kun/kun-security-extensions/index.md)

[Kun.Security.Http](/pr-preview/pr-122/sdk/api/kun/kun-security-http/index.md)

[Kun.Security.Jwks](/pr-preview/pr-122/sdk/api/kun/kun-security-jwks/index.md)

[Kun.Security.MultiTenancy](/pr-preview/pr-122/sdk/api/kun/kun-security-multitenancy/index.md)

[Kun.Security.S2SJwt](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/index.md)

[Kun.Security.Security](/pr-preview/pr-122/sdk/api/kun/kun-security-security/index.md)

### Classes

[S2sTokenOptions](/pr-preview/pr-122/sdk/api/kun/kun-security/s2stokenoptions.md)

Configuration for S2S JWT issuance and validation.
Algorithm is always asymmetric (RS256/ES256) — HS256 is rejected per zero-trust policy.
