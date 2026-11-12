> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunJwtIssuer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Implementations](/pr-preview/pr-50/sdk/api/kun/kun-identity-implementations/index.md)\
Assembly: Kun.Identity.dll

[IJwtIssuer](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ijwtissuer.md) backed by
[`JsonWebTokenHandler`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler). Signs tokens with the configured
RSA key — supplied either as a PEM string or as a
pre-built [`SecurityKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.securitykey) (the test-friendly path).

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer for canonical short-lived S2S tokens.", DiagnosticId = "KUNS2S001")]
public sealed class KunJwtIssuer : IJwtIssuer
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunJwtIssuer](/pr-preview/pr-50/sdk/api/kun/kun-identity-implementations/kunjwtissuer.md)

#### Implements

[IJwtIssuer](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/ijwtissuer.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunJwtIssuer(IOptions\<KunJwtIssuerOptions>) \{#Kun_Identity_Implementations_KunJwtIssuer__ctor_Microsoft_Extensions_Options_IOptions_Kun_Identity_Configuration_KunJwtIssuerOptions\_\_}

Creates an issuer bound to the supplied options.

```csharp
public KunJwtIssuer(IOptions<KunJwtIssuerOptions> options)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunJwtIssuerOptions](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/kunjwtissueroptions.md)>

## Methods

### Issue(IEnumerable\<Claim>, TimeSpan?) \{#Kun_Identity_Implementations_KunJwtIssuer_Issue_System_Collections_Generic_IEnumerable_System_Security_Claims_Claim__System_Nullable_System_TimeSpan\_\_}

Creates a signed JWT carrying `claims`. When
`lifetime` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the
configured `DefaultLifetime` applies. Returns the compact
serialization ready to place in an `Authorization: Bearer`
header.

```csharp
public string Issue(IEnumerable<Claim> claims, TimeSpan? lifetime = null)
```

#### Parameters

`claims` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[Claim](https://learn.microsoft.com/dotnet/api/system.security.claims.claim)>

`lifetime` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)
