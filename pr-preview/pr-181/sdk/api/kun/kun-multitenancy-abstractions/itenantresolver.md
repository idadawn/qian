> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ITenantResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-abstractions/index.md)\
Assembly: Kun.MultiTenancy.dll

Produces the current tenant id for an inbound HTTP request. The
framework ships `DefaultTenantResolver`, an options-driven
implementation that consults JWT claims, request headers, and a
configured fallback. Consumers can replace the default with a
custom resolver (subdomain-based, query-string-based, etc.) by
registering their own [ITenantResolver](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md) after calling
`AddKunMultiTenancy`.

```csharp
public interface ITenantResolver
```

## Methods

### ResolveTenantIdAsync(HttpContext) \{#Kun_MultiTenancy_Abstractions_ITenantResolver_ResolveTenantIdAsync_Microsoft_AspNetCore_Http_HttpContext\_}

Returns the tenant id for `httpContext`, or
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the resolver cannot determine one.
[KunMultiTenancyOptions.FallbackTenantId](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_FallbackTenantId)
applies last — the default resolver itself emits that when
every configured source comes up empty.

```csharp
ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?>
