> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.Gateway

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Gateway.Configuration](/pr-preview/pr-181/sdk/api/kun/kun-gateway-configuration/index.md)

[Kun.Gateway.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-gateway-dependencyinjection/index.md)

[Kun.Gateway.Endpoints](/pr-preview/pr-181/sdk/api/kun/kun-gateway-endpoints/index.md)

[Kun.Gateway.Identity](/pr-preview/pr-181/sdk/api/kun/kun-gateway-identity/index.md)

[Kun.Gateway.Jwks](/pr-preview/pr-181/sdk/api/kun/kun-gateway-jwks/index.md)

[Kun.Gateway.RateLimiting](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/index.md)

### Classes

[S2sTokenIssuer](/pr-preview/pr-181/sdk/api/kun/kun-gateway/s2stokenissuer.md)

Issues the closed, short-lived S2S claim contract with RS256 and the shared rotating JWKS ring.
An authenticated principal/resolver must provide explicit user/service identity, tenant context and user session/revision fields.
Trace context travels in headers, not additional JWT payload fields.
