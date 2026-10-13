# 能力目录 Provider

- 以 S1-RT-006、冻结 capability-entry、组件规格和 SDK 的真实绑定为准；主文档为 README.md。
- 只进 platform。目录、提示词和执行共享同一权限/scope 判定，不从预装清单推断可调用资格。
- 复用真实 IPermissionChecker 与 Registry scope 视图；不登记替身 PDP、不信任客户端 roles、tenant 或授权布尔值。
- 被过滤项在业务/模型表面统一不存在；管理原因留在 Registry 管理面。
- 元数据有效不等于代码、权限、菜单、OpenAPI、契约兼容或完整 profile 验收完成。
