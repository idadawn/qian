> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Audit.DependencyInjection

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[KunAuditBuilder](/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md)

Fluent builder returned by `AddKunAudit`. Extension methods on
this type register concrete `IAuditWriter` backends without
forcing consumers to call multiple top-level `Add*` methods.

[KunAuditOptions](/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md)

Configuration for `AddKunAudit`.

[KunAuditServiceCollectionExtensions](/sdk/api/kun/kun-audit-dependencyinjection/kunauditservicecollectionextensions.md)

Service-collection extensions for Kun.Audit. Wires up both halves of the module:
the writer/reader abstraction ([IAuditWriter](/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) / [IAuditReader](/sdk/api/kun/kun-audit-abstractions/iauditreader.md))
and the tamper-evident hash-chain primitives ([IAuditChainState](/sdk/api/kun/kun-audit/iauditchainstate.md) /
[IAuditSink](/sdk/api/kun/kun-audit/iauditsink.md) / [AuditLogPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)).
