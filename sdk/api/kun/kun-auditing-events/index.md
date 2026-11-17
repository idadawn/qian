> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Auditing.Events

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Auditing)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)

Integration event emitted by [AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md) for every
tracked mutation of an entity registered via `services.AddAuditable<TEntity>()`.
Published through the Outbox with stable id `kun.audit.event.v1`.

[AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)

规范审计事件 v2：使用字符串租户与主体，保留 v1 类型供迁移。
