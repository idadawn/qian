> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IIntegrationEventBus

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EventBus)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-50/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

High-level façade for publishing integration events via the Outbox pattern.

```csharp
public interface IIntegrationEventBus
```

## Methods

### Publish\<TEvent>(TEvent) \{#Kun_EventBus_IIntegrationEventBus_Publish__1___0\_}

Stages `event` as an outbox message within the current unit-of-work.

```csharp
void Publish<TEvent>(TEvent @event) where TEvent : class
```

#### Parameters

`event` TEvent

#### Type Parameters

`TEvent`
