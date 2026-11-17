> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunOutboxPublisherExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EventBus)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.DependencyInjection](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-dependencyinjection/index.md)\
Assembly: Kun.EventBus.dll

Extension methods to register the outbox publisher background service.

```csharp
public static class KunOutboxPublisherExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunOutboxPublisherExtensions](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-dependencyinjection/kunoutboxpublisherextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunOutboxPublisher(IServiceCollection, Action\<OutboxPublisherOptions>?) \{#Kun_EventBus_DependencyInjection_KunOutboxPublisherExtensions_AddKunOutboxPublisher_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_EventBus_Background_OutboxPublisherOptions\_\_}

Registers [OutboxPublisherBackgroundService](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/outboxpublisherbackgroundservice.md) and its dependencies.
默认 EF 存储使用事务行锁；自定义存储须通过 [KunOutboxPublisherExtensions.UseRedisOutboxLock](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-dependencyinjection/kunoutboxpublisherextensions.md#Kun_EventBus_DependencyInjection_KunOutboxPublisherExtensions_UseRedisOutboxLock_Microsoft_Extensions_DependencyInjection_IServiceCollection_)
配置共享锁。无锁模式只允许宿主显式注册 [NullOutboxLockProvider](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md)。

```csharp
public static IServiceCollection AddKunOutboxPublisher(this IServiceCollection services, Action<OutboxPublisherOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[OutboxPublisherOptions](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/outboxpublisheroptions.md)>?

Optional delegate to configure [OutboxPublisherOptions](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/outboxpublisheroptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.

### UseRedisOutboxLock(IServiceCollection) \{#Kun_EventBus_DependencyInjection_KunOutboxPublisherExtensions_UseRedisOutboxLock_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Replaces the default [NullOutboxLockProvider](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md) with
[RedisOutboxLockProvider](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/redisoutboxlockprovider.md) for distributed-lock semantics across multiple pods.
Requires `IConnectionMultiplexer` to be registered in the container.

```csharp
public static IServiceCollection UseRedisOutboxLock(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.
