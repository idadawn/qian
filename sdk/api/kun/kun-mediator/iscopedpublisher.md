> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IScopedPublisher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Mediator)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

由事务宿主显式指定作用域，保证通知处理器复用同一 DbContext 与 Outbox 缓冲。

```csharp
public interface IScopedPublisher
```

## Methods

### PublishInScope\<TNotification>(TNotification, IServiceProvider, CancellationToken) \{#Kun_Mediator_IScopedPublisher_PublishInScope__1___0_System_IServiceProvider_System_Threading_CancellationToken\_}

在调用方提供的作用域内顺序派发通知；不创建或释放该作用域。

```csharp
ValueTask PublishInScope<TNotification>(TNotification notification, IServiceProvider services, CancellationToken cancellationToken = default) where TNotification : INotification
```

#### Parameters

`notification` TNotification

`services` [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Type Parameters

`TNotification`
