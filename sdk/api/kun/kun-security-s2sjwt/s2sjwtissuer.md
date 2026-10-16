> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class S2SJwtIssuer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

ES256 implementation of [IS2SJwtIssuer](/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md). Emits the SAD v3.0 §12 claim
set (`sub`, `tenant_id`, `roles`, optional `membership_id`,
optional `project_id`, optional `trace_id`) and retains the pre-existing
`scope` / `session_id` claims for OAuth2-style callers.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public sealed class S2SJwtIssuer : IS2SJwtIssuer
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S2SJwtIssuer](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuer.md)

#### Implements

[IS2SJwtIssuer](/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S2SJwtIssuer(IOptions\<S2SJwtOptions>, ILogger\<S2SJwtIssuer>) \{#Kun_Security_S2SJwt_S2SJwtIssuer__ctor_Microsoft_Extensions_Options_IOptions_Kun_Security_S2SJwt_S2SJwtOptions__Microsoft_Extensions_Logging_ILogger_Kun_Security_S2SJwt_S2SJwtIssuer\_\_}

Initializes a new instance of the [S2SJwtIssuer](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuer.md) class.

```csharp
public S2SJwtIssuer(IOptions<S2SJwtOptions> options, ILogger<S2SJwtIssuer> logger)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[S2SJwtOptions](/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[S2SJwtIssuer](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuer.md)>

## Methods

### IssueAsync(S2SJwtIssueRequest, CancellationToken) \{#Kun_Security_S2SJwt_S2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken\_}

Issues a new S2S JWT token matching the SAD v3.0 §12 claim contract.

```csharp
public Task<string> IssueAsync(S2SJwtIssueRequest request, CancellationToken ct = default)
```

#### Parameters

`request` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)

The subject, tenant, audience, and optional claim inputs.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

The encoded JWT string.

### IssueAsync(string, string, string\[], string\[], string, CancellationToken) \{#Kun_Security_S2SJwt_S2SJwtIssuer_IssueAsync_System_String_System_String_System_String___System_String___System_String_System_Threading_CancellationToken\_}

Convenience overload that composes a [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md) from the
commonly used five fields and delegates to [IS2SJwtIssuer.IssueAsync](/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_).

```csharp
public Task<string> IssueAsync(string userId, string tenantId, string[] scopes, string[] audience, string sessionId, CancellationToken ct = default)
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
