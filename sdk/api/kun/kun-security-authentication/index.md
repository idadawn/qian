> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Security.Authentication

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[KunServiceAuthenticationExtensions](/sdk/api/kun/kun-security-authentication/kunserviceauthenticationextensions.md)

Extension methods for registering Kun S2S JWT Bearer authentication on downstream services.

[KunServiceAuthenticationOptions](/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md)

Options for configuring S2S JWT Bearer authentication on a downstream service.
Supports both static key sets and dynamic JWKS endpoint resolution.

[PrincipalClaims](/sdk/api/kun/kun-security-authentication/principalclaims.md)

从已认证的主身份读取授权主体，不拼接不同身份的 claim。
