> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class NoOpS2sPrincipalResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Identity](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/index.md)\
Assembly: Kun.Gateway.dll

Default [IS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md) that returns the authenticated external principal
unchanged. Consumers that do not need principal enrichment get this for free via
`AddKunGateway`; anyone wiring a custom resolver should register their implementation
with `services.AddSingleton<IS2sPrincipalResolver, MyResolver>()` before
`AddKunGateway` runs.

```csharp
public sealed class NoOpS2sPrincipalResolver : IS2sPrincipalResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NoOpS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/noops2sprincipalresolver.md)

#### Implements

[IS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/is2sprincipalresolver.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NoOpS2sPrincipalResolver() \{#Kun_Gateway_Identity_NoOpS2sPrincipalResolver__ctor}

```csharp
public NoOpS2sPrincipalResolver()
```

## Properties

### Instance \{#Kun_Gateway_Identity_NoOpS2sPrincipalResolver_Instance}

Shared instance — the resolver is stateless.

```csharp
public static NoOpS2sPrincipalResolver Instance { get; }
```

#### Property Value

[NoOpS2sPrincipalResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-identity/noops2sprincipalresolver.md)

## Methods

### ResolveAsync(ClaimsPrincipal, CancellationToken) \{#Kun_Gateway_Identity_NoOpS2sPrincipalResolver_ResolveAsync_System_Security_Claims_ClaimsPrincipal_System_Threading_CancellationToken\_}

Returns the principal that [S2sTokenIssuer.IssueFor](/pr-preview/pr-202/sdk/api/kun/kun-gateway/s2stokenissuer.md#Kun_Gateway_S2sTokenIssuer_IssueFor_System_Security_Claims_ClaimsPrincipal_) should
translate into the internal S2S token. Implementations may return
`externalPrincipal` unchanged (no-op) or a new
[`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) with enriched claims.

```csharp
public Task<ClaimsPrincipal> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
```

#### Parameters

`externalPrincipal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)

The authenticated principal from the IdP JWT.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the enrichment call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)>

The principal to mint the token for.
