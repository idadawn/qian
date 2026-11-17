> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class ApolloConfigurationExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration.Apollo` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration.Apollo`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Configuration.Apollo)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Apollo](/pr-preview/pr-50/sdk/api/kun/kun-configuration-apollo/index.md)\
Assembly: Kun.Configuration.Apollo.dll

显式添加 Apollo；源顺序由宿主按 appsettings、Apollo、Secret、环境变量、命令行安排。

```csharp
public static class ApolloConfigurationExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ApolloConfigurationExtensions](/pr-preview/pr-50/sdk/api/kun/kun-configuration-apollo/apolloconfigurationextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddApollo(IConfigurationBuilder, ApolloOptions, HttpClient?, ILogger?) \{#Kun_Configuration_Apollo_ApolloConfigurationExtensions_AddApollo_Microsoft_Extensions_Configuration_IConfigurationBuilder_Kun_Configuration_Apollo_ApolloOptions_System_Net_Http_HttpClient_Microsoft_Extensions_Logging_ILogger\_}

添加平台级 Apollo 配置源，HttpClient 由传入方管理或由 provider 自建。

```csharp
public static IConfigurationBuilder AddApollo(this IConfigurationBuilder builder, ApolloOptions options, HttpClient? client = null, ILogger? logger = null)
```

#### Parameters

`builder` [IConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationbuilder)

`options` [ApolloOptions](/pr-preview/pr-50/sdk/api/kun/kun-configuration-apollo/apollooptions.md)

`client` [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)?

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)?

#### Returns

[IConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.configuration.iconfigurationbuilder)
