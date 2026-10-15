# Authz Provider

- 权威设计是 ADR-006、组件规格 §6.3 与 S1 §7.5；权限不从令牌权限集合推导。
- Definition 为 Qian.Authz.Abstractions，主文档为 README.md；平台经 Registry 的预装/发布元数据登记，独立模式经同语义的属性声明登记。
- 权限点以组件 id 加资源、动作短名派生；角色模板只能引用本组件已登记的权限，通配符先展开，不能扩大为未知或外部权限。
- 元数据登记不等于租户角色已授予；真实角色持久化、求值、缓存、审计、接口都按任务验收，不以替身关闭。
- 数据库实现只依赖 Data Definition 并继承 QianDbContext；租户读写用受限角色及 RLS。
- 平台超管只经 `ISuperadminBypass` 放行：普通判定对平台主体（TenantId 为 null）一律拒绝；旁路先写审计（租户资源进目标租户链、平台资源进平台链），审计不可用或失败即拒绝。不要在 roles / grants 查询里给 null 租户开口子。
- 平台角色行只经专用平台登录（`PlatformAuthzDbContext` + `AuthzPlatformAccess.CreateMigrationSql`）读写；授予与撤销必须在无租户上下文中进行。不要用普通应用凭据或租户上下文读写平台行。
- 任何改变角色、绑定、授权或策略安装的写入都必须走 `PostgresTenantRoleStore.Write`，与变更同事务递增租户授权修订；绕开它会让各宿主继续命中旧快照。快照只缓存角色与授权，有效期、活动登记与 step-up / 终端约束必须留在每次判定时求值。
- `tenant/provisioned` 监听只接受 qian.tenancy 在目标租户 scope 派发、主体即该租户的通知；开通只建内置角色与尚未安装组件的默认策略，不绑定主体，也不升级已装组件（升级归 Lifecycle）。
- 有效权限列表（me/permissions）必须与 Check 走同一求值路径，不另写判定；step-up / 终端受限的权限点照样列出并给出拒绝原因。
