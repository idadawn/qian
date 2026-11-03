> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IAggregateRoot\<TKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

带强类型 Key 的聚合根标记接口。

```csharp
public interface IAggregateRoot<out TKey> : IAggregateRoot where TKey : notnull
```

#### Type Parameters

`TKey`

#### Implements

[IAggregateRoot](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iaggregateroot.md)

## Properties

### Id \{#Kun_Core_Domain_IAggregateRoot_1_Id}

Aggregate root primary key.

```csharp
TKey Id { get; }
```

#### Property Value

TKey
