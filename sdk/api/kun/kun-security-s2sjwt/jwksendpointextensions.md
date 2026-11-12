> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class JwksEndpointExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Security)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Extension methods for registering the S2S JWKS endpoint middleware.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public static class JwksEndpointExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwksEndpointExtensions](/sdk/api/kun/kun-security-s2sjwt/jwksendpointextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### UseS2SJwksEndpoint(IApplicationBuilder) \{#Kun_Security_S2SJwt_JwksEndpointExtensions_UseS2SJwksEndpoint_Microsoft_AspNetCore_Builder_IApplicationBuilder\_}

Adds the JWKS endpoint middleware that serves `/.well-known/jwks.json`.

```csharp
public static IApplicationBuilder UseS2SJwksEndpoint(this IApplicationBuilder app)
```

#### Parameters

`app` [IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)

#### Returns

[IApplicationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iapplicationbuilder)
