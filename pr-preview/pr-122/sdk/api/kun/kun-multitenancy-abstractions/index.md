> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.MultiTenancy.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Interfaces

[ITenantIdSource](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-abstractions/itenantidsource.md)

为尚未打开显式作用域的上下文提供已认证的租户标识。

[ITenantResolver](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md)

Produces the current tenant id for an inbound HTTP request. The
framework ships `DefaultTenantResolver`, an options-driven
implementation that consults JWT claims, request headers, and a
configured fallback. Consumers can replace the default with a
custom resolver (subdomain-based, query-string-based, etc.) by
registering their own [ITenantResolver](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md) after calling
`AddKunMultiTenancy`.
