> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IIntegrationEvent

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Core)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

集成事件标记接口。集成事件用于跨服务、跨进程的异步通知，
与领域事件不同，集成事件通过 Outbox + 消息总线传递，具备版本演进能力。

```csharp
public interface IIntegrationEvent
```

## Properties

### EventId \{#Kun_Core_Domain_IIntegrationEvent_EventId}

消息唯一标识，用于消费端幂等去重。

```csharp
Guid EventId { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### OccurredAt \{#Kun_Core_Domain_IIntegrationEvent_OccurredAt}

事件发生的 UTC 时间戳。

```csharp
DateTime OccurredAt { get; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)
