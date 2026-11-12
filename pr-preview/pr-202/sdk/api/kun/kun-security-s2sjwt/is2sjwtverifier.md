> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IS2SJwtVerifier

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/pr-preview/pr-202/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Verifies S2S JWT tokens issued by the gateway.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public interface IS2SJwtVerifier
```

## Methods

### VerifyAsync(string, string, CancellationToken) \{#Kun_Security_S2SJwt_IS2SJwtVerifier_VerifyAsync_System_String_System_String_System_Threading_CancellationToken\_}

Verifies a token and returns the principal, or `null` if invalid.

```csharp
Task<ClaimsPrincipal?> VerifyAsync(string token, string expectedAudience, CancellationToken ct = default)
```

#### Parameters

`token` [string](https://learn.microsoft.com/dotnet/api/system.string)

The encoded JWT string.

`expectedAudience` [string](https://learn.microsoft.com/dotnet/api/system.string)

The audience the verifier expects.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?>

The validated [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal), or `null` if validation fails.
