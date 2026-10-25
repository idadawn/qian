> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunCookieAuthApplicationBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

`IApplicationBuilder` extensions for the Kun cookie middleware.

```csharp
public static class KunCookieAuthApplicationBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCookieAuthApplicationBuilderExtensions](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthapplicationbuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### UseKunCookieAuth(IApplicationBuilder) \{#Kun_Identity_AspNetCore_KunCookieAuthApplicationBuilderExtensions_UseKunCookieAuth_Microsoft_AspNetCore_Builder_IApplicationBuilder\_}

Inserts [KunCookieAuthMiddleware](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthmiddleware.md) into the pipeline.
Call before any endpoint / authorization middleware that depends
on `HttpContext.User`.

```csharp
public static IApplicationBuilder UseKunCookieAuth(this IApplicationBuilder app)
```

#### Parameters

`app` [IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)

#### Returns

[IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)
