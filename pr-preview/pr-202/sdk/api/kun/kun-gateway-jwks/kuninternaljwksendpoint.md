> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunInternalJwksEndpoint

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Jwks](/pr-preview/pr-202/sdk/api/kun/kun-gateway-jwks/index.md)\
Assembly: Kun.Gateway.dll

Extension methods for mapping the **internal-network** JWKS endpoint, separate from the
always-on public `/.well-known/jwks.json` route.

```csharp
public static class KunInternalJwksEndpoint
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunInternalJwksEndpoint](/pr-preview/pr-202/sdk/api/kun/kun-gateway-jwks/kuninternaljwksendpoint.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

The public endpoint (see `JwksEndpoint.MapKunJwks`) is required for any consumer — inside
or outside the cluster — to validate S2S tokens, and stays unconditionally enabled. The internal
endpoint at `/internal/.well-known/jwks.json` is an opt-in convenience for downstream
services that talk to the gateway through a NetworkPolicy-isolated interface and want a stable
well-known path that does NOT share lifecycle with the public route (e.g. for separate
rate-limiting or egress controls).

Gating policy: the internal route is reachable only when either (a) the host runs in the
`Development` environment, or (b) [KunGatewayOptions.ExposeInternalJwks](/pr-preview/pr-202/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md#Kun_Gateway_Configuration_KunGatewayOptions_ExposeInternalJwks) is
explicitly set to [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool). Any other environment returns 404, so forgetting to
set the flag in production fails closed.

## Fields

### DefaultPattern \{#Kun_Gateway_Jwks_KunInternalJwksEndpoint_DefaultPattern}

Default path of the gated internal JWKS route.

```csharp
public const string DefaultPattern = "/internal/.well-known/jwks.json"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### MapKunInternalJwks(IEndpointRouteBuilder, string, Func\<HttpContext, bool>?) \{#Kun_Gateway_Jwks_KunInternalJwksEndpoint_MapKunInternalJwks_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_System_String_System_Func_Microsoft_AspNetCore_Http_HttpContext_System_Boolean\_\_}

Maps the internal JWKS endpoint, gated on host environment + options.

```csharp
public static IEndpointConventionBuilder MapKunInternalJwks(this IEndpointRouteBuilder endpoints, string pattern = "/internal/.well-known/jwks.json", Func<HttpContext, bool>? additionalGate = null)
```

#### Parameters

`endpoints` [IEndpointRouteBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.routing.iendpointroutebuilder)

The endpoint route builder.

`pattern` [string](https://learn.microsoft.com/dotnet/api/system.string)

Override the default `/internal/.well-known/jwks.json` path.

`additionalGate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext), [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>?

Optional predicate evaluated on each request after the environment / [KunGatewayOptions.ExposeInternalJwks](/pr-preview/pr-202/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md#Kun_Gateway_Configuration_KunGatewayOptions_ExposeInternalJwks)
check passes. Return [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) to respond 404 (e.g. restrict to RFC1918 peers
when no NetworkPolicy is in place). When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), no additional gating is applied.

#### Returns

[IEndpointConventionBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder)

The [`IEndpointConventionBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.iendpointconventionbuilder) for the mapped route.
