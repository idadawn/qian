# Qian.Data

Data seam 的 Provider，配合 Qian.Data.Abstractions 的安全上下文、服务键和会话契约。S1-DATA-001 正在补齐组件安装表面与参考应用验收，当前进度见 [任务](../../pm/tasks/S1-DATA-001.md)。

## 安全装配

```csharp
services.AddQianData<AppDbContext>((sp, options) => options
    .UseNpgsql(connectionString).UseKunSchema("plugin_example"));
```

业务上下文继承 QianDbContext。装配复用当前 KunVersion 的 AddKunTenantRls、AddKunDataConventions 与租户字段拦截器，强制模型缓存键、Qian 模型定制器、应用角色检查和迁移生成器。派生上下文漏调 base 仍保留租户/软删过滤，并与业务过滤条件取交集；采用 snake_case、Guid v7、可空审计主体和 xmin。每个上下文使用同一个 plugin_ 前缀 schema，租户列非空且默认具有索引。

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

事件类型必须由宿主显式通过 AddKunEventBus 登记，推荐使用源生成 JsonTypeInfo；Data 不暗中登记空目录。IOutboxWriter 与 IIntegrationEventBus 必须指向同一 scoped accumulator。领域通知走 Kun Mediator 的当前 scope，通知处理器与工作单元拿到同一业务上下文和缓冲；存在通知但未装配 Mediator、通知没有实现 INotification 时拒绝保存。

保存失败后会话不可复用，调用方必须释放后新开；业务数据与 Outbox 在同一次 EF 保存中提交或回滚。会话创建后不可切换租户，混入其他租户的事件会被拒绝。卸载 Provider 会撤销入口并释放存活会话，已缓存的数据服务不能再开新会话。本组件负责 Outbox 入库，后台投递由事件组件负责。

## 删除审计

QianSoftDeleteInterceptor 在 Kun 审计拦截器之前运行。AggregateRoot.Delete 和 DbContext.Remove 都落为软删除，填充 deleted_at / deleted_by；后者保留已有业务字段并让 Kun 同时更新 updated_at / updated_by。DeletedBy 与 CreatedBy 使用一致的可空用户键类型，没有 CLR 属性时用 shadow property，字段名由 QianDataConventions.DeletedByPropertyName 提供。匿名操作的删除主体为 null，已有删除审计不因普通更新而改写。

## 组件位置

本目录已有 DataPlugin 的实际入口和生命周期，契约放在 Qian.Data.Abstractions，业务组件只依赖 Definition。当前代码入口仅接受空配置，连接显式配置到各业务上下文；拒绝忽略连接参数或可关闭隔离的配置。`plugins/qian.data` 仍保留旧配置骨架，正式清单、各 profile 的配置接线和 `qian add data` 完整安装验收仍由本任务继续完成，当前不因此宣称可完整安装。

## 模型体验

无独立 Agent 工具。隔离装配失败使用 DATA_MISCONFIGURED，不输出连接串或凭据。Provider 的装配方法是宿主细节，业务代码通过 Data seam 使用数据能力。

## 已知限制与延期项

真实测试覆盖角色边界、自动迁移、空租户/跨租户写拒绝、数据约定、软删审计、同上下文 Outbox 原子性、不同上下文的缓冲隔离以及卸载释放。每租户 1000 行的参考应用与真实索引计划、组件安装表面尚未完成。S1-DEC-017 已批准并实施，完整 DATA-001 仍保持进行中。
