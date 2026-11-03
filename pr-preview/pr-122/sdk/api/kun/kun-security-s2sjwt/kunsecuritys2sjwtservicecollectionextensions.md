> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunSecurityS2SJwtServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Extension methods for registering Kun S2S JWT issuer and verifier services.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public static class KunSecurityS2SJwtServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSecurityS2SJwtServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/kunsecuritys2sjwtservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddS2SJwt(IServiceCollection, Action\<S2SJwtOptions>) \{#Kun_Security_S2SJwt_KunSecurityS2SJwtServiceCollectionExtensions_AddS2SJwt_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Security_S2SJwt_S2SJwtOptions\_\_}

Registers [IS2SJwtIssuer](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md) and [IS2SJwtVerifier](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/is2sjwtverifier.md)
configured with ES256 signing keys from [S2SJwtOptions](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md).

```csharp
public static IServiceCollection AddS2SJwt(this IServiceCollection services, Action<S2SJwtOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[S2SJwtOptions](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
