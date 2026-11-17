> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class JwksEndpoint

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Endpoints](/pr-preview/pr-202/sdk/api/kun/kun-gateway-endpoints/index.md)\
Assembly: Kun.Gateway.dll

Extension methods for mapping the public JWKS endpoint.

```csharp
public static class JwksEndpoint
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwksEndpoint](/pr-preview/pr-202/sdk/api/kun/kun-gateway-endpoints/jwksendpoint.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### MapKunJwks(IEndpointRouteBuilder, Func\<HttpContext, bool>?) \{#Kun_Gateway_Endpoints_JwksEndpoint_MapKunJwks_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_System_Func_Microsoft_AspNetCore_Http_HttpContext_System_Boolean\_\_}

Maps `/.well-known/jwks.json` — returns the gateway's RSA public key as a JSON Web Key Set.
The `kid` is derived from the SHA-256 hash of the RSA modulus for stable key identification.
Response is cached by [JwksResponseBuilder](/pr-preview/pr-202/sdk/api/kun/kun-gateway-jwks/jwksresponsebuilder.md) and only rebuilt when the
underlying public-key file is modified.

```csharp
public static IEndpointConventionBuilder MapKunJwks(this IEndpointRouteBuilder endpoints, Func<HttpContext, bool>? additionalGate = null)
```

#### Parameters

`endpoints` [IEndpointRouteBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder)

The endpoint route builder.

`additionalGate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext), [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>?

Optional predicate evaluated on each request. Return [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) to respond 404.
Typical use is a production-only allow-list (e.g. loopback only when this route is
supposed to be reachable only via mesh-internal hops). When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null),
no additional gating is applied.

#### Returns

[IEndpointConventionBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder)

The [`IEndpointConventionBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder) for the mapped route.
