> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class HttpClientBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Extensions](/pr-preview/pr-181/sdk/api/kun/kun-security-extensions/index.md)\
Assembly: Kun.Security.dll

Extension methods on [`IHttpClientBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihttpclientbuilder) for Kun S2S auth propagation.

```csharp
public static class HttpClientBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[HttpClientBuilderExtensions](/pr-preview/pr-181/sdk/api/kun/kun-security-extensions/httpclientbuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunServiceAuth(IHttpClientBuilder) \{#Kun_Security_Extensions_HttpClientBuilderExtensions_AddKunServiceAuth_Microsoft_Extensions_DependencyInjection_IHttpClientBuilder\_}

Chains [PropagateS2sTokenHandler](/pr-preview/pr-181/sdk/api/kun/kun-security-http/propagates2stokenhandler.md) onto the HTTP client pipeline,
so the S2S JWT from the current incoming request is forwarded to downstream services.

```csharp
public static IHttpClientBuilder AddKunServiceAuth(this IHttpClientBuilder builder)
```

#### Parameters

`builder` [IHttpClientBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihttpclientbuilder)

The HTTP client builder.

#### Returns

[IHttpClientBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.ihttpclientbuilder)

The builder for chaining.
