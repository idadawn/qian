> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Sandbox.Implementations

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[E2BCloudSandboxProvider](/sdk/api/kun/kun-sandbox-implementations/e2bcloudsandboxprovider.md)

Placeholder [ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) targeting E2B Cloud
(`https://api.e2b.dev`). The constructor signature is fixed so
DI wiring can be exercised in tests and reviewed in PRs, but every
method throws [`NotImplementedException`](https://learn.microsoft.com/dotnet/api/system.notimplementedexception).

[InMemorySandboxProvider](/sdk/api/kun/kun-sandbox-implementations/inmemorysandboxprovider.md)

In-process fake [ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) for unit tests and
local dev loops. Does not run anything: `processes.run` returns
a scripted [ProcessResult](/sdk/api/kun/kun-sandbox-abstractions/processresult.md) and file APIs operate on a
[`ConcurrentDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2). Clearly labelled
"test only" so no-one accidentally wires it into production.

[SupervisorSandboxProvider](/sdk/api/kun/kun-sandbox-implementations/supervisorsandboxprovider.md)

Production [ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) that talks to the
configured supervisor's `/v2/sandboxes/*` endpoints.
The HTTP client is supplied by
the caller — typically via [`IHttpClientFactory`](https://learn.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory)
— so delegating handlers can attach S2S JWT, mTLS client cert, and
OpenTelemetry instrumentation without this class knowing about them.
