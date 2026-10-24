> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunCachingServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Caching)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.DependencyInjection](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/index.md)\
Assembly: Kun.Caching.dll

Service-collection extensions that register `Kun.Caching` backends.

```csharp
public static class KunCachingServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCachingServiceCollectionExtensions](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunCaching(IServiceCollection, Action\<KunCachingOptions>?) \{#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_AddKunCaching_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Caching_Configuration_KunCachingOptions\_\_}

Registers the cache abstractions and the default JSON serializer.
Pick a backend via [KunCachingServiceCollectionExtensions.UseMemory](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingservicecollectionextensions.md#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseMemory_Kun_Caching_DependencyInjection_KunCachingBuilder_) or
[KunCachingServiceCollectionExtensions.UseRedis](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingservicecollectionextensions.md#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseRedis_Kun_Caching_DependencyInjection_KunCachingBuilder_System_String_) on the returned builder.

```csharp
public static KunCachingBuilder AddKunCaching(this IServiceCollection services, Action<KunCachingOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunCachingOptions](/pr-preview/pr-50/sdk/api/kun/kun-caching-configuration/kuncachingoptions.md)>?

#### Returns

[KunCachingBuilder](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)

### UseMemory(KunCachingBuilder) \{#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseMemory_Kun_Caching_DependencyInjection_KunCachingBuilder\_}

Registers [MemoryKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-implementations/memorykuncache.md) as the active
[IKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-abstractions/ikuncache.md). Adds the required
`IMemoryCache` service if the caller has not registered
one already.

```csharp
public static KunCachingBuilder UseMemory(this KunCachingBuilder builder)
```

#### Parameters

`builder` [KunCachingBuilder](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)

#### Returns

[KunCachingBuilder](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)

### UseRedis(KunCachingBuilder, string) \{#Kun_Caching_DependencyInjection_KunCachingServiceCollectionExtensions_UseRedis_Kun_Caching_DependencyInjection_KunCachingBuilder_System_String\_}

Registers [RedisKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-implementations/rediskuncache.md) as the active
[IKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-abstractions/ikuncache.md), wired to a StackExchange Redis
distributed cache built from `connectionString`.
When `KunCachingOptions.FallbackToMemoryOnRedisFailure` is
[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), an in-process [MemoryKunCache](/pr-preview/pr-50/sdk/api/kun/kun-caching-implementations/memorykuncache.md)
is registered as the resilience sidecar.

```csharp
public static KunCachingBuilder UseRedis(this KunCachingBuilder builder, string connectionString)
```

#### Parameters

`builder` [KunCachingBuilder](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)

`connectionString` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[KunCachingBuilder](/pr-preview/pr-50/sdk/api/kun/kun-caching-dependencyinjection/kuncachingbuilder.md)
