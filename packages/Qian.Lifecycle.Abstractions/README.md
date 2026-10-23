# Qian.Lifecycle.Abstractions

生命周期 Definition：服务键 `LifecycleDefinition.Key`（`ctx.Lifecycle()`）与 `IPluginLifecycleController`，覆盖平台级 `Install / Activate / Rollback / Deprecate / Retire`、租户级 `Entitle / Provision / Upgrade / Uninstall / Purge`、写租户 scope patch 的 `Mount / Unmount`、管理面诊断与反向权限批准（`ApprovePermissionsAsync` / `GetPermissionApprovalAsync`，受信读取 `GetApprovedPermissionsAsync`）。管理面诊断的权限点是 `qian.lifecycle:diagnostics:read`，批准的权限点是 `qian.lifecycle:install:approve`。

本包还定义以下契约：

- 制品与签名：`PluginArtifact`（组件文件夹字节 + D4 目录摘要 + 分离签名）、`ArtifactSignature`（Ed25519，KeyId 为公钥 SPKI 的 SHA-256 指纹）。
- 状态词汇：修订状态、部署健康、租户安装状态、钩子执行状态与挂载结论。
- 视图：修订、安装记录、钩子执行记录（`lifecycle_runs`）、挂载结论、装配诊断与批准记录（`PermissionApprovalView`，批准内容为五类的 `PermissionGrants`，渠道见 `PermissionApprovalChannels`）。
- 扩展点：
  - module 形态组件的进程内钩子 `IPluginLifecycleHooks`，含四个租户级钩子名与 `LifecycleHookContext`。
  - 部署执行器 `IDeploymentExecutor`，含 `DeploymentRequest`、`DeploymentHandle`、`DeploymentHealth`。
- 集成事件 `lifecycle.plugin.activated.v1`、`lifecycle.tenant-plugin.upgraded.v1` 及其登记方法 `RegisterLifecycleEvents`。
- 生命周期错误码 `LifecycleErrors`，含开通与升级缺少批准时的 `LIFECYCLE_PERMISSIONS_UNAPPROVED`，以及宿主没有装配可用审计链（租户级操作缺该租户 scope 的审计服务，平台级操作缺 global 审计服务或平台链 `chain.platformRole`）时 fail-closed 的 `LIFECYCLE_AUDIT_UNAVAILABLE`（503）。

本包只依赖 `Qian.Runtime`（含其错误协议 `Qian.Runtime.Errors`）、`Qian.Identity.Abstractions` 与 `Kun.EventBus.Abstractions`，不实现数据库访问、验签或部署。状态机语义、宿主装配、执行器与验收边界统一见 [Qian.Lifecycle](../Qian.Lifecycle/README.md)。
