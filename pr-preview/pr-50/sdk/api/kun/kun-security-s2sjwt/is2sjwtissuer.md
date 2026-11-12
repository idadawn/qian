> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IS2SJwtIssuer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Security)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Issues S2S JWT tokens signed with ES256 for internal service-to-service authentication.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public interface IS2SJwtIssuer
```

## Methods

### IssueAsync(S2SJwtIssueRequest, CancellationToken) \{#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken\_}

Issues a new S2S JWT token matching the SAD v3.0 §12 claim contract.

```csharp
Task<string> IssueAsync(S2SJwtIssueRequest request, CancellationToken ct = default)
```

#### Parameters

`request` [S2SJwtIssueRequest](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)

The subject, tenant, audience, and optional claim inputs.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

The encoded JWT string.

### IssueAsync(string, string, string\[], string\[], string, CancellationToken) \{#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_System_String_System_String_System_String___System_String___System_String_System_Threading_CancellationToken\_}

Convenience overload that composes a [S2SJwtIssueRequest](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md) from the
commonly used five fields and delegates to [IS2SJwtIssuer.IssueAsync](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_).

```csharp
Task<string> IssueAsync(string userId, string tenantId, string[] scopes, string[] audience, string sessionId, CancellationToken ct = default)
```

#### Parameters

`userId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Subject identifier (JWT `sub`).

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Tenant identifier claim.

`scopes` [string](https://learn.microsoft.com/dotnet/api/system.string)\[]

Scope tokens joined with spaces into the `scope` claim.

`audience` [string](https://learn.microsoft.com/dotnet/api/system.string)\[]

Token audience list (`aud`).

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Originating session identifier (`session_id`).

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>
