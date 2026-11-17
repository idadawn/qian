> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class JwksEndpointMiddleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

ASP.NET Core middleware that serves the JWKS at `/.well-known/jwks.json`.
Includes both the current and previous signing keys (for graceful rotation).

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public sealed class JwksEndpointMiddleware
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwksEndpointMiddleware](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/jwksendpointmiddleware.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### JwksEndpointMiddleware(RequestDelegate, IOptions\<S2SJwtOptions>, ILogger\<JwksEndpointMiddleware>) \{#Kun_Security_S2SJwt_JwksEndpointMiddleware__ctor_Microsoft_AspNetCore_Http_RequestDelegate_Microsoft_Extensions_Options_IOptions_Kun_Security_S2SJwt_S2SJwtOptions__Microsoft_Extensions_Logging_ILogger_Kun_Security_S2SJwt_JwksEndpointMiddleware\_\_}

Initializes a new instance of the [JwksEndpointMiddleware](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/jwksendpointmiddleware.md) class.

```csharp
public JwksEndpointMiddleware(RequestDelegate next, IOptions<S2SJwtOptions> options, ILogger<JwksEndpointMiddleware> logger)
```

#### Parameters

`next` [RequestDelegate](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.requestdelegate)

The next middleware in the pipeline.

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[S2SJwtOptions](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md)>

S2S JWT signing options.

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[JwksEndpointMiddleware](/pr-preview/pr-122/sdk/api/kun/kun-security-s2sjwt/jwksendpointmiddleware.md)>

Logger for the middleware.

## Methods

### InvokeAsync(HttpContext) \{#Kun_Security_S2SJwt_JwksEndpointMiddleware_InvokeAsync_Microsoft_AspNetCore_Http_HttpContext\_}

Invokes the middleware, serving the JWKS document when the request matches the JWKS endpoint path.

```csharp
public Task InvokeAsync(HttpContext context)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

The current HTTP context.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
