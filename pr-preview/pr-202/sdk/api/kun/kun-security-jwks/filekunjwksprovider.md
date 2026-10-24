> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class FileKunJwksProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Jwks](/pr-preview/pr-202/sdk/api/kun/kun-security-jwks/index.md)\
Assembly: Kun.Security.dll

Loads a JSON Web Key Set from a local file and caches it in memory.
The cache is invalidated when the file's last-write time changes, enabling
key rotation without service restart.

```csharp
public sealed class FileKunJwksProvider : IKunJwksProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[FileKunJwksProvider](/pr-preview/pr-202/sdk/api/kun/kun-security-jwks/filekunjwksprovider.md)

#### Implements

[IKunJwksProvider](/pr-preview/pr-202/sdk/api/kun/kun-security-jwks/ikunjwksprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### FileKunJwksProvider(string, ILogger\<FileKunJwksProvider>) \{#Kun_Security_Jwks_FileKunJwksProvider__ctor_System_String_Microsoft_Extensions_Logging_ILogger_Kun_Security_Jwks_FileKunJwksProvider\_\_}

Initializes the provider with the path to the JWKS JSON file.

```csharp
public FileKunJwksProvider(string filePath, ILogger<FileKunJwksProvider> logger)
```

#### Parameters

`filePath` [string](https://learn.microsoft.com/dotnet/api/system.string)

Absolute path to the JWKS JSON file.

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[FileKunJwksProvider](/pr-preview/pr-202/sdk/api/kun/kun-security-jwks/filekunjwksprovider.md)>

Logger for cache reload diagnostics.

## Methods

### GetAsync(CancellationToken) \{#Kun_Security_Jwks_FileKunJwksProvider_GetAsync_System_Threading_CancellationToken\_}

Returns the current [`JsonWebKeySet`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkeyset).

```csharp
public Task<JsonWebKeySet> GetAsync(CancellationToken ct = default)
```

#### Parameters

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[JsonWebKeySet](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkeyset)>
