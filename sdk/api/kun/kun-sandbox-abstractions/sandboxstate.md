> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Enum SandboxState

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Lifecycle states exposed by [ISandboxHandle.State](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_State). Matches the E2B v2 shape.

```csharp
public enum SandboxState
```

## Fields

`Creating = 1`

Backend has accepted the create request; container is not yet ready.

`Failed = 4`

Backend reported a failure during the sandbox lifetime.

`Killed = 3`

Sandbox was torn down via [ISandboxHandle.KillAsync](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_KillAsync_System_Threading_CancellationToken_) or because its timeout elapsed.

`Running = 2`

Container is ready; processes can run.

`TimedOut = 5`

Sandbox was terminated because it exceeded its `timeoutMs`.

`Unknown = 0`

Default value; should not appear on a returned handle.
