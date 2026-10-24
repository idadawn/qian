> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunCookieSessionBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Configuration surface exposed by
`KunIdentityBuilder.AddCookieSession(opts => ...)`. Inherits
every [KunCookieAuthOptions](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md) field (so callers write
`opts.CookieName = "kun.sid"; opts.SessionTtl = ...;` as
expected) and adds the backend-selection methods
[KunCookieSessionBuilder.UseInMemoryStore](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md#Kun_Identity_Configuration_KunCookieSessionBuilder_UseInMemoryStore) and [KunCookieSessionBuilder.UseRedisStore](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md#Kun_Identity_Configuration_KunCookieSessionBuilder_UseRedisStore).
Exactly one backend call should be made — multiple calls take
last-wins semantics because each call replaces the
[ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) registration on the underlying service
collection.

```csharp
public sealed class KunCookieSessionBuilder : KunCookieAuthOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCookieAuthOptions](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md) ←
[KunCookieSessionBuilder](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)

#### Inherited Members

[KunCookieAuthOptions.CookieName](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_CookieName),
[KunCookieAuthOptions.Domain](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Domain),
[KunCookieAuthOptions.Path](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Path),
[KunCookieAuthOptions.HttpOnly](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_HttpOnly),
[KunCookieAuthOptions.SameSite](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SameSite),
[KunCookieAuthOptions.Secure](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Secure),
[KunCookieAuthOptions.SessionTtl](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SessionTtl),
[KunCookieAuthOptions.SlidingExpiration](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SlidingExpiration),
[KunCookieAuthOptions.RequireAntiforgery](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_RequireAntiforgery),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunCookieSessionBuilder(IServiceCollection) \{#Kun_Identity_Configuration_KunCookieSessionBuilder__ctor_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Creates the builder bound to `services`.

```csharp
public KunCookieSessionBuilder(IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Methods

### UseInMemoryStore() \{#Kun_Identity_Configuration_KunCookieSessionBuilder_UseInMemoryStore}

Registers [InMemorySessionStore](/sdk/api/kun/kun-identity-implementations-session/inmemorysessionstore.md) as the backend.
Suitable for integration tests and single-process development.
Adds a [`TimeProvider`](https://learn.microsoft.com/dotnet/api/system.timeprovider) if none is already registered.

```csharp
public KunCookieSessionBuilder UseInMemoryStore()
```

#### Returns

[KunCookieSessionBuilder](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)

### UseRedisStore() \{#Kun_Identity_Configuration_KunCookieSessionBuilder_UseRedisStore}

Registers [RedisSessionStore](/sdk/api/kun/kun-identity-implementations-session/redissessionstore.md) as the backend. The
caller registers IConnectionMultiplexer, or configures Kun.Caching UseRedis.
A direct Redis connection is required for atomic indexes and revocation; no memory fallback is used.

```csharp
public KunCookieSessionBuilder UseRedisStore()
```

#### Returns

[KunCookieSessionBuilder](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)

### UseRedisStore(string) \{#Kun_Identity_Configuration_KunCookieSessionBuilder_UseRedisStore_System_String\_}

Registers the atomic Redis backend under an explicitly isolated application prefix.

```csharp
public KunCookieSessionBuilder UseRedisStore(string keyPrefix)
```

#### Parameters

`keyPrefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[KunCookieSessionBuilder](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)
