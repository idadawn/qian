> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Auditing

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Auditing)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Auditing.DependencyInjection](/sdk/api/kun/kun-auditing-dependencyinjection/index.md)

[Kun.Auditing.Events](/sdk/api/kun/kun-auditing-events/index.md)

[Kun.Auditing.Middleware](/sdk/api/kun/kun-auditing-middleware/index.md)

### Classes

[AuditContext](/sdk/api/kun/kun-auditing/auditcontext.md)

Request-scoped DTO that carries ambient audit context from the outer host
([AuditContextMiddleware](/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md)) into the EF Core
[AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md).

[AuditableAttribute](/sdk/api/kun/kun-auditing/auditableattribute.md)

Documentation-only marker for entity types that should emit
[AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) rows on every
`SaveChangesAsync`.

[AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md)

EF Core [`ISaveChangesInterceptor`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.isavechangesinterceptor) that emits one
[AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) per tracked change of an entity type registered
via `services.AddAuditable<TEntity>()`. The event is forwarded to
[IAuditSink](/sdk/api/kun/kun-auditing/iauditsink.md) (default: outbox writer) so it commits atomically with the
ambient `SaveChangesAsync` transaction.

[TransactionalAuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/transactionalauditingsavechangesinterceptor.md)

在业务上下文中同时暂存 AuditLog 与 Outbox，任何一方失败均使整个保存失败。

### Interfaces

[IAuditSink](/sdk/api/kun/kun-auditing/iauditsink.md)

Terminal destination for [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) records produced by
[AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md). The default implementation forwards to
[IOutboxWriter](/sdk/api/kun/kun-eventbus-outbox/ioutboxwriter.md) so audit events flow atomically with
the current `SaveChangesAsync` transaction.

[IAuditableEntityRegistry](/sdk/api/kun/kun-auditing/iauditableentityregistry.md)

Immutable, DI-populated lookup of entity CLR types that participate in audit logging.
Populated at [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
build time by calls to `services.AddAuditable<TEntity>()`.
