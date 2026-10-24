> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunOidcClientOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Per-provider configuration for `KunOidcClient`.

```csharp
public sealed class KunOidcClientOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunOidcClientOptions](/sdk/api/kun/kun-identity-configuration/kunoidcclientoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunOidcClientOptions() \{#Kun_Identity_Configuration_KunOidcClientOptions__ctor}

```csharp
public KunOidcClientOptions()
```

## Properties

### Authority \{#Kun_Identity_Configuration_KunOidcClientOptions_Authority}

Provider authority — used to resolve the OIDC discovery
document at `{Authority}/.well-known/openid-configuration`.
The trailing slash is normalised by the client.

```csharp
public string Authority { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ClientId \{#Kun_Identity_Configuration_KunOidcClientOptions_ClientId}

OIDC client id registered with the provider.

```csharp
public string ClientId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ClientSecret \{#Kun_Identity_Configuration_KunOidcClientOptions_ClientSecret}

OIDC client secret. Leave empty when the provider uses PKCE / public clients.

```csharp
public string ClientSecret { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### DiscoveryRefreshInterval \{#Kun_Identity_Configuration_KunOidcClientOptions_DiscoveryRefreshInterval}

Maximum metadata cache age. Defaults to five minutes; zero forces a refresh for every operation.

```csharp
public TimeSpan DiscoveryRefreshInterval { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Name \{#Kun_Identity_Configuration_KunOidcClientOptions_Name}

Logical name for this client; used as the DI key.

```csharp
public string Name { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RequireHttpsMetadata \{#Kun_Identity_Configuration_KunOidcClientOptions_RequireHttpsMetadata}

Require HTTPS for provider metadata and advertised endpoints. Disable only for local test providers.

```csharp
public bool RequireHttpsMetadata { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
