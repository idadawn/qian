> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IS2sPrincipalResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Gateway)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Identity](/sdk/api/kun/kun-gateway-identity/index.md)\
Assembly: Kun.Gateway.dll

Optional enrichment hook invoked by [S2sTokenIssuer](/sdk/api/kun/kun-gateway/s2stokenissuer.md) after the external IdP
principal authenticates and before the internal S2S token is minted. Lets consumers add,
remove, or rewrite claims — typically for tenant lookup, role projection, or attaching
service-scoped metadata that the IdP does not own.

```csharp
public interface IS2sPrincipalResolver
```

## Remarks

The resolver runs on the Gateway's request path, so its implementation should be cheap:
prefer cache-backed lookups over per-request database calls. Failures should surface as
exceptions rather than empty principals. The issuer rejects unauthenticated/empty principals
and missing canonical identity fields; session and authorization revisions must come from an authoritative source.

## Methods

### ResolveAsync(ClaimsPrincipal, CancellationToken) \{#Kun_Gateway_Identity_IS2sPrincipalResolver_ResolveAsync_System_Security_Claims_ClaimsPrincipal_System_Threading_CancellationToken\_}

Returns the principal that [S2sTokenIssuer.IssueFor](/sdk/api/kun/kun-gateway/s2stokenissuer.md#Kun_Gateway_S2sTokenIssuer_IssueFor_System_Security_Claims_ClaimsPrincipal_) should
translate into the internal S2S token. Implementations may return
`externalPrincipal` unchanged (no-op) or a new
[`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) with enriched claims.

```csharp
Task<ClaimsPrincipal> ResolveAsync(ClaimsPrincipal externalPrincipal, CancellationToken cancellationToken)
```

#### Parameters

`externalPrincipal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)

The authenticated principal from the IdP JWT.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the enrichment call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)>

The principal to mint the token for.
