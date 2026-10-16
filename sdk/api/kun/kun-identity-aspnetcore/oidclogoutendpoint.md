> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class OidcLogoutEndpoint

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Back-channel logout mapping; authentication comes from the signed provider token, not a browser cookie.

```csharp
public static class OidcLogoutEndpoint
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcLogoutEndpoint](/sdk/api/kun/kun-identity-aspnetcore/oidclogoutendpoint.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### MapKunBackChannelLogout(IEndpointRouteBuilder, string, string) \{#Kun_Identity_AspNetCore_OidcLogoutEndpoint_MapKunBackChannelLogout_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_System_String_System_String\_}

Maps the form-post logout endpoint for one registered provider. Returns 200 on success/replay and 400 on rejection.

```csharp
public static IEndpointConventionBuilder MapKunBackChannelLogout(this IEndpointRouteBuilder endpoints, string pattern, string clientName)
```

#### Parameters

`endpoints` [IEndpointRouteBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder)

`pattern` [string](https://learn.microsoft.com/dotnet/api/system.string)

`clientName` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[IEndpointConventionBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder)
