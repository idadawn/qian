> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunMultiTenancyOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.Configuration](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/index.md)\
Assembly: Kun.MultiTenancy.dll

Options for `AddKunMultiTenancy`.

```csharp
public sealed class KunMultiTenancyOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyOptions](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunMultiTenancyOptions() \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions__ctor}

```csharp
public KunMultiTenancyOptions()
```

## Properties

### FallbackTenantId \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_FallbackTenantId}

Tenant id returned when no configured source yields a value.
Defaults to `"default"` — flip to [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if
the consumer wants anonymous requests to fall out of
multi-tenant-filtered queries instead of being routed to a
shared "default" bucket.

```csharp
public string? FallbackTenantId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### HttpHeaderNames \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_HttpHeaderNames}

HTTP header names consulted in order by `DefaultTenantResolver`. Add via [KunMultiTenancyOptions.UseHttpHeader](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_UseHttpHeader_System_String_).

```csharp
public List<string> HttpHeaderNames { get; }
```

#### Property Value

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### JwtClaimNames \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_JwtClaimNames}

JWT claim names consulted in order by `DefaultTenantResolver`. Add via [KunMultiTenancyOptions.UseJwtClaim](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_UseJwtClaim_System_String_).

```csharp
public List<string> JwtClaimNames { get; }
```

#### Property Value

[List](https://learn.microsoft.com/dotnet/api/system.collections.generic.list-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Production \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_Production}

生产模式仅接受已认证身份的 tenant_id，拒绝请求头与默认租户回退。

```csharp
public bool Production { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Methods

### UseHttpHeader(string) \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_UseHttpHeader_System_String\_}

Appends `headerName` to the HTTP-header lookup chain.

```csharp
public KunMultiTenancyOptions UseHttpHeader(string headerName)
```

#### Parameters

`headerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[KunMultiTenancyOptions](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md)

### UseJwtClaim(string) \{#Kun_MultiTenancy_Configuration_KunMultiTenancyOptions_UseJwtClaim_System_String\_}

Appends `claimName` to the JWT-claim lookup chain.

```csharp
public KunMultiTenancyOptions UseJwtClaim(string claimName)
```

#### Parameters

`claimName` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[KunMultiTenancyOptions](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md)
