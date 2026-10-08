# Qian.Data

数据 Provider 的最小安全装配，完成 S1-KUN-001 的交叉验收；完整范围仍见 [S1-DATA-001](../../pm/tasks/S1-DATA-001.md)。

```csharp
services.AddQianData<AppDbContext>((sp, options) => options.UseNpgsql(connectionString));
```

`AppDbContext` 继承 Definition 包里的 `QianDbContext`，租户上下文由宿主注册。`AddQianData` 自动附加 RLS 连接版与事务版拦截器、租户模型缓存键。宿主 `StartAsync` 主动解析上下文并检查生效的缓存键，安全条件不满足时抛 `QianDataMisconfiguredException`，不会等到第一条业务查询才失败。

无需手写 `ReplaceService`；手工配置缺少该调用会在构造时被拒绝，后续回调把缓存键改回默认实现则在启动时被拒绝。启动检查只初始化 EF 模型，不执行迁移、不建立业务表、不发业务 SQL。

RLS 策略需由迁移显式创建，使用 Kun 的 `app.current_tenant_id`、`FORCE ROW LEVEL SECURITY` 与 `WITH CHECK`。数据库应用角色必须是 `NOSUPERUSER NOBYPASSRLS`。PgBouncer 事务池仍要求显式事务，自动接线不改变此条件。验收复用 `Qian.Testing.Containers.PostgresFixture` 的应用角色，分别用 EF 查询、原生 SQL、事务 GUC 验证隔离。

## 组件位置

当前目录含 Provider 代码、PublicAPI 清单与说明；Data seam 的契约放在 `Qian.Data.Abstractions`。完整组件目录、`ctx.Data`、profile 配置和 `qian add data` 由 S1-DATA-001 交付，计划随 SDK 进入全部 .NET profile。这里的 DI 方法只是宿主／插件装配细节。

## 模型体验

无独立 Agent 工具。启动失败使用 `DATA_MISCONFIGURED` 和具体缺失项诊断，不包含连接串与凭据。

## 已知限制与延期项

本任务不宣称数据组件完整可安装；审计字段、`xmin`、snake_case、Guid v7、迁移与分析器属于 S1-DATA-001／S1-KUN-005。此安全基础仅使用现有公开 Kun 2026.9.13 的 API；S1-KUN-001 上游后台链路修复已于 2026-10-08 合并 [Kun PR #43](https://github.com/idadawn/kun/pull/43)，仍需在 Kun 后续发版后通过 `KunVersion` 升级消费，本任务不触发 Kun 发版。
