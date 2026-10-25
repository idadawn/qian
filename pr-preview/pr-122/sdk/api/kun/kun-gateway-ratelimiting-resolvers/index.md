> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Gateway.RateLimiting.Resolvers

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[IpRateLimitPartitionResolver](/pr-preview/pr-122/sdk/api/kun/kun-gateway-ratelimiting-resolvers/ipratelimitpartitionresolver.md)

Resolves the rate-limit partition key from the remote IP address.
Reads the peer normalized by trusted ForwardedHeadersMiddleware; never parses raw headers.

[TenantRateLimitPartitionResolver](/pr-preview/pr-122/sdk/api/kun/kun-gateway-ratelimiting-resolvers/tenantratelimitpartitionresolver.md)

Resolves the rate-limit partition key from the `tenant_id` claim of the authenticated user.
Falls back to `"anonymous"` when the claim is absent.

[UserPrincipalRateLimitPartitionResolver](/pr-preview/pr-122/sdk/api/kun/kun-gateway-ratelimiting-resolvers/userprincipalratelimitpartitionresolver.md)

Resolves the rate-limit partition key from the authenticated user's `sub` claim
(the per-user identifier in S2S JWTs and most OIDC tokens). Falls back to `"anonymous"`
when the claim is absent — useful when an open endpoint should still rate-limit per
caller IP via a different resolver chain.
