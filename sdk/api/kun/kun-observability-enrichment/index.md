> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Observability.Enrichment

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Observability` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Observability`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Observability)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[KunBaggageKeys](/sdk/api/kun/kun-observability-enrichment/kunbaggagekeys.md)

Canonical baggage key names the Kun activity enricher mirrors
onto Kun-sourced span tags at `Activity.Start` time. See
`docs/architecture/ADR-013-Kun.Observability-enricher.md`
for the contract, including why only these keys are whitelisted
and how consumers should populate them.
