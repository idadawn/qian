> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class CapabilityDescriptor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Mediator)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

命令或查询的稳定标识与 JSON 请求／响应契约。

```csharp
public sealed class CapabilityDescriptor
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[CapabilityDescriptor](/sdk/api/kun/kun-mediator/capabilitydescriptor.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### CapabilityDescriptor(string, JsonTypeInfo, JsonTypeInfo) \{#Kun_Mediator_CapabilityDescriptor__ctor_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo_System_Text_Json_Serialization_Metadata_JsonTypeInfo\_}

从调用方的源生成元数据建立机器可读契约。

```csharp
public CapabilityDescriptor(string id, JsonTypeInfo request, JsonTypeInfo response)
```

#### Parameters

`id` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo)

`response` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo)

## Properties

### Id \{#Kun_Mediator_CapabilityDescriptor_Id}

稳定能力标识。

```csharp
public string Id { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RequestSchema \{#Kun_Mediator_CapabilityDescriptor_RequestSchema}

请求 Schema 的独立节点。

```csharp
public JsonNode RequestSchema { get; }
```

#### Property Value

[JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

### RequestType \{#Kun_Mediator_CapabilityDescriptor_RequestType}

请求 CLR 类型。

```csharp
public Type RequestType { get; }
```

#### Property Value

[Type](https://learn.microsoft.com/dotnet/api/system.type)

### ResponseSchema \{#Kun_Mediator_CapabilityDescriptor_ResponseSchema}

响应 Schema 的独立节点。

```csharp
public JsonNode ResponseSchema { get; }
```

#### Property Value

[JsonNode](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode)

### ResponseType \{#Kun_Mediator_CapabilityDescriptor_ResponseType}

响应 CLR 类型。

```csharp
public Type ResponseType { get; }
```

#### Property Value

[Type](https://learn.microsoft.com/dotnet/api/system.type)
