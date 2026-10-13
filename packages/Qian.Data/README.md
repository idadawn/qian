# Qian.Data

Data seam 的 Provider，配合 Qian.Data.Abstractions 的安全上下文、服务键和会话契约。参考应用数据验收已补齐，SDK / CLI 的完整代码安装仍有后续门禁，当前进度见 [任务](../../pm/tasks/S1-DATA-001.md)。

## 跨宿主租户模型

租户查询过滤器必须从当前执行查询的 DbContext 读取租户。相同上下文类型和租户在不同宿主中复用 EF 模型时，不能保留第一次建模时的访问器实例；旧访问器退出或切换租户不应改变新请求的条件。`CrossHostTenantModelTests` 以真实受限 PostgreSQL 角色验证模型复用，并让第二宿主编译不同形状的查询，覆盖旧查询缓存可能掩盖的问题。Qian 通过唯一 `KunVersion` 消费此框架修复，仍保留 RLS 双拦截器与模型缓存门禁。

## 安全装配

```csharp
services.AddQianData<AppDbContext>((sp, options) => options
    .UseNpgsql(connectionString).UseKunSchema("plugin_example"));
```

业务上下文继承 QianDbContext。独立宿主可直接注入 IDataService，无需挂载平台插件。装配复用当前 KunVersion 的 AddKunTenantRls、AddKunDataConventions 与租户字段拦截器，强制模型缓存键、Qian 模型定制器、应用角色检查和迁移生成器。派生上下文漏调 base 仍保留租户/软删过滤，并与业务过滤条件取交集；采用 snake_case、Guid v7、可空审计主体和 xmin。每个上下文使用同一个 plugin_ 前缀 schema，租户列非空且默认具有索引。

启动会构建模型并打开真实连接，只读取角色属性，不建表或迁移。登录角色及当前角色必须均为 NOSUPERUSER / NOBYPASSRLS，预打开连接也检查；同步/异步新开连接遇高权限角色立即拒绝并关闭。

迁移生成器为目标模型中的每个新建 IMultiTenant 表紧接着输出 ENABLE / FORCE RLS 与 USING / WITH CHECK。租户限制采用 AS RESTRICTIVE，附加宽松策略不能通过 OR 放宽边界；`NULLIF(current_setting('app.current_tenant_id', true), '')` 使缺失和空租户不匹配。丢失目标模型或标准租户列标记时拒绝生成裸表。历史表转换仍须显式安全迁移。PgBouncer 事务池仍要求显式事务。

## 数据服务与工作单元

DataPlugin 在 Apply 中提供 Data 键；消费者通过 `ctx.Data()` 解析。每次 `OpenAsync<TContext>()` 建立独立 DI scope，选择已注册的业务上下文，把该上下文、Kun IntegrationEventBuffer 和 EfUnitOfWork 绑定在一起。不会把全局 KunDbContext 指向最后登记的业务上下文。

```csharp
await using var session = await ctx.Data().OpenAsync<AppDbContext>(ct);
session.Context.Add(entity);
session.Outbox.Enqueue(integrationEvent);
await session.SaveChangesAsync(ct);
```

事件类型必须由宿主显式通过 AddKunEventBus 登记，推荐使用源生成 JsonTypeInfo；Data 不暗中登记空目录。IOutboxWriter 与 IIntegrationEventBus 必须指向同一个 scoped 实例：默认是 Kun 累加器；装了事件组件时换成它的信封写入端，该写入端经 IOutboxBufferBinding 声明自己写入当前 scope 的缓冲器，否则拒绝开会话。领域通知走 Kun Mediator 的当前 scope，通知处理器与工作单元拿到同一业务上下文和缓冲；存在通知但未装配 Mediator、通知没有实现 INotification 时拒绝保存。

保存失败后会话不可复用，调用方必须释放后新开；业务数据与 Outbox 在同一次 EF 保存中提交或回滚。会话创建后不可切换租户，混入其他租户的事件会被拒绝。卸载 Provider 会撤销入口并释放存活会话，已缓存的数据服务不能再开新会话。本组件负责 Outbox 入库，后台投递由 [Qian.Events](../Qian.Events/README.md) 负责；AddQianData 同时登记 IDataContextCatalog，投递服务据此逐个处理各业务上下文的 Outbox 表。

## 删除审计

QianSoftDeleteInterceptor 在 Kun 审计拦截器之前运行。AggregateRoot.Delete 和 DbContext.Remove 都落为软删除，填充 deleted_at / deleted_by；后者保留已有业务字段并让 Kun 同时更新 updated_at / updated_by。DeletedBy 与 CreatedBy 使用一致的可空用户键类型，没有 CLR 属性时用 shadow property，字段名由 QianDataConventions.DeletedByPropertyName 提供。匿名操作的删除主体为 null，已有删除审计不因普通更新而改写。

## 组件位置

本目录已有 DataPlugin 的实际入口和生命周期，契约放在 Qian.Data.Abstractions，业务组件只依赖 Definition。组件接受 dialect=pg、rls.enforce=true 和可选 connection 引用。带连接引用的 AddQianData 重载将上下文绑定到 platform/dev secretRef，凭据由宿主工厂在每次创建上下文时读取；组件的 connection 选择对应的上下文集合，未登记的引用或上下文会被拒绝。省略 connection 时沿用各上下文自身的配置，方便按组件使用独立 schema 和受限角色。租户挂载的 Data 只接受相同租户上下文，租户自带业务库仍在 S3 范围。

按已批准的 [S1-DEC-018](../../pm/tasks/S1-DEC-018.md)，数据本体先交付以解除注册表的实现前置。`plugins/qian.data` 仍保留配置骨架标记；SDK 代码字段源生成、默认装配与私有 NuGet 锁步发布由 S1-RT-014 回验，真实 `qian add data` 代码安装与 Provider 运行由 S1-RT-011 回验。这些门禁通过前，不宣称完整组件安装或正式私有发布已经完成。

## 模型体验

无独立 Agent 工具。隔离装配失败使用 DATA_MISCONFIGURED，不输出连接串或凭据。Provider 的装配方法是宿主细节，业务代码通过 Data seam 使用数据能力。

## 已知限制与延期项

真实测试覆盖角色边界、自动迁移、空租户/跨租户写拒绝、数据约定、软删审计、同上下文 Outbox 原子性、不同上下文的缓冲隔离以及卸载释放。samples/Qian.Sample.Crm 已通过真实签名身份、HTTP 与 PostgreSQL 的千行双租户及原 SQL 索引验收。SDK 源生成、私有发布与代码安装尚待后续任务验收；S1-DEC-017 / S1-DEC-018 均已批准并落实到对应验收边界。
