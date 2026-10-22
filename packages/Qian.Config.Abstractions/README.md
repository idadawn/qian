# Qian.Config.Abstractions

Config seam 的 Definition，只包含服务键、只读端口和统一错误词汇，直接依赖 Qian.Runtime 与标准配置抽象。真实配置源和适配器位于 Qian.Config.Apollo，组件库不依赖 Registry、Lifecycle 或 Capabilities。

ConfigDefinition.Key 对应 Config，ctx.Config() 按当前 scope 解析 IConfigService。GetValue / GetRequiredValue 读取层次键，ReadSection 返回只读快照，GetReloadToken 提供变更通知；监听句柄由消费者交给 ctx.Effect。缺失必需值使用 CONFIG_REQUIRED_MISSING，引导项不合法使用 CONFIG_BOOTSTRAP_INVALID，均走 ErrorDetail。

S1-CFG-003 增加的契约：错误码 CONFIG_RELOAD_REJECTED（强类型配置候选值未通过校验、已保留旧值）；进程内事件 `ConfigRuntimeEvents.ReloadFailed`（`config/reload-failed`，载荷 `ConfigReloadFailed` 不含配置值）与先保留名称的集成事件 `ConfigEvents.ReloadFailedType`（`config.reload.failed.v1`）；Apollo 发布审计的能力 ID `ConfigEvents.ReleaseAppliedCapabilityId` / `ReleaseRejectedCapabilityId`；配置值读取权限点 `ConfigDefinition.ValuesReadPermission`（`qian.config:values:read`）。强类型配置监视器与发布审计在 Qian.Config.Apollo，HTTP 接口在 Qian.Config.AspNetCore。

适用于全部 .NET profile。标准 qian.config 适配宿主 IConfiguration，qian.config.apollo 适配实际 Apollo 源；代码字段生成与 qian add 安装仍由 SDK/CLI 任务交付。独立宿主可直接注入 IConfigService，无需平台组件。

## 模型体验

本端口用于受信任代码读取，不自动向模型公开原始配置值；秘密权限校验与脱敏读取由专属配置任务交付。

## 已知限制与延期项

这里不定义租户设置、不实现秘密存储、特性开关或发布审批；发布审计的写入在 Provider。配置源顺序、引导及降级矩阵见 Provider README。
