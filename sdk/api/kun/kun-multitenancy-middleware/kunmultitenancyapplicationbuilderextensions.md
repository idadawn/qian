> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunMultiTenancyApplicationBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.MultiTenancy)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.Middleware](/sdk/api/kun/kun-multitenancy-middleware/index.md)\
Assembly: Kun.MultiTenancy.dll

Registration extensions for [TenantContextMiddleware](/sdk/api/kun/kun-multitenancy-middleware/tenantcontextmiddleware.md).

```csharp
public static class KunMultiTenancyApplicationBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyApplicationBuilderExtensions](/sdk/api/kun/kun-multitenancy-middleware/kunmultitenancyapplicationbuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### UseKunMultiTenancy(IApplicationBuilder) \{#Kun_MultiTenancy_Middleware_KunMultiTenancyApplicationBuilderExtensions_UseKunMultiTenancy_Microsoft_AspNetCore_Builder_IApplicationBuilder\_}

Inserts the Kun multi-tenancy middleware into the pipeline. Call
after `UseAuthentication()` so the JWT-claim resolver sees
the authenticated `HttpContext.User`.

```csharp
public static IApplicationBuilder UseKunMultiTenancy(this IApplicationBuilder app)
```

#### Parameters

`app` [IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)

#### Returns

[IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)
