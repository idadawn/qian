# qian.lifecycle

此目录的清单仍是 S1-RT-002 的配置骨架（`x-skeleton: true`）；Provider 已由 [Qian.Lifecycle](../../packages/Qian.Lifecycle/README.md) 实现（S1-RT-005），清单的代码字段随 SDK 的组件文件夹生成补齐。

- `schemas/config.schema.json`：钩子租约与部署执行器（`gitops` / `local`）的完整配置契约，基础组合包的 `config: {}` 表示不配执行器（安装 service 组件返回 `LIFECYCLE_DEPLOYMENT_UNAVAILABLE`）。
- `metadata/permissions.json` 与 `metadata/roles.json` 声明管理面诊断读取权限点 `qian.lifecycle:diagnostics:read`（`GET /api/lifecycle/diagnostics`）与反向权限批准权限点 `qian.lifecycle:install:approve`（`PUT / GET /api/lifecycle/tenants/{tenant}/plugins/{plugin}/versions/{version}/permissions`），都默认授予租户 Owner 与 Admin；Authz 从注册表的预装快照编译，判定由宿主装配的权限端口完成。
- 信任根不在本组件配置里：平台公钥经 Apollo 公共 namespace `qian.common.trust` 的 `Trust:PlatformKeys` 节引用 K8s Secret，见 Provider README。
