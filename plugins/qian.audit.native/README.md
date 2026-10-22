# 原生审计组件

组件入口为 `Qian.Audit.Native.NativeAuditComponent`，提供原 `Audit` 与 `CommandAudit`，依赖 `NativeData`。装配、独立宿主、迁移权限、事务语义与验收见 [主文档](../../packages/Qian.Audit.Native/README.md)。

配置 Schema 从代码生成，使用构建工具同步，禁止手改受管内容。正式 feed 安装门尚未完成，保留 `x-skeleton`；独立包与最终 gateway 组合的验收边界分别记录在 S1-AUD-008 和 S1-GW-001。
