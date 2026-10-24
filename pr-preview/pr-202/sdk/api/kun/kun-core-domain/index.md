> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Core.Domain

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[AggregateRoot\<TKey, TUserKey>](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/aggregateroot-2.md)

DDD 聚合根基类。

聚合根是领域一致性边界的持有者，所有外部修改必须通过聚合根的公开方法进行。
持久化细节（ORM 属性、表映射）一律不在此类出现——它们属于基础设施层的职责。

[AggregateRoot\<TKey>](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/aggregateroot-1.md)

便捷重载：审计用户 ID 默认为 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)。

[DomainException](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/domainexception.md)

Base exception for systemic domain-level failures that are not expressible via `Result<T>`.

Kun handler code should prefer [Result\<T>](/pr-preview/pr-202/sdk/api/kun/kun-core-results/result-1.md) for business failures.
`DomainException` is reserved for exceptional states the framework itself surfaces
(concurrency conflicts, repository contract violations, infrastructure-level faults).

[Entity\<TKey>](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/entity-1.md)

DDD 实体基类。实体的相等性由 [Entity\<TKey>.Id](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id) 决定，而非属性集合（这是实体与值对象的核心区别）。

[ValueObject](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/valueobject.md)

DDD 值对象基类。值对象没有独立标识，相等性由其所有组成属性决定。
值对象应当是不可变的——修改应返回新实例，而非改变现有实例。

### Interfaces

[IAggregateRoot\<TKey>](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iaggregateroot-1.md)

带强类型 Key 的聚合根标记接口。

[IAggregateRoot](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iaggregateroot.md)

聚合根标记接口（非泛型）。用于框架内部通过 `ChangeTracker` 按类型筛选。

[IAuditedObject\<TUserKey>](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iauditedobject-1.md)

带用户标识的审计接口。
`TUserKey` 可以是 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)、[string](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)、[long](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/integral-numeric-types) 等，
以兼容本地用户、外部 IdP 的 `sub`、数据库自增等不同场景。

[IAuditedObject](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iauditedobject.md)

通用审计接口，记录创建/修改时间。

[IDomainEvent](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/idomainevent.md)

标记领域事件。领域事件由聚合根内部产生，表示业务上发生的重要事实。
领域事件仅在聚合根边界内同步分发；若需跨服务传递，请在事件处理器中
转换为 [IIntegrationEvent](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iintegrationevent.md)。

[IDomainEventDispatcher](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/idomaineventdispatcher.md)

Dispatches domain events to their in-process handlers.

Called by the Unit of Work immediately before persistence commits, so handlers run inside
the same EF Core transaction that produced the aggregate changes. For cross-service events,
handlers translate the domain event into an [IIntegrationEvent](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iintegrationevent.md) and enqueue it
via the Outbox.

[IIntegrationEvent](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iintegrationevent.md)

集成事件标记接口。集成事件用于跨服务、跨进程的异步通知，
与领域事件不同，集成事件通过 Outbox + 消息总线传递，具备版本演进能力。

[IMultiTenant](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/imultitenant.md)

标记实体支持多租户隔离。由 `KunDbContext` 自动附加 `TenantId` 全局过滤器。

[ISoftDelete](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/isoftdelete.md)

标记实体支持软删除。由基础设施层在持久化时挂载全局查询过滤器。
