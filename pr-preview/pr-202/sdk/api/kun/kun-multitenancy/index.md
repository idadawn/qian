> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.MultiTenancy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.MultiTenancy.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-abstractions/index.md)

[Kun.MultiTenancy.Configuration](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-configuration/index.md)

[Kun.MultiTenancy.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-dependencyinjection/index.md)

[Kun.MultiTenancy.EntityFrameworkCore](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/index.md)

[Kun.MultiTenancy.Middleware](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-middleware/index.md)

### Classes

[AsyncLocalTenantContext](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md)

[ITenantContext](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) implementation backed by an
[`AsyncLocal`](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1) so the tenant id flows with the async
call graph across thread hops, task schedulers, and concurrent
parallel tasks. Pair with `TenantContextMiddleware` (or a
background-worker scope that opens a tenant scope explicitly) to
populate the value.

[DefaultTenantResolver](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy/defaulttenantresolver.md)

Options-driven [ITenantResolver](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md). Consults the
registered JWT claim names against `HttpContext.User`, then
the registered HTTP header names against `HttpContext.Request.Headers`,
and finally [KunMultiTenancyOptions.FallbackTenantId](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_FallbackTenantId)
when every configured source comes up empty.
