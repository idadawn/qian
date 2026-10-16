> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunSecurityServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-security-dependencyinjection/index.md)\
Assembly: Kun.Security.dll

Extension methods for registering Kun Security services with an [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

```csharp
public static class KunSecurityServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSecurityServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-security-dependencyinjection/kunsecurityservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunSecurity(IServiceCollection) \{#Kun_Security_DependencyInjection_KunSecurityServiceCollectionExtensions_AddKunSecurity_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers Kun Security services:

- [`IHttpContextAccessor`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor) (if not already registered)
- Singleton [ITenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) → [AsyncLocalTenantContext](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md)，JWT 仅作解析源
- Scoped [ICurrentUser](/pr-preview/pr-181/sdk/api/kun/kun-core-security/icurrentuser.md) → [JwtCurrentUser](/pr-preview/pr-181/sdk/api/kun/kun-security-security/jwtcurrentuser.md)

Call `AddAuthentication().AddKunServiceAuthentication(...)` separately to configure
JWT Bearer validation.

```csharp
public static IServiceCollection AddKunSecurity(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.
