> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class JwksResponse

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Endpoints](/pr-preview/pr-181/sdk/api/kun/kun-gateway-endpoints/index.md)\
Assembly: Kun.Gateway.dll

JSON Web Key Set response model.

```csharp
public sealed class JwksResponse
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwksResponse](/pr-preview/pr-181/sdk/api/kun/kun-gateway-endpoints/jwksresponse.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### JwksResponse() \{#Kun_Gateway_Endpoints_JwksResponse__ctor}

```csharp
public JwksResponse()
```

## Properties

### Keys \{#Kun_Gateway_Endpoints_JwksResponse_Keys}

The list of public JSON Web Keys.

```csharp
[JsonPropertyName("keys")]
public required IReadOnlyList<JwkDto> Keys { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[JwkDto](/pr-preview/pr-181/sdk/api/kun/kun-gateway-endpoints/jwkdto.md)>
