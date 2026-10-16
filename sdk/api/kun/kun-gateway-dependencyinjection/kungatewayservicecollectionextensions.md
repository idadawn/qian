> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunGatewayServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.DependencyInjection](/sdk/api/kun/kun-gateway-dependencyinjection/index.md)\
Assembly: Kun.Gateway.dll

Extension methods for registering Kun Gateway services with an [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

```csharp
public static class KunGatewayServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunGatewayServiceCollectionExtensions](/sdk/api/kun/kun-gateway-dependencyinjection/kungatewayservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunGateway(IServiceCollection, Action\<KunGatewayOptions>, IConfiguration?) \{#Kun_Gateway_DependencyInjection_KunGatewayServiceCollectionExtensions_AddKunGateway_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Gateway_Configuration_KunGatewayOptions__Microsoft_Extensions_Configuration_IConfiguration\_}

Registers core Kun Gateway services:

- [KunGatewayOptions](/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md) bound from the provided delegate.
- [S2sTokenIssuer](/sdk/api/kun/kun-gateway/s2stokenissuer.md) as a singleton.
- Default [TenantRateLimitPartitionResolver](/sdk/api/kun/kun-gateway-ratelimiting-resolvers/tenantratelimitpartitionresolver.md) (override by registering your own before calling this).
- YARP reverse proxy with the [Kun.Gateway.Transforms.S2sAuthTransform](/sdk/api/kun/kun-gateway/index.md#Kun_Gateway_Transforms_S2sAuthTransform) applied to all routes.

When `reverseProxyConfig` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), a startup validation
service checks that an `IProxyConfigProvider` has been registered and throws a
descriptive [`InvalidOperationException`](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception) on host start if one is missing.

```csharp
public static IServiceCollection AddKunGateway(this IServiceCollection services, Action<KunGatewayOptions> configure, IConfiguration? reverseProxyConfig = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunGatewayOptions](/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md)>

Delegate to configure [KunGatewayOptions](/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md).

`reverseProxyConfig` [IConfiguration](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfiguration)?

Optional YARP configuration section (e.g. `Configuration.GetSection("ReverseProxy")`)
to bind Routes/Clusters. When null, callers must register their own
`IProxyConfigProvider`.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.
