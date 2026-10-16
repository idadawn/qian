> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class JwksResponseBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Jwks](/sdk/api/kun/kun-gateway-jwks/index.md)\
Assembly: Kun.Gateway.dll

Caches the JWKS response keyed on (public-key path + file last-write time).
Re-reads the PEM, rebuilds the [`JsonWebKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkey), and materialises a new
[JwksResponse](/sdk/api/kun/kun-gateway-endpoints/jwksresponse.md) only when the underlying file has been modified —
amortises the per-request cost of every JWKS fetch across downstream services
to a single [`GetLastWriteTimeUtc`](https://learn.microsoft.com/dotnet/api/system.io.file.getlastwritetimeutc) stat after the first warm-up.

```csharp
public sealed class JwksResponseBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwksResponseBuilder](/sdk/api/kun/kun-gateway-jwks/jwksresponsebuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Registered as a DI singleton by `AddKunGateway`. The cache is shared between
the public `MapKunJwks` route and the gated `MapKunInternalJwks` route
so both serve the same byte-identical document and both pay the key-rebuild
cost at most once per key-rotation event.

Thread-safe via a single lock around the cached tuple. The lock scope is tiny
(read the mtime, compare, maybe rebuild) and the rebuild path only fires when
an operator rotates the key — expected to be infrequent relative to request
volume, so lock contention is a non-issue in practice.

## Constructors

### JwksResponseBuilder() \{#Kun_Gateway_Jwks_JwksResponseBuilder__ctor}

```csharp
public JwksResponseBuilder()
```

## Methods

### Build(string) \{#Kun_Gateway_Jwks_JwksResponseBuilder_Build_System_String\_}

Returns the JWKS response for `publicKeyPath`, rebuilding only
when the file has been modified since the last successful build.

```csharp
public JwksResponse Build(string publicKeyPath)
```

#### Parameters

`publicKeyPath` [string](https://learn.microsoft.com/dotnet/api/system.string)

Absolute path to the PEM-encoded RSA public key.

#### Returns

[JwksResponse](/sdk/api/kun/kun-gateway-endpoints/jwksresponse.md)

The cached or freshly-built [JwksResponse](/sdk/api/kun/kun-gateway-endpoints/jwksresponse.md).

#### Exceptions

[FileNotFoundException](https://learn.microsoft.com/dotnet/api/system.io.filenotfoundexception)

The key file does not exist.
