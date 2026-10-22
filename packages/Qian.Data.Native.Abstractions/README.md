# NativeData Definition

本包提供 `NativeData` 服务键、`INativeDataService` 和 `INativeDataSession`，只依赖 Runtime 与错误契约，不依赖 EF 或 Npgsql。它是原生连接/事务 seam 的 Definition，与原有 `IDataService` / `Data` 并存。

`OpenTenantAsync(binding)` 使用已验证的当前租户；`OpenPlatformAsync(binding, caller)` 要求仍有效的 global 调用上下文以及专用受限登录。binding 是受信任宿主登记的名字，组件配置不包含连接凭据。

会话拥有 `DbConnection` 和当前 `DbTransaction`，支持先取得连接级 advisory lock，再以指定隔离级别开始事务。命令只能来自可信组件，不能接收用户提供的任意 SQL。连接、命令与事务的生命周期、角色和当前租户由 Provider 守卫；未完成事务随会话释放回滚。

`ValidateTenantTransactionAsync` 供同事务审计等消费者校验受管事务或外部真实事务（包括既有 EF 的实际数据库事务），检查同库绑定、登录、租户上下文与 SERIALIZABLE 隔离级别。该方法不提交、不回滚、不重试外部事务；不提供跳过守卫的公开解包入口。

## 模型体验

本包没有模型工具，不向模型公开连接、事务、连接串或数据库执行入口。

## 已知限制与延期项

Provider 与 NativeAOT 的发布/运行证明由 S1-DATA-008 接续。本 Definition 的编译不代表已完成 PostgreSQL 运行或正式安装验收。
