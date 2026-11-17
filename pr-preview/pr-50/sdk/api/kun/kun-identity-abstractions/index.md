> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.Identity.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Identity.Abstractions.Session](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/index.md)

### Classes

[OidcCodeExchange](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)

Authorization code and the original browser transaction's PKCE verifier and nonce.

[OidcConfiguration](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)

Subset of an OpenID Connect discovery document surfaced by [IOidcClient.DiscoverAsync](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ioidcclient.md#Kun_Identity_Abstractions_IOidcClient_DiscoverAsync_System_Threading_CancellationToken_).

[OidcLogoutToken](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)

Validated back-channel logout instruction. Subject or SessionId (or both) identifies sessions within Issuer.

[OidcTokenResponse](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)

Result of an OIDC authorization-code exchange.

### Interfaces

[IAuthenticatedUser](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/iauthenticateduser.md)

Browser-flow extension of [ICurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/icurrentuser.md). Adds the
multi-tenant identifier, profile claims, and direct access to the
ambient [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) that the
`Kun.Identity.AspNetCore` cookie middleware populates.

[IJwtIssuer](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ijwtissuer.md)

Mints signed JWTs for internal service-to-service calls. The
issuer, audience, signing key, and default lifetime come from the
registered `KunJwtIssuerOptions`; callers pass the claims
describing _this_ token (subject, tenant, roles, …).

[IJwtValidator](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ijwtvalidator.md)

Validates a Bearer JWT against the configured issuer / audience /
signing keys and returns the resulting [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)
on success. Returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when validation fails —
callers decide whether to surface a 401 or fall through.

[IOidcClient](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ioidcclient.md)

OpenID Connect client for discovery, PKCE code exchange, token refresh,
client credentials and signed back-channel logout validation.
