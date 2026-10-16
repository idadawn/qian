> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.Observability.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Observability` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Observability`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Observability)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Classes

[KunObservabilityOptions](/pr-preview/pr-181/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md)

Configuration for `AddKunObservability`. Every option has a safe
default; the zero-arg call yields a trace + metric + log pipeline with
ASP.NET Core + HttpClient + EF Core instrumentation enabled and OTLP
export pointed at `OTEL_EXPORTER_OTLP_ENDPOINT`.
