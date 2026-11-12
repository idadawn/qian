> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunJwtValidator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Implementations](/pr-preview/pr-202/sdk/api/kun/kun-identity-implementations/index.md)\
Assembly: Kun.Identity.dll

[IJwtValidator](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/ijwtvalidator.md) backed by
[`JsonWebTokenHandler`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler). Resolves signing keys from the
configured JWKS URI via
[`ConfigurationManager`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.protocols.configurationmanager-1) (with automatic
rotation) or from statically registered keys when supplied. Returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on any validation failure — the caller
decides whether to 401, ignore, or escalate.

```csharp
public sealed class KunJwtValidator : IJwtValidator
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunJwtValidator](/pr-preview/pr-202/sdk/api/kun/kun-identity-implementations/kunjwtvalidator.md)

#### Implements

[IJwtValidator](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/ijwtvalidator.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunJwtValidator(IOptions\<KunJwtBearerOptions>, ILogger\<KunJwtValidator>) \{#Kun_Identity_Implementations_KunJwtValidator__ctor_Microsoft_Extensions_Options_IOptions_Kun_Identity_Configuration_KunJwtBearerOptions__Microsoft_Extensions_Logging_ILogger_Kun_Identity_Implementations_KunJwtValidator\_\_}

Creates a validator bound to the supplied options.

```csharp
public KunJwtValidator(IOptions<KunJwtBearerOptions> options, ILogger<KunJwtValidator> logger)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunJwtBearerOptions](/pr-preview/pr-202/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[KunJwtValidator](/pr-preview/pr-202/sdk/api/kun/kun-identity-implementations/kunjwtvalidator.md)>

## Methods

### ValidateAsync(string, CancellationToken) \{#Kun_Identity_Implementations_KunJwtValidator_ValidateAsync_System_String_System_Threading_CancellationToken\_}

Validates `token`. Keys are resolved from the
configured JWKS URI with automatic rotation, or from statically
registered `IssuerSigningKeys` when set. Expiry, issuer,
audience, and lifetime are all checked; tokens failing any
check return [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

```csharp
public Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken cancellationToken = default)
```

#### Parameters

`token` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?>
