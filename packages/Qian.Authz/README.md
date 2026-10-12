# Qian.Authz

Authz seam 的 Provider，Definition 为 Qian.Authz.Abstractions；当前按 S1-AZ-002 建立权限声明与默认角色模板入口。组件表面为 metadata/permissions.json、metadata/roles.json；平台来源为 Registry 的宿主预装声明，独立模式来源为 PermissionAttribute。后续通过 qian.authz 装入 platform，也支持 standalone；qian add authz 的完整安装、正式发布与真实服务仍按各项门禁验收。

本包已实现内部租户角色与默认授权持久化，完整判定引擎、管理 API、真实租户开通接线与可安装组件尚未完成，不能以声明解析代替授权成功。S1-AZ-002 保持 doing，具体工作与验收记录在任务文件中。

## 声明编译

`PermissionCatalogCompiler` 复用表面解析器并使用本组件的两个 Schema：`authz-permissions.schema.json`、`authz-roles.schema.json`。v2 的 metadata 文件保留既有字段：权限为 `{ resource, actions[], risk{}, stepUp[] }`，角色为 `{ role, grant[] }`；风险采用按动作的稀疏映射，未标记动作取 low，未列入 stepUp 的动作不要求 step-up。terminals 同样按动作映射，省略表示未限定终端。风险和终端数据不产生授权。

权限点始终由组件 id 加资源、动作拼成全名。重复权限、重复角色、风险/step-up/终端引用未声明动作均被拒绝。默认模板的 grant 必须属于本组件，精确权限或资源动作通配符只展开为当前目录已登记的权限；未匹配的模式报错，平台角色不能由组件默认模板授予。模板编译结果包含精确 Grants 和原始 Patterns，持久化及租户授予尚未接入。

平台入口 `FromInstalled` 消费 Registry 的只读预装快照；独立入口 `FromAttributes` 消费真实 `PermissionAttribute` 实例，可由编译期代码或宿主反射提供。本库不扫描程序集，不把属性声明误作入口 PEP，也不从令牌权限集合取值。两种来源得到同一 PermissionDefinition / RoleTemplate 形状，包含风险、step-up 与终端字段。错误统一为 INPUT_INVALID + AUTHZ_METADATA_INVALID 字段错误，指向 metadata 文件，不回显非法值。


## 目录持久化

`AuthzDbContext` 继承 QianDbContext 并使用 plugin_qian_authz；宿主通过 Data 强制装配连接与应用角色边界。内部目录存储把 permission_catalogs、permission_definitions、resource_types、policy_packs 放在同一事务中，组件/版本维度的 PostgreSQL 事务锁防止多副本竞争。相同版本、相同内容幂等；内容不同返回 CONFLICT，原目录保持不变，新内容需新版本。

这些是全局声明表，尚不包含主体角色授予，不以全局元数据可读替代租户数据授权。真实 PostgreSQL 测试使用无超级用户、无 BYPASSRLS 的应用角色；验证权限/资源/模板落库、两宿主冲突不覆盖，以及策略包写入阶段数据库触发器拒绝时整笔回滚。平台 metadata 与真实属性声明在同一版本真实登记后仍是同一份行，不产生重复。


## 租户角色与默认授权

内部 `PostgresTenantRoleStore` 用当前已验证租户上下文建立内置角色、角色主体绑定，并把已登记组件策略实例化到 grants。既有 Owner / 使用者角色成员不需要在安装每个组件时重复授予：安装默认策略后，真实角色关联查询自动得到对应权限。事务以租户维度加锁，避免跨组件共同创建内置角色时竞争；重复安装同版本不重复写入，版本变更要求匹配已有版本，只替换该组件拥有的默认授权。

roles、role_assignments、grants 保留可空 tenant_id 的平台角色存储形状；当前租户路径显式设置非空归属，用当前 DbContext 成员查询过滤，并复用 Data 的严格 RLS 迁移标记。缺失上下文和平台 NULL 行都不因此放行。平台超管的显式旁路和审计由 AZ-003 实现，不能把平台 NULL 自动当作任意请求的通行条件。

当前内部读取只覆盖直接用户角色与有效期，用于验证落库结果，不能代替完整 IPermissionChecker、组织/岗位/组继承、缓存或对外 me/permissions 接口。开通调用方仍须先证明租户与成员归属，真实 Tenancy / Identity / Lifecycle 接线尚未完成。直接 SQL 验证本租户可见、越租户写入被拒；另有两个宿主并发幂等、升级失败回滚以及旧宿主上下文退出后的新宿主查询验收。
