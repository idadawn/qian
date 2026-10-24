> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ApolloOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration.Apollo` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration.Apollo`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Configuration.Apollo)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Apollo](/sdk/api/kun/kun-configuration-apollo/index.md)\
Assembly: Kun.Configuration.Apollo.dll

平台／服务级 Apollo 引导配置；不承载租户级配置或明文业务密钥。

```csharp
public sealed class ApolloOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ApolloOptions](/sdk/api/kun/kun-configuration-apollo/apollooptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ApolloOptions() \{#Kun_Configuration_Apollo_ApolloOptions__ctor}

```csharp
public ApolloOptions()
```

## Properties

### AppId \{#Kun_Configuration_Apollo_ApolloOptions_AppId}

应用标识。

```csharp
public required string AppId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CacheDirectory \{#Kun_Configuration_Apollo_ApolloOptions_CacheDirectory}

缓存目录；null 禁用磁盘缓存。

```csharp
public string? CacheDirectory { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Cluster \{#Kun_Configuration_Apollo_ApolloOptions_Cluster}

集群标识。

```csharp
public string Cluster { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ConfigServers \{#Kun_Configuration_Apollo_ApolloOptions_ConfigServers}

已允许访问的 ConfigService 端点，按顺序故障转移。

```csharp
public required IReadOnlyList<Uri> ConfigServers { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<Uri>

### Label \{#Kun_Configuration_Apollo_ApolloOptions_Label}

可选实例灰度标签。

```csharp
public string? Label { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Namespaces \{#Kun_Configuration_Apollo_ApolloOptions_Namespaces}

按公共、插件、application 顺序叠加的 properties namespace。

```csharp
public IReadOnlyList<string> Namespaces { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### ReloadOnChange \{#Kun_Configuration_Apollo_ApolloOptions_ReloadOnChange}

启动后台长轮询；测试或显式刷新宿主可关闭。

```csharp
public bool ReloadOnChange { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ResolveAccessKey \{#Kun_Configuration_Apollo_ApolloOptions_ResolveAccessKey}

在每次签名前由宿主解析 AccessKey；不写入配置快照、缓存或日志。

```csharp
public Func<string?>? ResolveAccessKey { get; init; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?>?

### RetryDelay \{#Kun_Configuration_Apollo_ApolloOptions_RetryDelay}

连接失败后的重试间隔。

```csharp
public TimeSpan RetryDelay { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Validate \{#Kun_Configuration_Apollo_ApolloOptions_Validate}

可选的整份快照校验，失败时不替换数据与磁盘缓存。

```csharp
public Func<IReadOnlyDictionary<string, string?>, bool>? Validate { get; init; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)?>, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>?

### ValidateNamespace \{#Kun_Configuration_Apollo_ApolloOptions_ValidateNamespace}

每份原始 namespace 在合并与缓存前校验；离线缓存读取时同样执行。

```csharp
public Func<string, IReadOnlyDictionary<string, string?>, bool>? ValidateNamespace { get; init; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)?>, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>?
