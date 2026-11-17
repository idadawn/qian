> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface ITracedCurrentUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Security](/pr-preview/pr-202/sdk/api/kun/kun-core-security/index.md)\
Assembly: Kun.Core.dll

Extension of [ICurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-core-security/icurrentuser.md) that exposes the ambient OpenTelemetry trace id.
Cast or inject this sub-interface when trace-continuity access is required.

```csharp
public interface ITracedCurrentUser : ICurrentUser
```

#### Implements

[ICurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-core-security/icurrentuser.md)

## Properties

### TraceId \{#Kun_Core_Security_ITracedCurrentUser_TraceId}

The OpenTelemetry trace id propagated via the `trace_id` claim on the S2S JWT,
or `null` when the token was issued without trace context (e.g. background refresh).

```csharp
string? TraceId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
