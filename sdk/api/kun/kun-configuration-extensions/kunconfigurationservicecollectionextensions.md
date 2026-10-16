> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunConfigurationServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Configuration)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Configuration.Extensions](/sdk/api/kun/kun-configuration-extensions/index.md)\
Assembly: Kun.Configuration.dll

Service-collection extensions that register Kun typed-options
validation.

```csharp
public static class KunConfigurationServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunConfigurationServiceCollectionExtensions](/sdk/api/kun/kun-configuration-extensions/kunconfigurationservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunOptions\<TOptions>(IServiceCollection, string) \{#Kun_Configuration_Extensions_KunConfigurationServiceCollectionExtensions_AddKunOptions__1_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_String\_}

Registers `TOptions` bound to
`section`, validates DataAnnotations
attributes at host start, and fails fast with
section-qualified messages.

```csharp
[RequiresUnreferencedCode("DataAnnotations-based options validation walks properties by reflection.")]
[RequiresDynamicCode("DataAnnotations-based options validation walks properties by reflection.")]
[UnconditionalSuppressMessage("ConfigurationBinder", "SYSLIB1100", Justification = "Consumer opts into reflection-based binding by calling this annotated entry point.")]
[UnconditionalSuppressMessage("ConfigurationBinder", "SYSLIB1101", Justification = "Consumer opts into reflection-based binding by calling this annotated entry point.")]
[UnconditionalSuppressMessage("ConfigurationBinder", "SYSLIB1104", Justification = "Consumer opts into reflection-based binding by calling this annotated entry point.")]
public static OptionsBuilder<TOptions> AddKunOptions<TOptions>(this IServiceCollection services, string section) where TOptions : class
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

Service collection to register into.

`section` [string](https://learn.microsoft.com/dotnet/api/system.string)

Configuration section path (e.g. `"Authentik"`, `"Database:Primary"`).

#### Returns

[OptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsbuilder-1)\<TOptions>

The underlying [`OptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsbuilder-1) for further chaining.

#### Type Parameters

`TOptions`

Options type; must be a reference type. Annotated with
[`All`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.dynamicallyaccessedmembertypes.all) so the
trimmer preserves the properties DataAnnotations walks.

### AddKunOptions\<TOptions>(IServiceCollection, string, bool) \{#Kun_Configuration_Extensions_KunConfigurationServiceCollectionExtensions_AddKunOptions__1_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_String_System_Boolean\_}

关键配置在启动时校验；可选配置在首次读取时校验，未使用时不阻断宿主启动。

```csharp
[RequiresUnreferencedCode("配置绑定与 DataAnnotations 校验需要反射。")]
[RequiresDynamicCode("配置绑定与 DataAnnotations 校验需要反射。")]
public static OptionsBuilder<TOptions> AddKunOptions<TOptions>(this IServiceCollection services, string section, bool critical) where TOptions : class
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`section` [string](https://learn.microsoft.com/dotnet/api/system.string)

`critical` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Returns

[OptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsbuilder-1)\<TOptions>

#### Type Parameters

`TOptions`

### AddKunOptions\<TOptions>(IServiceCollection, string, JsonTypeInfo\<TOptions>, bool) \{#Kun_Configuration_Extensions_KunConfigurationServiceCollectionExtensions_AddKunOptions__1_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0__System_Boolean\_}

绑定并校验配置，同时登记基于源生成 JSON 元数据的 Schema 2020-12。

```csharp
[RequiresUnreferencedCode("与既有 AddKunOptions 相同，绑定及 DataAnnotations 需要反射。Schema 导出本身使用源生成元数据。")]
[RequiresDynamicCode("与既有 AddKunOptions 相同，调用方显式选择反射绑定。")]
public static OptionsBuilder<TOptions> AddKunOptions<TOptions>(this IServiceCollection services, string section, JsonTypeInfo<TOptions> typeInfo, bool critical = true) where TOptions : class
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`section` [string](https://learn.microsoft.com/dotnet/api/system.string)

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TOptions>

`critical` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Returns

[OptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsbuilder-1)\<TOptions>

#### Type Parameters

`TOptions`
