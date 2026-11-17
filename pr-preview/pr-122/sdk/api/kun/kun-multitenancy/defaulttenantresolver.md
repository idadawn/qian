> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class DefaultTenantResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy/index.md)\
Assembly: Kun.MultiTenancy.dll

Options-driven [ITenantResolver](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md). Consults the
registered JWT claim names against `HttpContext.User`, then
the registered HTTP header names against `HttpContext.Request.Headers`,
and finally [KunMultiTenancyOptions.FallbackTenantId](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_FallbackTenantId)
when every configured source comes up empty.

```csharp
public sealed class DefaultTenantResolver : ITenantResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DefaultTenantResolver](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy/defaulttenantresolver.md)

#### Implements

[ITenantResolver](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### DefaultTenantResolver(IOptions\<KunMultiTenancyOptions>) \{#Kun_MultiTenancy_DefaultTenantResolver__ctor_Microsoft_Extensions_Options_IOptions_Kun_MultiTenancy_Configuration_KunMultiTenancyOptions\_\_}

Creates a resolver bound to the supplied `options`.

```csharp
public DefaultTenantResolver(IOptions<KunMultiTenancyOptions> options)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunMultiTenancyOptions](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md)>

## Methods

### ResolveTenantIdAsync(HttpContext) \{#Kun_MultiTenancy_DefaultTenantResolver_ResolveTenantIdAsync_Microsoft_AspNetCore_Http_HttpContext\_}

Returns the tenant id for `httpContext`, or
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the resolver cannot determine one.
[KunMultiTenancyOptions.FallbackTenantId](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_FallbackTenantId)
applies last — the default resolver itself emits that when
every configured source comes up empty.

```csharp
public ValueTask<string?> ResolveTenantIdAsync(HttpContext httpContext)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?>
