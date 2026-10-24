> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Sandbox.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Sandbox)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[BrowserSessionConfig](/sdk/api/kun/kun-sandbox-abstractions/browsersessionconfig.md)

Browser configuration sub-object of [NeuroagentSandboxExtension](/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md).

[FileEntry](/sdk/api/kun/kun-sandbox-abstractions/fileentry.md)

Single entry returned by [IFiles.ListAsync](/sdk/api/kun/kun-sandbox-abstractions/ifiles.md#Kun_Sandbox_Abstractions_IFiles_ListAsync_System_String_System_Boolean_System_Threading_CancellationToken_). Wire shape
matches the E2B v2 `files/list` response.

[NeuroagentSandboxExtension](/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md)

Compatibility shape for the legacy explicit supervisor extension. New integrations choose their own provider extension contract.

[ProcessResult](/sdk/api/kun/kun-sandbox-abstractions/processresult.md)

Outcome of a completed [IProcesses.RunAsync](/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_)
call. Wire shape matches the E2B v2 `processes` response.

[ProcessRunOptions](/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)

Per-call options for [IProcesses.RunAsync](/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_).
Mirrors the E2B v2 `POST /v2/sandboxes/{id}/processes` request body.

[SandboxCreateOptions](/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

Sandbox create options use the portable E2B v2 fields plus explicitly named provider-owned extension data.

[SecretScopeConfig](/sdk/api/kun/kun-sandbox-abstractions/secretscopeconfig.md)

Secret-scope sub-object of [NeuroagentSandboxExtension](/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md).

### Interfaces

[IFiles](/sdk/api/kun/kun-sandbox-abstractions/ifiles.md)

File sub-API of an [ISandboxHandle](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md). Maps to the E2B v2
`GET/POST/DELETE /v2/sandboxes/{id}/files?path=...` endpoints and
`GET /v2/sandboxes/{id}/files/list?path=...`.

[IProcesses](/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md)

Process-execution sub-API of an [ISandboxHandle](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md). Maps
to the E2B v2 `POST /v2/sandboxes/{id}/processes` endpoint.

[ISandboxHandle](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)

Handle to a running sandbox. Exposes process and file sub-APIs that
map one-to-one to E2B v2's `processes` and `files`
endpoints, plus a [ISandboxHandle.KillAsync](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_KillAsync_System_Threading_CancellationToken_) operation that maps to
`DELETE /v2/sandboxes/{id}`.

[ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md)

Pluggable sandbox backend. Creates an [ISandboxHandle](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)
that represents a short-lived isolated execution environment.

### Enums

[SandboxState](/sdk/api/kun/kun-sandbox-abstractions/sandboxstate.md)

Lifecycle states exposed by [ISandboxHandle.State](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_State). Matches the E2B v2 shape.
