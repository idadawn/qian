> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IPublisher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/pr-preview/pr-181/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

Publishes a notification to all registered handlers.

```csharp
public interface IPublisher
```

## Methods

### Publish\<TNotification>(TNotification, CancellationToken) \{#Kun_Mediator_IPublisher_Publish__1___0_System_Threading_CancellationToken\_}

Publish `notification` to all registered handlers sequentially.

```csharp
ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
```

#### Parameters

`notification` TNotification

The notification to publish.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Type Parameters

`TNotification`

The notification type.
