# qian.registry

此目录为 S1-RT-002 的配置骨架（`x-skeleton: true`），提供清单与配置结构校验，不含 Provider 代码、服务键注册或就绪承诺。后续组件任务用真实实现替换骨架，并补充自己的完整配置契约。

`metadata/permissions.json` 与 `metadata/roles.json` 声明管理面读取权限点 `qian.registry:scope:read`，默认授予租户 Owner 与 Admin（S1-RT-004），写权限 `qian.registry:scope:write` 仅默认授予 Owner（S1-RT-029）；Authz 从注册表的预装快照编译，判定由宿主装配的权限端口完成。

`metadata/contracts.json` 索引真实 HTTP 端点导出的 scope patch 条件写入契约；Provider、HTTP 与 CLI 交付范围见 [注册表主文档](../../packages/Qian.Registry/README.md)。骨架标记与代码清单生成仍由 S1-RT-033 处理。
