# Qian.Data.Abstractions

数据 seam 的基础契约包，先交付 `QianDbContext` 与 `QianDataMisconfiguredException`，供 S1-KUN-001 的安全装配验收使用。业务组件可以依赖此 Definition 包，不依赖 `Qian.Data` Provider。

`QianDbContext` 继承 `KunDbContext`，保留租户／软删过滤与 Outbox 模型；构造时检查 RLS 双拦截器、`KunMultiTenantModelCacheKeyFactory`，拒绝外部 EF 服务容器和预编译模型绕过。配置统一通过 Provider 的 options 回调传入，派生上下文不覆盖 `OnConfiguring`。

`DataErrors.Misconfigured` 定义稳定错误码 `DATA_MISCONFIGURED`，异常的 `Error` 为 `QianError`。它用于宿主启动失败；`AddQianData` 在启动时登记、停止时撤销该错误码。HTTP 宿主通过统一 ErrorDetail 机制处理错误。

## 组件位置

当前目录含契约代码、PublicAPI 清单与说明文档；完整 `qian.data` 组件文件夹、profile 挂载与 `qian add data` 由 S1-DATA-001 后续交付。本包是 Data seam 的 Definition，计划随 SDK 进入全部 .NET profile。

## 模型体验

Agent 可通过命名稳定的异常及错误码识别隔离配置缺失，不读取或输出数据库口令。宿主只在装配处引用 Provider，业务组件只引用本包。

## 已知限制与延期项

此处只是 S1-KUN-001 所需安全基础。审计列、并发令牌、命名约定、主键策略、迁移模板、数据服务键与安装表面仍由 S1-DATA-001 及后续 DATA 任务负责。
