# Qian.Data.Abstractions

数据 seam 的基础契约包，先交付 `QianDbContext` 与 `QianDataMisconfiguredException`，供 S1-KUN-001 的安全装配验收使用。业务组件可以依赖此 Definition 包，不依赖 `Qian.Data` Provider。

`QianDbContext` 继承 `KunDbContext`，保留租户／软删过滤与 Outbox 模型；构造时检查 RLS 双拦截器、审计/租户字段拦截器、应用角色校验拦截器、`KunMultiTenantModelCacheKeyFactory`、Qian 模型定制器及插件 schema，拒绝外部 EF 服务容器和预编译模型绕过。配置统一通过 Provider 的 options 回调传入，派生上下文不覆盖 `OnConfiguring`。

`UseQianDataModel` 由 Provider 自动装配，在业务映射后完成强制模型约定。即使派生类漏调 base，仍安装 Kun 的默认租户/软删过滤，并与业务自己的过滤条件取交集。命名、Guid v7 与 `xmin` 复用 Kun，租户列和索引由 Qian 补充。

`QianApplicationRoleInterceptor` 是安全基类要求的角色检查：同步／异步开连接时只允许非超级用户、无 BYPASSRLS 的登录角色和当前角色；Provider 启动时还会显式核验预打开连接。它只读取 pg_roles，不创建数据库角色。管理员造数／迁移必须使用独立连接，不能用管理员凭据启动在线上下文。

`DataErrors.Misconfigured` 定义稳定错误码 `DATA_MISCONFIGURED`，异常的 `Error` 为 `QianError`。它用于宿主启动失败；`AddQianData` 在启动时登记、停止时撤销该错误码。HTTP 宿主通过统一 ErrorDetail 机制处理错误。

## 组件位置

当前目录含契约代码、PublicAPI 清单与说明文档；完整 `qian.data` 组件文件夹、profile 挂载与 `qian add data` 由 S1-DATA-001 后续交付。本包是 Data seam 的 Definition，计划随 SDK 进入全部 .NET profile。

## 模型体验

Agent 可通过命名稳定的异常及错误码识别隔离配置缺失，不读取或输出数据库口令。宿主只在装配处引用 Provider，业务组件只引用本包。

## 已知限制与延期项

S1-DATA-001 仍在进行；迁移模板、数据服务键、安装表面与完整真实验收尚未完成，不因已有模型约定而提前关闭任务。
