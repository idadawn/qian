> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Infrastructure.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Infrastructure)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[Specification\<T>](/sdk/api/kun/kun-infrastructure-abstractions/specification-1.md)

规约模式默认实现。业务方通过继承并在构造器中调用 `AddCriteria` / `ApplyOrderBy` /
`ApplyPaging` 等保护方法来定义查询语义。

### Interfaces

[IProcessedMessageStore](/sdk/api/kun/kun-infrastructure-abstractions/iprocessedmessagestore.md)

消费端已处理消息记录存储。用于保证 Outbox 至少一次投递场景下的消费幂等。

默认实现基于 Redis 的 `SETNX + EX`（单命令原子操作）。
业务可替换为基于数据库唯一索引的实现以获得跨故障域的强一致性。

[IRepository\<T>](/sdk/api/kun/kun-infrastructure-abstractions/irepository-1.md)

聚合根仓储抽象。业务层依赖本接口，不依赖具体 ORM。

[ISpecification\<T>](/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md)

规约模式（Specification Pattern）抽象。
把查询条件、排序、分页从仓储中剥离出来，以可组合的对象表达业务意图。

[IUnitOfWork](/sdk/api/kun/kun-infrastructure-abstractions/iunitofwork.md)

工作单元抽象。通常每个请求作用域一个实例，[IUnitOfWork.SaveChangesAsync](/sdk/api/kun/kun-infrastructure-abstractions/iunitofwork.md#Kun_Infrastructure_Abstractions_IUnitOfWork_SaveChangesAsync_System_Threading_CancellationToken_) 由 Pipeline Behavior 统一触发。
