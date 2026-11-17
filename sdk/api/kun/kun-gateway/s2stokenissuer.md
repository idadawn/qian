> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class S2sTokenIssuer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Gateway)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway](/sdk/api/kun/kun-gateway/index.md)\
Assembly: Kun.Gateway.dll

Issues the closed, short-lived S2S claim contract with RS256 and the shared rotating JWKS ring.
An authenticated principal/resolver must provide explicit user/service identity, tenant context and user session/revision fields.
Trace context travels in headers, not additional JWT payload fields.

```csharp
public sealed class S2sTokenIssuer : IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S2sTokenIssuer](/sdk/api/kun/kun-gateway/s2stokenissuer.md)

#### Implements

[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S2sTokenIssuer(IOptions\<KunGatewayOptions>, IHostEnvironment, IS2sPrincipalResolver, IHttpContextAccessor?) \{#Kun_Gateway_S2sTokenIssuer__ctor_Microsoft_Extensions_Options_IOptions_Kun_Gateway_Configuration_KunGatewayOptions__Microsoft_Extensions_Hosting_IHostEnvironment_Kun_Gateway_Identity_IS2sPrincipalResolver_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Initializes the issuer with an explicit principal resolver. Prefer this ctor —
the 3-arg overload is kept for binary compatibility but is deprecated.

```csharp
public S2sTokenIssuer(IOptions<KunGatewayOptions> options, IHostEnvironment env, IS2sPrincipalResolver resolver, IHttpContextAccessor? httpContextAccessor = null)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunGatewayOptions](/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md)>

Gateway configuration options.

`env` [IHostEnvironment](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostenvironment)

Host environment used to differentiate Development from Production.

`resolver` [IS2sPrincipalResolver](/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md)

Principal resolver (use [NoOpS2sPrincipalResolver.Instance](/sdk/api/kun/kun-gateway-identity/noops2sprincipalresolver.md#Kun_Gateway_Identity_NoOpS2sPrincipalResolver_Instance) for the identity transform).

`httpContextAccessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)?

Retained for constructor compatibility. Trace context is now propagated through HTTP headers.

### S2sTokenIssuer(IOptions\<KunGatewayOptions>, IHostEnvironment, IHttpContextAccessor?) \{#Kun_Gateway_S2sTokenIssuer__ctor_Microsoft_Extensions_Options_IOptions_Kun_Gateway_Configuration_KunGatewayOptions__Microsoft_Extensions_Hosting_IHostEnvironment_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Backward-compatible overload without an [IS2sPrincipalResolver](/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md) — falls back
to [NoOpS2sPrincipalResolver.Instance](/sdk/api/kun/kun-gateway-identity/noops2sprincipalresolver.md#Kun_Gateway_Identity_NoOpS2sPrincipalResolver_Instance). Kept for one release so existing
consumers who wired [S2sTokenIssuer](/sdk/api/kun/kun-gateway/s2stokenissuer.md) manually continue to compile.

```csharp
[Obsolete("Use the 4-arg constructor that accepts an IS2sPrincipalResolver. The 3-arg overload will be removed in 0.2.0.", DiagnosticId = "KUN0002", UrlFormat = "https://github.com/idadawn/kun/issues")]
public S2sTokenIssuer(IOptions<KunGatewayOptions> options, IHostEnvironment env, IHttpContextAccessor? httpContextAccessor)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunGatewayOptions](/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md)>

Gateway configuration options.

`env` [IHostEnvironment](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostenvironment)

Host environment used to differentiate Development from Production.

`httpContextAccessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)?

Optional HTTP context accessor (M3 guard for trace-id emission).

## Methods

### Dispose() \{#Kun_Gateway_S2sTokenIssuer_Dispose}

释放通过兼容构造器独立创建的签名状态；DI 共享状态由容器释放。

```csharp
public void Dispose()
```

### IssueFor(ClaimsPrincipal) \{#Kun_Gateway_S2sTokenIssuer_IssueFor_System_Security_Claims_ClaimsPrincipal\_}

Issues an internal S2S JWT for the given external `externalPrincipal`.
The configured [IS2sPrincipalResolver](/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md) runs first to produce the effective
principal, then the canonical identity/scope/session fields are validated and projected.
Missing required identity fields fail closed; arbitrary claims are not forwarded.

```csharp
public string IssueFor(ClaimsPrincipal externalPrincipal)
```

#### Parameters

`externalPrincipal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)

The authenticated principal from the external IdP JWT.

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

A signed RS256 JWT string.

#### Remarks

Synchronous convenience wrapper over [S2sTokenIssuer.IssueForAsync](/sdk/api/kun/kun-gateway/s2stokenissuer.md#Kun_Gateway_S2sTokenIssuer_IssueForAsync_System_Security_Claims_ClaimsPrincipal_System_Threading_CancellationToken_).
Prefer the async form on the Gateway request path — it avoids the
`GetAwaiter().GetResult()` hop and lets resolvers that perform real I/O work
without starving the thread pool.

### IssueForAsync(ClaimsPrincipal, CancellationToken) \{#Kun_Gateway_S2sTokenIssuer_IssueForAsync_System_Security_Claims_ClaimsPrincipal_System_Threading_CancellationToken\_}

Async variant of [S2sTokenIssuer.IssueFor](/sdk/api/kun/kun-gateway/s2stokenissuer.md#Kun_Gateway_S2sTokenIssuer_IssueFor_System_Security_Claims_ClaimsPrincipal_). Awaits the configured
[IS2sPrincipalResolver](/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md) without blocking, then mints the token.

```csharp
public Task<string> IssueForAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken = default)
```

#### Parameters

`externalPrincipal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)

The authenticated principal from the external IdP JWT.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token passed to the resolver.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

A signed RS256 JWT string.

### IssueForAudienceAsync(ClaimsPrincipal, string, CancellationToken) \{#Kun_Gateway_S2sTokenIssuer_IssueForAudienceAsync_System_Security_Claims_ClaimsPrincipal_System_String_System_Threading_CancellationToken\_}

Issues the canonical closed S2S payload for one explicitly selected downstream audience.

```csharp
public Task<string> IssueForAudienceAsync(ClaimsPrincipal externalPrincipal, string audience, CancellationToken cancellationToken = default)
```

#### Parameters

`externalPrincipal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)

`audience` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>
