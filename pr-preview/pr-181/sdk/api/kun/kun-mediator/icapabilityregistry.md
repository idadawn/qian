> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ICapabilityRegistry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/pr-preview/pr-181/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

只读能力目录；调用权限与 scope 过滤由上层服务完成。

```csharp
public interface ICapabilityRegistry
```

## Methods

### Find(string) \{#Kun_Mediator_ICapabilityRegistry_Find_System_String\_}

按稳定 ID 查找能力。

```csharp
CapabilityDescriptor? Find(string id)
```

#### Parameters

`id` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[CapabilityDescriptor](/pr-preview/pr-181/sdk/api/kun/kun-mediator/capabilitydescriptor.md)?

### GetAll() \{#Kun_Mediator_ICapabilityRegistry_GetAll}

以 ID 顺序枚举全部声明。

```csharp
IReadOnlyList<CapabilityDescriptor> GetAll()
```

#### Returns

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[CapabilityDescriptor](/pr-preview/pr-181/sdk/api/kun/kun-mediator/capabilitydescriptor.md)>
