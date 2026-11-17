> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IIntegrationEventHandler\<TEvent>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-202/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

Handles a strongly-typed integration event received from the broker.

```csharp
public interface IIntegrationEventHandler<in TEvent> where TEvent : class
```

#### Type Parameters

`TEvent`

The integration event type.

## Methods

### HandleAsync(TEvent, MessageContext, CancellationToken) \{#Kun_EventBus_IIntegrationEventHandler_1_HandleAsync__0_Kun_EventBus_MessageContext_System_Threading_CancellationToken\_}

Processes the `event`.

```csharp
ValueTask HandleAsync(TEvent @event, MessageContext context, CancellationToken cancellationToken = default)
```

#### Parameters

`event` TEvent

`context` [MessageContext](/pr-preview/pr-202/sdk/api/kun/kun-eventbus/messagecontext.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
