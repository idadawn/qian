> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.Gateway.Jwks

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Classes

[JwksResponseBuilder](/pr-preview/pr-181/sdk/api/kun/kun-gateway-jwks/jwksresponsebuilder.md)

Caches the JWKS response keyed on (public-key path + file last-write time).
Re-reads the PEM, rebuilds the [`JsonWebKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkey), and materialises a new
[JwksResponse](/pr-preview/pr-181/sdk/api/kun/kun-gateway-endpoints/jwksresponse.md) only when the underlying file has been modified —
amortises the per-request cost of every JWKS fetch across downstream services
to a single [`GetLastWriteTimeUtc`](https://learn.microsoft.com/dotnet/api/system.io.file.getlastwritetimeutc) stat after the first warm-up.

[KunInternalJwksEndpoint](/pr-preview/pr-181/sdk/api/kun/kun-gateway-jwks/kuninternaljwksendpoint.md)

Extension methods for mapping the **internal-network** JWKS endpoint, separate from the
always-on public `/.well-known/jwks.json` route.
