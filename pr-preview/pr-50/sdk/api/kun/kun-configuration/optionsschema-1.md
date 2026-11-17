> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class OptionsSchema\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Configuration)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration](/pr-preview/pr-50/sdk/api/kun/kun-configuration/index.md)\
Assembly: Kun.Configuration.dll

由源生成 JSON 元数据导出的配置契约，不包含任何运行时配置值。

```csharp
public sealed class OptionsSchema<T> where T : class
```

#### Type Parameters

`T`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OptionsSchema\<T>](/pr-preview/pr-50/sdk/api/kun/kun-configuration/optionsschema-1.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OptionsSchema(string, JsonTypeInfo\<T>) \{#Kun_Configuration_OptionsSchema_1__ctor_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo__0\_\_}

由源生成 JSON 元数据导出的配置契约，不包含任何运行时配置值。

```csharp
public OptionsSchema(string section, JsonTypeInfo<T> typeInfo)
```

#### Parameters

`section` [string](https://learn.microsoft.com/dotnet/api/system.string)

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<T>

## Properties

### Section \{#Kun_Configuration_OptionsSchema_1_Section}

该配置绑定的 section。

```csharp
public string Section { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### Export() \{#Kun_Configuration_OptionsSchema_1_Export}

每次返回独立的 JSON Schema 2020-12 节点，调用方修改不会污染缓存。

```csharp
public JsonObject Export()
```

#### Returns

[JsonObject](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject)
