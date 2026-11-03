> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IJwtIssuer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Mints signed JWTs for internal service-to-service calls. The
issuer, audience, signing key, and default lifetime come from the
registered `KunJwtIssuerOptions`; callers pass the claims
describing _this_ token (subject, tenant, roles, …).

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer for canonical short-lived S2S tokens.", DiagnosticId = "KUNS2S001")]
public interface IJwtIssuer
```

## Methods

### Issue(IEnumerable\<Claim>, TimeSpan?) \{#Kun_Identity_Abstractions_IJwtIssuer_Issue_System_Collections_Generic_IEnumerable_System_Security_Claims_Claim__System_Nullable_System_TimeSpan\_\_}

Creates a signed JWT carrying `claims`. When
`lifetime` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the
configured `DefaultLifetime` applies. Returns the compact
serialization ready to place in an `Authorization: Bearer`
header.

```csharp
string Issue(IEnumerable<Claim> claims, TimeSpan? lifetime = null)
```

#### Parameters

`claims` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[Claim](https://learn.microsoft.com/dotnet/api/system.security.claims.claim)>

`lifetime` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)
