> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISaga\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/index.md)\
Assembly: Kun.Saga.dll

Defines an orchestrated saga — an ordered sequence of compensatable [ISagaStep\<TContext>](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagastep-1.md)s.
The orchestrator executes steps in forward order and compensates in reverse order on failure.

```csharp
public interface ISaga<TContext> where TContext : ISagaContext
```

#### Type Parameters

`TContext`

The saga context type shared across all steps.

## Properties

### DefaultStepTimeout \{#Kun_Saga_Abstractions_ISaga_1_DefaultStepTimeout}

Gets the default timeout applied to each step that does not override
[ISagaStep\<TContext>.Timeout](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_Timeout).
The timeout detector will trigger compensation if a step exceeds this duration.

```csharp
TimeSpan DefaultStepTimeout { get; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Name \{#Kun_Saga_Abstractions_ISaga_1_Name}

Gets the stable, unique name for this saga type.
Used as the discriminator key in `saga_instances.saga_name`.
Keep it independent of CLR type names, e.g. `order.create.v1`. Do not change it while persisted instances still refer to it.

```csharp
string Name { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Steps \{#Kun_Saga_Abstractions_ISaga_1_Steps}

Gets the ordered list of steps to execute; compensated in reverse on failure.

```csharp
IReadOnlyList<ISagaStep<TContext>> Steps { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ISagaStep](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagastep-1.md)\<TContext>>
