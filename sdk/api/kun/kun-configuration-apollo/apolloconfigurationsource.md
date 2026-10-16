> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ApolloConfigurationSource

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration.Apollo` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration.Apollo`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Configuration.Apollo)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Apollo](/sdk/api/kun/kun-configuration-apollo/index.md)\
Assembly: Kun.Configuration.Apollo.dll

Apollo 标准 IConfiguration 源。

```csharp
public sealed class ApolloConfigurationSource : IConfigurationSource
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ApolloConfigurationSource](/sdk/api/kun/kun-configuration-apollo/apolloconfigurationsource.md)

#### Implements

[IConfigurationSource](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationsource)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ApolloConfigurationSource(ApolloOptions, HttpClient?, ILogger?) \{#Kun_Configuration_Apollo_ApolloConfigurationSource__ctor_Kun_Configuration_Apollo_ApolloOptions_System_Net_Http_HttpClient_Microsoft_Extensions_Logging_ILogger\_}

Apollo 标准 IConfiguration 源。

```csharp
public ApolloConfigurationSource(ApolloOptions options, HttpClient? client = null, ILogger? logger = null)
```

#### Parameters

`options` [ApolloOptions](/sdk/api/kun/kun-configuration-apollo/apollooptions.md)

`client` [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)?

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)?

## Methods

### Build(IConfigurationBuilder) \{#Kun_Configuration_Apollo_ApolloConfigurationSource_Build_Microsoft_Extensions_Configuration_IConfigurationBuilder\_}

```csharp
public IConfigurationProvider Build(IConfigurationBuilder builder)
```

#### Parameters

`builder` [IConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationbuilder)

#### Returns

[IConfigurationProvider](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationprovider)
