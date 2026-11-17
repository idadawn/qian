> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Identity.Configuration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[KunCookieAuthOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md)

Options for the cookie session layer wired by
`AddKunIdentity(id => id.AddCookieSession(...))`. The defaults
encode the zero-trust baseline `Kun.Gateway` already applies to
outbound cookies.

[KunCookieSessionBuilder](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)

Configuration surface exposed by
`KunIdentityBuilder.AddCookieSession(opts => ...)`. Inherits
every [KunCookieAuthOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md) field (so callers write
`opts.CookieName = "kun.sid"; opts.SessionTtl = ...;` as
expected) and adds the backend-selection methods
[KunCookieSessionBuilder.UseInMemoryStore](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md#Kun_Identity_Configuration_KunCookieSessionBuilder_UseInMemoryStore) and [KunCookieSessionBuilder.UseRedisStore](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md#Kun_Identity_Configuration_KunCookieSessionBuilder_UseRedisStore).
Exactly one backend call should be made — multiple calls take
last-wins semantics because each call replaces the
[ISessionStore](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) registration on the underlying service
collection.

[KunIdentityBuilder](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)

Fluent builder surfaced by `AddKunIdentity`.

[KunJwtBearerOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md)

Validation parameters for external Bearer JWTs issued by an OIDC
provider (Authentik, Keycloak, Auth0, Azure AD, …). Feeds
`KunJwtValidator`.

[KunJwtIssuerOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunjwtissueroptions.md)

Signing / claims configuration for `KunJwtIssuer`.

[KunOidcClientOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunoidcclientoptions.md)

Per-provider configuration for `KunOidcClient`.

### Enums

[KunCookieSecurePolicy](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md)

Secure-policy for [KunCookieAuthOptions.Secure](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Secure). Mirrors
ASP.NET Core's `CookieSecurePolicy` shape without taking a
dependency on that package from the core abstractions.

[KunSameSiteMode](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunsamesitemode.md)

`SameSite` policy for [KunCookieAuthOptions.SameSite](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SameSite).
Mirrors ASP.NET Core's `SameSiteMode` shape without taking a
dependency on that package from the core abstractions.
