> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunIdentityBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Fluent builder surfaced by `AddKunIdentity`.

```csharp
public sealed class KunIdentityBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunIdentityBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunIdentityBuilder(IServiceCollection) \{#Kun_Identity_Configuration_KunIdentityBuilder__ctor_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Creates a builder bound to `services`.

```csharp
public KunIdentityBuilder(IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Properties

### Services \{#Kun_Identity_Configuration_KunIdentityBuilder_Services}

The underlying service collection.

```csharp
public IServiceCollection Services { get; }
```

#### Property Value

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Methods

### AddCookieSession(Action\<KunCookieSessionBuilder>) \{#Kun_Identity_Configuration_KunIdentityBuilder_AddCookieSession_System_Action_Kun_Identity_Configuration_KunCookieSessionBuilder\_\_}

Registers the cookie-session pipeline described in
`ADR-010`: a `KunCookieAuthOptions` options entry, a
default [`TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider), and the
`ISessionStore` backend the caller selects via
`opts.UseInMemoryStore()` or `opts.UseRedisStore()`.

```csharp
public KunIdentityBuilder AddCookieSession(Action<KunCookieSessionBuilder> configure)
```

#### Parameters

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunCookieSessionBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)>

#### Returns

[KunIdentityBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)

#### Remarks

The `UseKunCookieAuth()` middleware,
`SignInHelper`, `ChallengeHelper`, and
`KunCurrentUser` DI entries live in the companion package
`Kun.Identity.AspNetCore`; callers wiring a browser-flow
service must reference that package in addition to calling
[KunIdentityBuilder.AddCookieSession](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md#Kun_Identity_Configuration_KunIdentityBuilder_AddCookieSession_System_Action_Kun_Identity_Configuration_KunCookieSessionBuilder__).

If neither `UseInMemoryStore` nor `UseRedisStore` is
called within the lambda, resolving `ISessionStore` from
DI will throw — the registration is intentionally not defaulted
because an unintended in-memory store in production would be a
subtle data-loss / cross-replica-drift bug.

### AddJwtBearer(Action\<KunJwtBearerOptions>) \{#Kun_Identity_Configuration_KunIdentityBuilder_AddJwtBearer_System_Action_Kun_Identity_Configuration_KunJwtBearerOptions\_\_}

Registers [IJwtValidator](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ijwtvalidator.md) configured for external
Bearer JWTs. Subsequent calls replace the existing options so
consumers can re-configure without double-registration.

```csharp
public KunIdentityBuilder AddJwtBearer(Action<KunJwtBearerOptions> configure)
```

#### Parameters

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunJwtBearerOptions](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md)>

#### Returns

[KunIdentityBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)

### AddOidcClient(string, Action\<KunOidcClientOptions>) \{#Kun_Identity_Configuration_KunIdentityBuilder_AddOidcClient_System_String_System_Action_Kun_Identity_Configuration_KunOidcClientOptions\_\_}

Registers a named [IOidcClient](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ioidcclient.md). Multiple calls with
distinct `name` values register multiple
clients — resolve them from DI via the registered
`IEnumerable<IOidcClient>` and match by
[IOidcClient.Name](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ioidcclient.md#Kun_Identity_Abstractions_IOidcClient_Name).

```csharp
public KunIdentityBuilder AddOidcClient(string name, Action<KunOidcClientOptions> configure)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunOidcClientOptions](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunoidcclientoptions.md)>

#### Returns

[KunIdentityBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)

### AddS2SIssuer(Action\<KunJwtIssuerOptions>) \{#Kun_Identity_Configuration_KunIdentityBuilder_AddS2SIssuer_System_Action_Kun_Identity_Configuration_KunJwtIssuerOptions\_\_}

Registers [IJwtIssuer](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ijwtissuer.md) for internal S2S token minting.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer.", DiagnosticId = "KUNS2S001")]
public KunIdentityBuilder AddS2SIssuer(Action<KunJwtIssuerOptions> configure)
```

#### Parameters

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunJwtIssuerOptions](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunjwtissueroptions.md)>

#### Returns

[KunIdentityBuilder](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)
