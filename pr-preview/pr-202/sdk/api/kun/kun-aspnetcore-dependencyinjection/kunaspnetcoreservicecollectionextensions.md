> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunAspNetCoreServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-dependencyinjection/index.md)\
Assembly: Kun.AspNetCore.dll

Extension methods for registering Kun ASP.NET Core services.

```csharp
public static class KunAspNetCoreServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunAspNetCoreServiceCollectionExtensions](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-dependencyinjection/kunaspnetcoreservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunAspNetCore(IServiceCollection) \{#Kun_AspNetCore_DependencyInjection_KunAspNetCoreServiceCollectionExtensions_AddKunAspNetCore_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers Kun ASP.NET Core services:

- [KunExceptionHandler](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-exceptionhandling/kunexceptionhandler.md) via `AddExceptionHandler`
- Standard `ProblemDetails` support

Call `app.UseExceptionHandler()` to activate the exception handler in the pipeline.

```csharp
public static IServiceCollection AddKunAspNetCore(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.
