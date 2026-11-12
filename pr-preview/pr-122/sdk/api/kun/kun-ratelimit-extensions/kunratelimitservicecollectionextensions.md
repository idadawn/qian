> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunRateLimitServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.RateLimit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.RateLimit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.RateLimit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.RateLimit.Extensions](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-extensions/index.md)\
Assembly: Kun.RateLimit.dll

Service-collection extensions that register unified Kun rate limiting.

```csharp
public static class KunRateLimitServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunRateLimitServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-extensions/kunratelimitservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunRateLimit(IServiceCollection, Action\<KunRateLimitOptions>) \{#Kun_RateLimit_Extensions_KunRateLimitServiceCollectionExtensions_AddKunRateLimit_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_RateLimit_Abstractions_KunRateLimitOptions\_\_}

Registers [`RateLimiterOptions`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.ratelimiting.ratelimiteroptions) built from the supplied
[KunRateLimitOptions](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md). Each named window becomes a
fixed-window partition policy buckets on the configured
[RateLimitPartitionKey](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/ratelimitpartitionkey.md). Rejected requests receive a
[`Status429TooManyRequests`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.statuscodes.status429toomanyrequests) response with a
`Retry-After` header; the optional
[KunRateLimitOptions.OnRejected](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md#Kun_RateLimit_Abstractions_KunRateLimitOptions_OnRejected) hook runs after the
default response is written.

```csharp
public static IServiceCollection AddKunRateLimit(this IServiceCollection services, Action<KunRateLimitOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunRateLimitOptions](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md)>

Options callback; must register at least one window.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.
