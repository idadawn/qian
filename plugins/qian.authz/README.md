# qian.authz

此目录仍为组件安装骨架（`x-skeleton: true`），提供清单与配置校验。Provider 已在 [Qian.Authz](../../packages/Qian.Authz/README.md) 中实现（`AuthzPlugin`：判定服务、活动权限登记、资源类型扩展点与平台超管显式旁路），但清单的 provides、`qian add authz` 安装与 Ready 承诺仍未完成，不能把骨架视为完整就绪的组件。

配置只有两个字段：`advancedMode` 只接受 false（S1 没有高级模式）；可选的 `platformRole` 只允许 global Provider，角色须由宿主单独创建为受限平台授权登录并绑定连接，迁移阶段执行 `AuthzPlatformAccess.CreateMigrationSql`，配置里不存口令。未配置时旁路端口一律拒绝。完整边界以 Provider 主文档为准。
