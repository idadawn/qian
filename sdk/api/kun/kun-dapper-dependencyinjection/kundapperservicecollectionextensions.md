> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunDapperServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Dapper)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper.DependencyInjection](/sdk/api/kun/kun-dapper-dependencyinjection/index.md)\
Assembly: Kun.Dapper.dll

Registers Kun.Dapper services into the DI container.

```csharp
public static class KunDapperServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunDapperServiceCollectionExtensions](/sdk/api/kun/kun-dapper-dependencyinjection/kundapperservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunDapper(IServiceCollection, Action\<ReadDbOptions>) \{#Kun_Dapper_DependencyInjection_KunDapperServiceCollectionExtensions_AddKunDapper_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Dapper_ReadDbOptions\_\_}

Adds the read-side connection factory and Dapper defaults.

```csharp
public static IServiceCollection AddKunDapper(this IServiceCollection services, Action<ReadDbOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md)>

Callback to configure [ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Remarks

Use this overload when you supply your own [IReadDbConnectionFactory](/sdk/api/kun/kun-dapper/ireaddbconnectionfactory.md)
registration. For PostgreSQL projects, prefer [KunDapperServiceCollectionExtensions.AddKunDapperNpgsql](/sdk/api/kun/kun-dapper-dependencyinjection/kundapperservicecollectionextensions.md#Kun_Dapper_DependencyInjection_KunDapperServiceCollectionExtensions_AddKunDapperNpgsql_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Func_Kun_Dapper_ReadDbOptions__).

### AddKunDapper(IServiceCollection, Func\<ReadDbOptions>) \{#Kun_Dapper_DependencyInjection_KunDapperServiceCollectionExtensions_AddKunDapper_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Func_Kun_Dapper_ReadDbOptions\_\_}

通过工厂创建不可变 options，支持 init-only ConnectionString；兼容保留旧 Action 重载。

```csharp
public static IServiceCollection AddKunDapper(this IServiceCollection services, Func<ReadDbOptions> createOptions)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`createOptions` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddKunDapperNpgsql(IServiceCollection, Action\<ReadDbOptions>) \{#Kun_Dapper_DependencyInjection_KunDapperServiceCollectionExtensions_AddKunDapperNpgsql_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Dapper_ReadDbOptions\_\_}

Adds the read-side connection factory backed by Npgsql (PostgreSQL).
Registers [ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md) as a singleton and
[NpgsqlReadDbConnectionFactory](/sdk/api/kun/kun-dapper-npgsql/npgsqlreaddbconnectionfactory.md) as the [IReadDbConnectionFactory](/sdk/api/kun/kun-dapper/ireaddbconnectionfactory.md).

```csharp
public static IServiceCollection AddKunDapperNpgsql(this IServiceCollection services, Action<ReadDbOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md)>

Callback to configure [ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddKunDapperNpgsql(IServiceCollection, Func\<ReadDbOptions>) \{#Kun_Dapper_DependencyInjection_KunDapperServiceCollectionExtensions_AddKunDapperNpgsql_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Func_Kun_Dapper_ReadDbOptions\_\_}

通过不可变 options 工厂装配 PostgreSQL 读侧连接。

```csharp
public static IServiceCollection AddKunDapperNpgsql(this IServiceCollection services, Func<ReadDbOptions> createOptions)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`createOptions` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[ReadDbOptions](/sdk/api/kun/kun-dapper/readdboptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
