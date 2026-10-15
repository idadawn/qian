# qian.registry

此目录为 S1-RT-002 的配置骨架（`x-skeleton: true`），提供清单与配置结构校验，不含 Provider 代码、服务键注册或就绪承诺。后续组件任务用真实实现替换骨架，并补充自己的完整配置契约。

`metadata/permissions.json` 与 `metadata/roles.json` 声明管理面读取权限点 `qian.registry:scope:read`，默认授予租户 Owner 与 Admin（S1-RT-004）；Authz 从注册表的预装快照编译，判定由宿主装配的权限端口完成。
