> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Gateway.Identity

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[NoOpS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/noops2sprincipalresolver.md)

Default [IS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md) that returns the authenticated external principal
unchanged. Consumers that do not need principal enrichment get this for free via
`AddKunGateway`; anyone wiring a custom resolver should register their implementation
with `services.AddSingleton<IS2sPrincipalResolver, MyResolver>()` before
`AddKunGateway` runs.

### Interfaces

[IS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md)

Optional enrichment hook invoked by [S2sTokenIssuer](/pr-preview/pr-202/sdk/api/kun/kun-gateway/s2stokenissuer.md) after the external IdP
principal authenticates and before the internal S2S token is minted. Lets consumers add,
remove, or rewrite claims — typically for tenant lookup, role projection, or attaching
service-scoped metadata that the IdP does not own.
