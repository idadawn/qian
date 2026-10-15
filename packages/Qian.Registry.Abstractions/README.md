# Qian.Registry.Abstractions

注册表 Definition 提供 Registry 服务键、宿主运行时入口、预装组件的只读元数据、管理面 scope 视图、候选制品登记与生命周期回写（`RegistryArtifactStates`）、供可信平台插件调用的公开挂载 / 卸载 / 恢复与装配诊断的端口，以及集成事件 `registry.scope.mounted.v1` / `registry.scope.unmounted.v1` 及其登记方法，并拥有 R48 的四个 403 原因码、管理面读取权限点 `qian.registry:scope:read`、条件写入冲突和 scope patch 快照。当前 Provider 的实现进度、profile 边界与真实验收统一见 [Qian.Registry](../Qian.Registry/README.md)。本包只依赖 `Qian.Runtime`、`Qian.Identity.Abstractions`（管理面入口接收宿主已验证的 `ICurrentUser`）与 `Kun.EventBus.Abstractions`（集成事件契约），不实现数据库访问或授权规则。
