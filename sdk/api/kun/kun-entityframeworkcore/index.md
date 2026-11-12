> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EntityFrameworkCore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.EntityFrameworkCore.Behaviors](/sdk/api/kun/kun-entityframeworkcore-behaviors/index.md)

[Kun.EntityFrameworkCore.Conventions](/sdk/api/kun/kun-entityframeworkcore-conventions/index.md)

[Kun.EntityFrameworkCore.DependencyInjection](/sdk/api/kun/kun-entityframeworkcore-dependencyinjection/index.md)

[Kun.EntityFrameworkCore.Extensions](/sdk/api/kun/kun-entityframeworkcore-extensions/index.md)

[Kun.EntityFrameworkCore.Interceptors](/sdk/api/kun/kun-entityframeworkcore-interceptors/index.md)

[Kun.EntityFrameworkCore.MultiTenancy](/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)

### Classes

[EfOutboxMessageStore](/sdk/api/kun/kun-entityframeworkcore/efoutboxmessagestore.md)

默认 PostgreSQL Outbox 存储：用 SKIP LOCKED 在同一事务中领取、发布并更新批次。

[EfRepository\<T>](/sdk/api/kun/kun-entityframeworkcore/efrepository-1.md)

Generic EF Core implementation of [IRepository\<T>](/sdk/api/kun/kun-infrastructure-abstractions/irepository-1.md) for aggregate roots.
Persistence is committed by the Unit of Work — callers must not call
`SaveChangesAsync` directly.

[EfUnitOfWork](/sdk/api/kun/kun-entityframeworkcore/efunitofwork.md)

EF Core-backed [IUnitOfWork](/sdk/api/kun/kun-entityframeworkcore/iunitofwork.md) that atomically persists aggregate changes,
dispatches domain events in-process, and writes integration events to the Outbox.

[InProcessDomainEventDispatcher](/sdk/api/kun/kun-entityframeworkcore/inprocessdomaineventdispatcher.md)

Dispatches domain events to in-process Mediator notification handlers.
Domain events that also implement [INotification](/sdk/api/kun/kun-mediator/inotification.md) are routed to all
registered [INotificationHandler\<TNotification>](/sdk/api/kun/kun-mediator/inotificationhandler-1.md)s via [IPublisher](/sdk/api/kun/kun-mediator/ipublisher.md).
Events that do not implement [INotification](/sdk/api/kun/kun-mediator/inotification.md) are silently skipped.

[IntegrationEventOutboxAccumulator](/sdk/api/kun/kun-entityframeworkcore/integrationeventoutboxaccumulator.md)

[IIntegrationEventBus](/sdk/api/kun/kun-eventbus/iintegrationeventbus.md) implementation that serializes integration events and
appends them to the [IntegrationEventBuffer](/sdk/api/kun/kun-eventbus-outbox/integrationeventbuffer.md) with current trace and tenant context.
The buffer is flushed atomically by [EfUnitOfWork](/sdk/api/kun/kun-entityframeworkcore/efunitofwork.md) during `SaveChangesAsync`.

[KunDbContext](/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

Base EF Core DbContext for Kun Framework services.

[NoOpDomainEventDispatcher](/sdk/api/kun/kun-entityframeworkcore/noopdomaineventdispatcher.md)

Default no-op domain event dispatcher. Users replace this with a real implementation
(e.g. one backed by `Kun.Mediator.IPublisher`) by registering their own
[IDomainEventDispatcher](/sdk/api/kun/kun-core-domain/idomaineventdispatcher.md) before calling `AddKunEntityFrameworkCore`.

[OutboxMessageConfiguration](/sdk/api/kun/kun-entityframeworkcore/outboxmessageconfiguration.md)

EF Core mapping for [OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md).

[SpecificationEvaluator](/sdk/api/kun/kun-entityframeworkcore/specificationevaluator.md)

Applies an [ISpecification\<T>](/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md) to an [`IQueryable`](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1),
translating criteria, ordering, includes, and paging into EF Core LINQ operators.

### Interfaces

[IUnitOfWork](/sdk/api/kun/kun-entityframeworkcore/iunitofwork.md)

Atomic unit-of-work boundary for command handlers.
