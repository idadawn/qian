> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class NpgsqlReadDbConnectionFactory

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Dapper)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper.Npgsql](/pr-preview/pr-122/sdk/api/kun/kun-dapper-npgsql/index.md)\
Assembly: Kun.Dapper.dll

Npgsql-backed [IReadDbConnectionFactory](/pr-preview/pr-122/sdk/api/kun/kun-dapper/ireaddbconnectionfactory.md).
Opens a fresh connection per call; the caller is responsible for disposing it.

```csharp
public sealed class NpgsqlReadDbConnectionFactory : IReadDbConnectionFactory
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NpgsqlReadDbConnectionFactory](/pr-preview/pr-122/sdk/api/kun/kun-dapper-npgsql/npgsqlreaddbconnectionfactory.md)

#### Implements

[IReadDbConnectionFactory](/pr-preview/pr-122/sdk/api/kun/kun-dapper/ireaddbconnectionfactory.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NpgsqlReadDbConnectionFactory(ReadDbOptions) \{#Kun_Dapper_Npgsql_NpgsqlReadDbConnectionFactory__ctor_Kun_Dapper_ReadDbOptions\_}

Initializes a new [NpgsqlReadDbConnectionFactory](/pr-preview/pr-122/sdk/api/kun/kun-dapper-npgsql/npgsqlreaddbconnectionfactory.md).

```csharp
public NpgsqlReadDbConnectionFactory(ReadDbOptions options)
```

#### Parameters

`options` [ReadDbOptions](/pr-preview/pr-122/sdk/api/kun/kun-dapper/readdboptions.md)

## Methods

### OpenAsync(CancellationToken) \{#Kun_Dapper_Npgsql_NpgsqlReadDbConnectionFactory_OpenAsync_System_Threading_CancellationToken\_}

Opens and returns a new read connection.

```csharp
public Task<IDbConnection> OpenAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IDbConnection](https://learn.microsoft.com/dotnet/api/system.data.idbconnection)>

#### Remarks

The caller is responsible for disposing the returned connection.
