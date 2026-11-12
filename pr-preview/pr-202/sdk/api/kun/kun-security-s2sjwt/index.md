> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Security.S2SJwt

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[JwksEndpointExtensions](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/jwksendpointextensions.md)

Extension methods for registering the S2S JWKS endpoint middleware.

[JwksEndpointMiddleware](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/jwksendpointmiddleware.md)

ASP.NET Core middleware that serves the JWKS at `/.well-known/jwks.json`.
Includes both the current and previous signing keys (for graceful rotation).

[KunSecurityS2SJwtServiceCollectionExtensions](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/kunsecuritys2sjwtservicecollectionextensions.md)

Extension methods for registering Kun S2S JWT issuer and verifier services.

[S2SJwtIssueRequest](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)

Input for [IS2SJwtIssuer.IssueAsync](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_).
Carries the full SAD v3.0 §12 legacy S2S claim set.

[S2SJwtIssuer](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuer.md)

ES256 implementation of [IS2SJwtIssuer](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md). Emits the SAD v3.0 §12 claim
set (`sub`, `tenant_id`, `roles`, optional `membership_id`,
optional `project_id`, optional `trace_id`) and retains the pre-existing
`scope` / `session_id` claims for OAuth2-style callers.

[S2SJwtOptions](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md)

Configuration for S2S JWT issuance and verification using ES256 (ECDSA P-256).
Per the zero-trust policy, ES256 is the required algorithm for legacy S2S tokens.

[S2SJwtVerifier](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/s2sjwtverifier.md)

Verifies S2S JWT tokens using ES256 and the configured signing keys.
Supports key rotation by accepting both current and previous signing keys.

### Interfaces

[IS2SJwtIssuer](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md)

Issues S2S JWT tokens signed with ES256 for internal service-to-service authentication.

[IS2SJwtVerifier](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/is2sjwtverifier.md)

Verifies S2S JWT tokens issued by the gateway.
