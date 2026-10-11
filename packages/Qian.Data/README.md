# Qian.Data

数据 Provider 的安全装配；S1-DATA-001 正在补齐完整范围，当前进度与剩余验收见 [任务](../../pm/tasks/S1-DATA-001.md)。

```csharp
services.AddQianData<AppDbContext>((sp, options) => options
    .UseNpgsql(connectionString).UseKunSchema("plugin_example"));
```

`AppDbContext` 继承 Definition 包里的 `QianDbContext`，租户上下文由宿主注册。装配复用当前 KunVersion 的 `AddKunTenantRls<TContext>()`、`AddKunDataConventions<TContext>()` 和租户字段拦截器，不再维护第二套 RLS 接线。宿主 `StartAsync` 主动解析上下文、检查模型缓存键并构建模型，安全条件不满足时抛 `QianDataMisconfiguredException`。

Qian 模型定制器在业务映射后强制应用租户/软删过滤、Outbox 模型和 Kun 的 snake_case、Guid v7、可空审计主体、`xmin` 约定，并保留业务过滤条件的交集。每个上下文必须用 `UseKunSchema` 指定同一个 `plugin_` 前缀 schema；租户列非空且默认具有索引。派生上下文漏调 `base.OnModelCreating` 不能移除这些安全约定。

无需手写 `ReplaceService`；手工配置缺少该调用会在构造时被拒绝，后续回调把缓存键改回默认实现则在启动时被拒绝。启动检查初始化 EF 模型并打开真实数据库连接，从 pg_roles 校验 session_user 与 current_user；预打开连接也检查。每次通过 EF 新开连接时同样检查，超级用户及 BYPASSRLS 角色被拒绝并关闭连接。启动不执行迁移、不建立业务表、不发业务 SQL。

RLS 策略需由迁移显式创建，使用 Kun 的 `app.current_tenant_id`、`FORCE ROW LEVEL SECURITY` 与 `WITH CHECK`。数据库应用角色必须是 `NOSUPERUSER NOBYPASSRLS`。PgBouncer 事务池仍要求显式事务，自动接线不改变此条件。验收复用 `Qian.Testing.Containers.PostgresFixture` 的应用角色，分别用 EF 查询、原生 SQL、事务 GUC 验证隔离。

## 组件位置

当前目录含 Provider 代码、PublicAPI 清单与说明；Data seam 的契约放在 `Qian.Data.Abstractions`。完整组件目录、`ctx.Data`、profile 配置和 `qian add data` 由 S1-DATA-001 交付，计划随 SDK 进入全部 .NET profile。这里的 DI 方法只是宿主／插件装配细节。

## 模型体验

无独立 Agent 工具。启动失败使用 `DATA_MISCONFIGURED` 和具体缺失项诊断，不包含连接串与凭据。

## 已知限制与延期项

当前已消费 Kun 2026.10.9。编译门禁拒绝在线上下文不继承安全基类，以及直接或委托调用 `IgnoreQueryFilters`。真库测试已验证原生 SQL 的租户隔离、自动主键/审计字段、`xmin` 冲突、软删和业务过滤条件保留。

自动 RLS 迁移、1000 行/租户的参考应用与索引验收、Data seam/安装表面、Outbox 同上下文工作单元封装仍在本任务范围内，尚未交付。RLS 验收角色与旧文档会话名的冲突见 [S1-DEC-017](../../pm/tasks/S1-DEC-017.md)，已由产品负责人批准并落实到文档及在线角色检查；完整 DATA-001 仍须完成其余验收。
