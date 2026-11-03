> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class TenantContextMiddleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.MultiTenancy)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.Middleware](/sdk/api/kun/kun-multitenancy-middleware/index.md)\
Assembly: Kun.MultiTenancy.dll

Resolves the inbound request's tenant id and opens an
[AsyncLocalTenantContext](/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md) scope that bounds every
async continuation downstream from this middleware.

```csharp
public sealed class TenantContextMiddleware
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantContextMiddleware](/sdk/api/kun/kun-multitenancy-middleware/tenantcontextmiddleware.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### TenantContextMiddleware(RequestDelegate) \{#Kun_MultiTenancy_Middleware_TenantContextMiddleware__ctor_Microsoft_AspNetCore_Http_RequestDelegate\_}

Creates the middleware bound to `next`.

```csharp
public TenantContextMiddleware(RequestDelegate next)
```

#### Parameters

`next` [RequestDelegate](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.requestdelegate)

## Methods

### InvokeAsync(HttpContext, ITenantResolver, AsyncLocalTenantContext) \{#Kun_MultiTenancy_Middleware_TenantContextMiddleware_InvokeAsync_Microsoft_AspNetCore_Http_HttpContext_Kun_MultiTenancy_Abstractions_ITenantResolver_Kun_MultiTenancy_AsyncLocalTenantContext\_}

Resolves the tenant, scopes it, and invokes the next middleware.

```csharp
public Task InvokeAsync(HttpContext httpContext, ITenantResolver resolver, AsyncLocalTenantContext tenantContext)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`resolver` [ITenantResolver](/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md)

`tenantContext` [AsyncLocalTenantContext](/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
