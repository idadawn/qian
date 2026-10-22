# NativeData PostgreSQL Provider

`Qian.Data.Native.Postgres` 实现 `qian.data.native`，提供独立 `NativeData` 服务键，消费 [NativeData Definition](../Qian.Data.Native.Abstractions/README.md)。它不实现 EF 的 `IDataService`，不更改平台、调度器或既有 EF 模型安全约束。

## 组件与宿主边界

组件文件夹为 `plugins/qian.data.native`，只有唯一清单、派生配置 Schema 与说明，没有模型工具。默认用于 gateway 原生组合；global 是唯一允许的挂载 scope。安装分发仍需正式发布与 `qian add` 验收，未完成前保留骨架标记。

宿主通过 `AddQianNativeData(NativeDataBinding)` 登记具名绑定：名字、明确的 Tenant / Platform 用途、专用登录角色、凭据工厂与声明的表。工厂只在挂载时读取受信任 Secret；组件 JSON 只列绑定名，例如 `{"connections":["audit_tenant","audit_platform"]}`，不含连接串、口令或运行时授权字段。凭据轮换通过重挂载生效。

每张 `NativeDataTable` 声明 schema、表、保护策略名与完整 `pg_get_expr` 的 USING / WITH CHECK 期望值。期望值必须来自受审查的固定迁移源码，不能读取当前数据库后自动学习。在线组件不执行 DDL、不生成策略、不迁移。

## 真实安全检查

挂载、打开连接、开始事务和执行命令前，Provider 验证实际登录与数据库状态。登录必须恰好是绑定角色，`current_user = session_user`，具有 LOGIN / NOINHERIT，且没有超级用户、BYPASSRLS、建库、建角色、复制权限或任何角色成员关系。租户与平台绑定不得复用同一登录。

声明表必须具有 RLS 与 FORCE RLS；声明的策略必须是针对 PUBLIC 的 restrictive FOR ALL，USING 与 WITH CHECK 必须逐字匹配迁移期望值。登录不能拥有数据库/表、不能在目标 schema 建表，也不能拥有 TRUNCATE 或 TRIGGER 权限。不要求直接表 DML，因而可供受限 SECURITY DEFINER 存储函数的审计 Provider 使用。

`OpenTenantAsync` 只取当前 `ITenantContext` 中规范且非空的 UUID；调用方不能传租户字符串覆盖它。连接打开设置 session GUC，开始事务再设置 local GUC。连接、命令、读取器与事务带守卫，保存连接后切换租户仍会拒绝。没有租户不能隐式取得平台权限。

`OpenPlatformAsync` 同时要求服务 owner 与 caller 的 Scope / Entry.Scope 均为 global、当前没有租户，并验证 caller 仍能从其有效上下文解析当前同一个 NativeData 实例。租户 entry 调用 `Scoped(Global)` 不能冒充平台。

这些连接只供受信任组件执行参数化 SQL，不能作为用户输入任意 SQL 的执行器。数据库权限与声明表应由宿主按最小集合配置；NativeData 不解释或授权业务 SQL，也不替代业务鉴权。

## 事务与清理

会话拥有连接。只允许显式事务，内部关闭 ambient enlist，并拒绝在 TransactionScope 中使用。拒绝 Multiplexing、NoResetOnClose 与额外连接 Options；不提供公开的原始 Npgsql 解包口。读取器的 CloseConnection 模式被拒绝，避免绕过会话清理路径。

未结束事务在释放时回滚；提交在等待、边界校验或提交阶段取消/失败后，会话被封存，只能清理。清理使用内部连接与独立取消策略执行回滚、释放全部 session advisory lock、重置会话/角色及临时表。清理失败会清池后关闭连接，不把污染连接归还给下一租户；正常路径保留 Npgsql 自身的池重置。

组件卸载先拒绝新操作，取消并等待尚在打开/交接的连接，随后释放所有活动会话和数据源。已经拿到但尚未登记的连接也处于异常清理范围，不能落在卸载快照之外。

`ValidateTenantTransactionAsync` 接受本 Provider 的有效事务或真实 `NpgsqlTransaction`（包括 EF `GetDbTransaction()` 的结果）。它校验同一绑定端点/数据库/受限登录、真实 GUC 与当前租户，并检查服务器实际 SERIALIZABLE 隔离级别；不提交、回滚或重试调用方事务。旧业务宿主必须显式登记符合约束的绑定，不能用任意旧角色绕过检查。

## 验证

`tests/Qian.Data.Native.Postgres.Tests` 复用共享 PostgreSQL 16 fixture，覆盖两个真实租户、原始 SQL、平台上下文、事务/取消、池复用、advisory lock、卸载、外部事务与策略漂移。角色与口令按每个用例随机生成，凭据不输出、不提交。

[原生探针](../../tools/Qian.Data.Native.Probe/README.md) 使用严格 Linux NativeAOT 发布，再通过独立 driver 创建真实数据库并运行原生进程。CI 的 `native-data-aot` 作业执行发布和运行两道门，不抑制 IL 警告。普通 Release 构建、原生发布成功、原生运行成功是三个独立结论。

## 模型体验

本组件不暴露模型工具。模型只能看到组件描述与绑定名 Schema；连接、事务、凭据工厂和 SQL 不进入模型表面。公开错误只含稳定错误码，不携带连接串或密钥。

## 已知限制与延期项

当前是 PostgreSQL Provider，不提供其他数据库方言、动态查询工具或在线迁移。正式包源安装、gateway 完整组合与 DEP-008 的业务镜像分别验收，不能用本组件独立探针代替。
