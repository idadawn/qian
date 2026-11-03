> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IReadDbConnectionFactory

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Dapper)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper](/pr-preview/pr-202/sdk/api/kun/kun-dapper/index.md)\
Assembly: Kun.Dapper.dll

Creates read-only database connections for query handlers.

```csharp
public interface IReadDbConnectionFactory
```

## Methods

### OpenAsync(CancellationToken) \{#Kun_Dapper_IReadDbConnectionFactory_OpenAsync_System_Threading_CancellationToken\_}

Opens and returns a new read connection.

```csharp
Task<IDbConnection> OpenAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IDbConnection](https://learn.microsoft.com/dotnet/api/system.data.idbconnection)>

#### Remarks

The caller is responsible for disposing the returned connection.
