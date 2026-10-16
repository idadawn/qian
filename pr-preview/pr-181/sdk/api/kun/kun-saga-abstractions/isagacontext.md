> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISagaContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/index.md)\
Assembly: Kun.Saga.dll

Marker interface for saga context objects. Every saga context carries a unique
[ISagaContext.SagaId](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId) and an optional [ISagaContext.TenantId](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_TenantId) for multi-tenant scenarios.
Implementations should be serializable to JSON (used for durable state storage).

```csharp
public interface ISagaContext
```

## Properties

### SagaId \{#Kun_Saga_Abstractions_ISagaContext_SagaId}

Gets the unique identifier for this saga instance.

```csharp
Guid SagaId { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### TenantId \{#Kun_Saga_Abstractions_ISagaContext_TenantId}

Gets the tenant identifier; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for single-tenant deployments.

```csharp
string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
