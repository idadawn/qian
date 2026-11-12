> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IJwtValidator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Validates a Bearer JWT against the configured issuer / audience /
signing keys and returns the resulting [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)
on success. Returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when validation fails —
callers decide whether to surface a 401 or fall through.

```csharp
public interface IJwtValidator
```

## Methods

### ValidateAsync(string, CancellationToken) \{#Kun_Identity_Abstractions_IJwtValidator_ValidateAsync_System_String_System_Threading_CancellationToken\_}

Validates `token`. Keys are resolved from the
configured JWKS URI with automatic rotation, or from statically
registered `IssuerSigningKeys` when set. Expiry, issuer,
audience, and lifetime are all checked; tokens failing any
check return [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

```csharp
Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken = default)
```

#### Parameters

`token` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?>
