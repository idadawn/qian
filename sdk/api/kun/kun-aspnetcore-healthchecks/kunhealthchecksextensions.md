> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunHealthChecksExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.AspNetCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.HealthChecks](/sdk/api/kun/kun-aspnetcore-healthchecks/index.md)\
Assembly: Kun.AspNetCore.dll

Extension methods for registering Kun health checks.

```csharp
public static class KunHealthChecksExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunHealthChecksExtensions](/sdk/api/kun/kun-aspnetcore-healthchecks/kunhealthchecksextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunHealthChecks(IServiceCollection) \{#Kun_AspNetCore_HealthChecks_KunHealthChecksExtensions_AddKunHealthChecks_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers the standard ASP.NET Core health check infrastructure.
Infrastructure-specific checks (DB, Redis, RabbitMQ) are registered separately
by the respective Kun infrastructure modules.

```csharp
public static IHealthChecksBuilder AddKunHealthChecks(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

#### Returns

[IHealthChecksBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder)

The [`IHealthChecksBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihealthchecksbuilder) for further configuration.
