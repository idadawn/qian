> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunIdentityAspNetCoreServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Registers the browser identity helpers without replacing the host's authentication scheme.

```csharp
public static class KunIdentityAspNetCoreServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunIdentityAspNetCoreServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/kunidentityaspnetcoreservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Fields

### CookieScheme \{#Kun_Identity_AspNetCore_KunIdentityAspNetCoreServiceCollectionExtensions_CookieScheme}

Authentication scheme for the verified local cookie principal.

```csharp
public const string CookieScheme = "KunCookieAuth"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### AddKunIdentityAspNetCore(IServiceCollection) \{#Kun_Identity_AspNetCore_KunIdentityAspNetCoreServiceCollectionExtensions_AddKunIdentityAspNetCore_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Adds protected OIDC challenges, atomic replay checks, sign-in helpers, current-user bindings and antiforgery.

```csharp
public static IServiceCollection AddKunIdentityAspNetCore(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
