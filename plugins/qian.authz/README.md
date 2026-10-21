# qian.authz

此目录仍为组件安装骨架（`x-skeleton: true`），提供清单与配置校验。Provider 已在 [Qian.Authz](../../packages/Qian.Authz/README.md) 中实现（`AuthzPlugin`：判定服务、活动权限登记、资源类型扩展点、平台超管显式旁路、命令 PEP，以及 `tenant/provisioned` 时为新租户实例化默认角色），当前主体的有效权限端点 `GET /api/authz/me/permissions` 在 Qian.Authz.AspNetCore；但清单的 provides、`qian add authz` 安装与 Ready 承诺仍未完成，不能把骨架视为完整就绪的组件。

配置只有三个字段：`advancedMode` 只接受 false（S1 没有高级模式）；可选的 `platformRole` 只允许 global Provider，角色须由宿主单独创建为受限平台授权登录并绑定连接，迁移阶段执行 `AuthzPlatformAccess.CreateMigrationSql`，配置里不存口令，未配置时旁路端口一律拒绝；可选的 `decisionLogSampling`（0–1，缺省 1）是放行判定写入决策审计的采样比例，拒绝一律记录。Provider 同时提供三处强制执行点共用的执行端口 `AuthzEnforcement`（HTTP 端点 `RequireQianPermission`、gRPC 拦截器、命令管道），并在授权变更时经 Outbox 发出 `authz.grant.changed.v1` / `authz.role.changed.v1`，宿主须登记这两个事件类型与处理器（S1-AZ-004）。完整边界以 Provider 主文档为准。
