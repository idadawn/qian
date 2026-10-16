> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class ApolloConfigurationProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration.Apollo` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration.Apollo`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Configuration.Apollo)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Apollo](/pr-preview/pr-181/sdk/api/kun/kun-configuration-apollo/index.md)\
Assembly: Kun.Configuration.Apollo.dll

通过公开协议获取服务级配置；无网络且无缓存时启动失败，不以空配置继续。

```csharp
public sealed class ApolloConfigurationProvider : ConfigurationProvider, IConfigurationProvider, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ConfigurationProvider](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider) ←
[ApolloConfigurationProvider](/pr-preview/pr-181/sdk/api/kun/kun-configuration-apollo/apolloconfigurationprovider.md)

#### Implements

[IConfigurationProvider](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationprovider),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[ConfigurationProvider.TryGet(string, out string?)](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.tryget),
[ConfigurationProvider.Set(string, string?)](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.set),
[ConfigurationProvider.Load()](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.load),
[ConfigurationProvider.GetChildKeys(IEnumerable\<string>, string?)](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.getchildkeys),
[ConfigurationProvider.GetReloadToken()](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.getreloadtoken),
[ConfigurationProvider.ToString()](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.configurationprovider.tostring),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ApolloConfigurationProvider(ApolloOptions, HttpClient?, ILogger?) \{#Kun_Configuration_Apollo_ApolloConfigurationProvider__ctor_Kun_Configuration_Apollo_ApolloOptions_System_Net_Http_HttpClient_Microsoft_Extensions_Logging_ILogger\_}

客户端超时须大于长轮询的 60 秒；默认客户端禁止重定向以避免签名泄漏。

```csharp
public ApolloConfigurationProvider(ApolloOptions options, HttpClient? client = null, ILogger? logger = null)
```

#### Parameters

`options` [ApolloOptions](/pr-preview/pr-181/sdk/api/kun/kun-configuration-apollo/apollooptions.md)

`client` [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)?

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)?

## Methods

### Dispose() \{#Kun_Configuration_Apollo_ApolloConfigurationProvider_Dispose}

```csharp
public void Dispose()
```

### Load() \{#Kun_Configuration_Apollo_ApolloConfigurationProvider_Load}

```csharp
public override void Load()
```

### RefreshAsync(CancellationToken) \{#Kun_Configuration_Apollo_ApolloConfigurationProvider_RefreshAsync_System_Threading_CancellationToken\_}

显式刷新全部 namespace；校验失败时原配置与缓存保持不变。

```csharp
public Task RefreshAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
