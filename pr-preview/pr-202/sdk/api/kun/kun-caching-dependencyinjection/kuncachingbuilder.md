> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunCachingBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-caching-dependencyinjection/index.md)\
Assembly: Kun.Caching.dll

Fluent builder returned from `AddKunCaching`.

```csharp
public sealed class KunCachingBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCachingBuilder](/pr-preview/pr-202/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

#### Extension Methods

[KunCachingServiceCollectionExtensions.UseMemory(KunCachingBuilder)](/pr-preview/pr-202/sdk/api/kun/kun-caching-dependencyinjection/kuncachingservicecollectionextensions.md#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseMemory_Kun_Caching_DependencyInjection_KunCachingBuilder_),
[KunCachingServiceCollectionExtensions.UseRedis(KunCachingBuilder, string)](/pr-preview/pr-202/sdk/api/kun/kun-caching-dependencyinjection/kuncachingservicecollectionextensions.md#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseRedis_Kun_Caching_DependencyInjection_KunCachingBuilder_System_String_)

## Constructors

### KunCachingBuilder(IServiceCollection) \{#Kun_Caching_DependencyInjection_KunCachingBuilder__ctor_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Creates a builder bound to `services`.

```csharp
public KunCachingBuilder(IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Properties

### Services \{#Kun_Caching_DependencyInjection_KunCachingBuilder_Services}

The service collection being configured.

```csharp
public IServiceCollection Services { get; }
```

#### Property Value

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
