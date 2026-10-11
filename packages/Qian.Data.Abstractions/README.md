# Qian.Data.Abstractions

Data seam 的 Definition，包含安全上下文、数据服务/会话端口、系统字段约定和错误契约。业务组件只依赖本包，Provider 位于 Qian.Data。

## 安全上下文

QianDbContext 继承 KunDbContext，保留租户/软删过滤和 Outbox 模型。构造期检查 RLS 双拦截器、软删/审计/租户字段拦截器、应用角色检查、KunMultiTenantModelCacheKeyFactory、Qian 模型定制器及插件 schema；拒绝外部 EF 服务容器和预编译模型绕过。配置统一从 Provider 的 options 回调传入，派生上下文不覆盖 OnConfiguring。

UseQianDataModel 在业务映射之后强制应用默认过滤，与业务已有条件取交集。命名、Guid v7、审计字段和 xmin 复用 Kun；租户列、索引和 DeletedBy 由 Qian 补充。DeletedBy 使用与 CreatedBy 一致的可空类型，映射为 deleted_by；没有 CLR 属性时自动创建 shadow property。

QianApplicationRoleInterceptor 校验同步/异步新连接的登录角色和当前角色；Provider 启动时还显式检查预打开连接。仅允许非超级用户且无 BYPASSRLS 的应用角色。QianSoftDeleteInterceptor 强制在 Kun 审计拦截器之前运行，将直接删除转换为软删除并填充删除审计。

## 数据端口

DataDefinition.Key 拥有 Data 服务键，`ctx.Data()` 解析当前 scope 可见的 IDataService。`OpenAsync<TContext>()` 返回 IDataSession，暴露该工作单元的 Context 和 IOutboxWriter，继承 Kun 当前 IUnitOfWork 的 SaveChangesAsync；会话通过 IAsyncDisposable 释放。Provider 负责把业务上下文、通知处理器和事件缓冲绑定在同一 scope，防止多上下文串写。

DataErrors.Misconfigured 定义统一 DATA_MISCONFIGURED，QianDataMisconfiguredException 携带 QianError。宿主统一映射为 ErrorDetail，不自造错误体。

## 组件位置

本目录提供代码契约、PublicAPI 清单和说明，适用于全部 .NET profile。Qian.Data 已有真实 Provider 入口；完整 qian.data 安装表面、profile 接线与 qian add 验收仍在 S1-DATA-001 中推进。

## 模型体验

Agent 可识别稳定的错误码和系统字段，不获取数据库口令。Data 本身不发布模型可调用的 SQL 工具。

## 已知限制与延期项

完整组件安装和参考应用的千行索引验收仍未完成。Filter AST、IRecordHook 等后续契约属于 S1-DATA-004，不在本次基础安全装配中冒充已实现。
