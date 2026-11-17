> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IAggregateRoot

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

聚合根标记接口（非泛型）。用于框架内部通过 `ChangeTracker` 按类型筛选。

```csharp
public interface IAggregateRoot
```

## Properties

### DomainEvents \{#Kun_Core_Domain_IAggregateRoot_DomainEvents}

聚合根产生的领域事件集合。

```csharp
IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
```

#### Property Value

[IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[IDomainEvent](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/idomainevent.md)>

## Methods

### ClearDomainEvents() \{#Kun_Core_Domain_IAggregateRoot_ClearDomainEvents}

清空已发布的领域事件（由 UoW 在持久化后调用）。

```csharp
void ClearDomainEvents()
```
