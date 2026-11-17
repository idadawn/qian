> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Identity.Implementations

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Identity.Implementations.Session](/sdk/api/kun/kun-identity-implementations-session/index.md)

### Classes

[KunJwtIssuer](/sdk/api/kun/kun-identity-implementations/kunjwtissuer.md)

[IJwtIssuer](/sdk/api/kun/kun-identity-abstractions/ijwtissuer.md) backed by
[`JsonWebTokenHandler`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler). Signs tokens with the configured
RSA key — supplied either as a PEM string or as a
pre-built [`SecurityKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.securitykey) (the test-friendly path).

[KunJwtValidator](/sdk/api/kun/kun-identity-implementations/kunjwtvalidator.md)

[IJwtValidator](/sdk/api/kun/kun-identity-abstractions/ijwtvalidator.md) backed by
[`JsonWebTokenHandler`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler). Resolves signing keys from the
configured JWKS URI via
[`ConfigurationManager`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.protocols.configurationmanager-1) (with automatic
rotation) or from statically registered keys when supplied. Returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on any validation failure — the caller
decides whether to 401, ignore, or escalate.

[KunOidcClient](/sdk/api/kun/kun-identity-implementations/kunoidcclient.md)

OIDC protocol client with bounded metadata caching and signed, issuer/audience/nonce-bound ID Tokens.
