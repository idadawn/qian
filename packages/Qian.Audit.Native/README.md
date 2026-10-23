# 原生 PostgreSQL 审计

`Qian.Audit.Native` 是 `qian.audit.native` 的 Provider，依赖 `Qian.Audit.Abstractions`、`Qian.Data.Native.Abstractions` 与 SDK，继续提供原 `IAuditService` / `Audit` 及 `CommandAudit`。唯一声明入口是 `NativeAuditComponent`，`NativeAuditPlugin` 保留程序化组合入口；没有 EF Provider 引用。目录表面为 `.qian-plugin/plugin.json`、代码派生的 `schemas/config.schema.json` 与本主文档链接。它属于 Audit seam 的原生存储与强制审计策略角色，供 DEC-030 的 gateway 原生组合安装；最终 profile 接线由 S1-GW-001 验收。正式 feed 的 `qian add` 安装门尚未完成，清单保留 `x-skeleton`。

## 独立宿主

宿主登记真实 NativeData 的两个具名绑定，再通过 `PluginLoader` 挂载 `NativeDataComponent`、`NativeAuditComponent` 和消费者，不需要 registry/lifecycle。绑定凭据由宿主 secretRef 解析器或连接工厂提供，不进入组件 JSON。以下装配在宿主工程中引用两个 Provider；Audit 包本身只引用 NativeData Definition。

```csharp
var roles = new NativeAuditRoles("audit_tenant_login", "audit_function_owner", "audit_platform_login");
var guard = NativeAuditStorage.ExpectedGuard(roles);
var tables = new[] {
    new NativeDataTable(NativeAuditStorage.Schema, "audit_entries", NativeAuditStorage.GuardPolicy, guard, guard),
    new NativeDataTable(NativeAuditStorage.Schema, "audit_chain_state", NativeAuditStorage.GuardPolicy, guard, guard)
};
services.AddQianNativeData(new("audit_tenant", roles.TenantLogin, NativeDataAccess.Tenant, tenantSecretFactory, tables));
services.AddQianNativeData(new("audit_platform", roles.PlatformLogin!, NativeDataAccess.Platform, platformSecretFactory, tables));
```

NativeData 配置为 `{"connections":["audit_tenant","audit_platform"]}`。Audit 配置为：

```json
{
  "tenantBinding": "audit_tenant",
  "platformBinding": "audit_platform",
  "tenantRole": "audit_tenant_login",
  "platformRole": "audit_platform_login",
  "functionOwner": "audit_function_owner",
  "maxAttempts": 6
}
```

global 挂载必须没有 ambient tenant，并显式提供独立平台绑定，以受限平台登录只读校验 catalog；缺少时挂载失败，不能猜凭据或虚构租户。tenant 挂载必须有真实 ambient tenant，且与 entry scope、当前 scope 一致；只绑定租户凭据也能独立运行。复用含平台角色的既有 Schema 时可以只声明 `platformRole` 而省略 `platformBinding`，此时平台 API 不开放。角色名只接受小写 ASCII 字母开头的字母、数字、下划线，最长 63 字符，三者不同。

## 迁移与权限

由部署工具预先创建专用角色，再用独立迁移连接调用 `NativeAuditStorage.MigrateAsync(connection, roles)`；它拥有 SERIALIZABLE 迁移事务及 advisory 锁。也可导出 `CreateMigrationSql(roles)`，由部署工具放进同等受控事务执行。在线组件只校验，不创建表、不改授权、不运行迁移。两种 LOGIN 都必须 NOINHERIT、非 superuser/BYPASSRLS、无建库建角复制权限、无任何角色成员关系；函数 owner 必须是独立 NOLOGIN/NOINHERIT，非表/schema owner、没有 schema CREATE。所有口令由部署环境提供，没有内置默认值。

沿用 `plugin_qian_audit.audit_entries` 和 `audit_chain_state` 的字段与 v1 哈希协议。新租户和平台 LOGIN 只有 SELECT 与 `native_tip` / `native_append` 的 EXECUTE；不能直接 INSERT/UPDATE/DELETE/TRUNCATE，也不能任意回拨链头。受限 SECURITY DEFINER 函数使用固定 `search_path=pg_catalog`、PUBLIC 无 EXECUTE，在真实专用 session login、可信租户 GUC、SERIALIZABLE 下原子追加记录并推进链头。函数 owner 只有记录 INSERT 和链头两个 tip 列的 UPDATE。平台登录只见 null 平台行，设置租户 GUC 后也不能读租户行；SET ROLE 或成员资格均不能冒充。

迁移在原 `qian_tenant_guard` 上保留旧角色分支，并为三个新身份拼接受限分支；额外 restrictive `qian_audit_native_guard` 与其 AND 生效。固定源码模板提供 `ExpectedGuard` 的 PostgreSQL 16 规范表达式，不能从当前数据库反读后当作期望值。在线逐字校验两表 FORCE RLS、权限、策略、函数体、owner、函数属性和 ACL；启动还校验字段与唯一索引。原 guard 的迁移标记保存原表达式及已安装表达式，重复迁移幂等，漂移或改换角色要求显式迁移。不会改动既有 EF 角色授权。

因此，只追加保证针对新 Native 角色；已有 EF 租户角色的 UPDATE/DELETE、旧平台链头权限仍保持原行为。S1-AUD-007 的其它治理范围没有被本任务关闭，管理员权限也不属于防篡改边界。验链能定位历史变更，不能宣称数据库管理员无法改写整条链。

## 事务与协议

唯一规范化、哈希及输入投影实现位于 Definition 包的 `AuditProtocol` / `AuditInputProjection`。旧公开 `Qian.Audit.AuditCanonicalizer` 继续转发，既有 API 与历史哈希字节保持一致；新旧 Provider 能共同读写、验链，包括平台链和全部委托、输入哈希、通道、追踪及 payload 字段。

独立追加先取得与旧 Provider 相同的会话 advisory 锁，再开始 SERIALIZABLE，防止等待锁时持有旧快照；同 ID、同内容重放返回原记录，不同内容报 CONFLICT。序列化冲突或死锁最多尝试 `maxAttempts` 次（默认 6，范围 1–10），每次新事务；唯一约束/损坏链头不能当作成功。读取每页 1–1000 条；验链使用 REPEATABLE READ 和 128 条分页。

`AppendEnlistedAsync` 接受 NativeData 事务包装以及真实 Npgsql/EF `GetDbTransaction()`。必须是同绑定端点、数据库、受限租户登录、真实当前租户和 SERIALIZABLE；它不提交、不回滚、不释放、不局部重试调用方事务。业务使用该受限 Native LOGIN 连接即可共享真实 EF 事务，默认 INHERIT 的旧测试应用登录不满足绑定条件。业务外层须重试整笔事务。平台链没有同事务入口：`AppendPlatformEnlistedAsync`（EF Provider 在 S1-AUD-009 新增的平台链同事务入口）在这里抛 `NotSupportedException`，平台记录仍走 `AppendPlatformAsync` 的独立事务；gateway 的原生组合不装 Lifecycle，当前没有调用方，需要时另立任务（同步改 `native_tip` / `native_append`、固定 guard 模板与在线逐字校验）。卸载先撤销策略/服务登记，再取消、排空在途调用，旧服务句柄失效。

## 验证

`tests/Qian.Audit.Native.Tests` 的真实 PostgreSQL 专项覆盖两个独立 IHost、160 条并发、跨 128 条分页验链、同 ID 冲突、精确历史篡改、有界 40001 重试、CRM EF 业务提交/回滚、旧 EF 模型/权限及双向读写、平台/租户/NOLOGIN 中间成员冒充、权限和函数漂移、表达式唯一索引伪装，以及卸载真实锁等待。2026-10-23 当前代码的存储专项 18/18、配置与角色正反例 14/14、旧 EF 存储回归 17/17 通过。共享协议历史 fixture 与旧 Provider 回归分别在 `tests/Qian.Audit.Tests`，SDK 的原生 owner 授权与 listener 真撤销在 `tests/Qian.Plugin.Sdk.Tests`。

严格 Linux 原生发布与真库探针见 [验收说明](../../tools/Qian.Audit.Native.Probe/README.md)。该门独立于普通解决方案测试；gateway 的完整 Config/Identity/Redis 组合仍由 S1-GW-001 验收。
