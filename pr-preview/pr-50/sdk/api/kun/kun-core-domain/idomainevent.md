> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IDomainEvent

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

标记领域事件。领域事件由聚合根内部产生，表示业务上发生的重要事实。
领域事件仅在聚合根边界内同步分发；若需跨服务传递，请在事件处理器中
转换为 [IIntegrationEvent](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iintegrationevent.md)。

```csharp
public interface IDomainEvent
```

## Properties

### OccurredAt \{#Kun_Core_Domain_IDomainEvent_OccurredAt}

事件发生的 UTC 时间戳。

```csharp
DateTime OccurredAt { get; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)
