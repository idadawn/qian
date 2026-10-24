> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.MultiTenancy.Middleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[KunMultiTenancyApplicationBuilderExtensions](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-middleware/kunmultitenancyapplicationbuilderextensions.md)

Registration extensions for [TenantContextMiddleware](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-middleware/tenantcontextmiddleware.md).

[TenantContextMiddleware](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-middleware/tenantcontextmiddleware.md)

Resolves the inbound request's tenant id and opens an
[AsyncLocalTenantContext](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md) scope that bounds every
async continuation downstream from this middleware.
