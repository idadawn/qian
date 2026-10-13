# Qian.Authz

Authz seam 的 Provider，Definition 为 Qian.Authz.Abstractions；当前按 S1-AZ-002 建立权限声明与默认角色模板入口。组件表面为 metadata/permissions.json、metadata/roles.json；平台来源为 Registry 的宿主预装声明，独立模式来源为 PermissionAttribute。后续通过 qian.authz 装入 platform，也支持 standalone；qian add authz 的完整安装、正式发布与真实服务仍按各项门禁验收。

本包已实现内部租户角色与默认授权持久化，完整判定引擎、管理 API、真实租户开通接线与可安装组件尚未完成，不能以声明解析代替授权成功。S1-AZ-002 保持 doing，具体工作与验收记录在任务文件中。

## 声明编译

`PermissionCatalogCompiler` 复用表面解析器并使用本组件的两个 Schema：`authz-permissions.schema.json`、`authz-roles.schema.json`。v2 的 metadata 文件保留既有字段：权限为 `{ resource, actions[], risk{}, stepUp[] }`，角色为 `{ role, grant[] }`；风险采用按动作的稀疏映射，未标记动作取 low，未列入 stepUp 的动作不要求 step-up。terminals 同样按动作映射，省略表示未限定终端。风险和终端数据不产生授权。

权限点始终由组件 id 加资源、动作拼成全名。重复权限、重复角色、风险/step-up/终端引用未声明动作均被拒绝。默认模板的 grant 必须属于本组件，精确权限或资源动作通配符只展开为当前目录已登记的权限；未匹配的模式报错，平台角色不能由组件默认模板授予。模板编译结果包含精确 Grants 和原始 Patterns，持久化及内部租户实例化见下文；真实开通接线仍待完成。

平台入口 `FromInstalled` 消费 Registry 的只读预装快照；独立入口 `FromAttributes` 消费真实 `PermissionAttribute` 实例，可由编译期代码或宿主反射提供。本库不扫描程序集，不把属性声明误作入口 PEP，也不从令牌权限集合取值。两种来源得到同一 PermissionDefinition / RoleTemplate 形状，包含风险、step-up 与终端字段。错误统一为 INPUT_INVALID + AUTHZ_METADATA_INVALID 字段错误，指向 metadata 文件，不回显非法值。


## 目录持久化

`AuthzDbContext` 继承 QianDbContext 并使用 plugin_qian_authz；宿主通过 Data 强制装配连接与应用角色边界。内部目录存储把 permission_catalogs、permission_definitions、resource_types、policy_packs 放在同一事务中，组件/版本维度的 PostgreSQL 事务锁防止多副本竞争。相同版本、相同内容幂等；内容不同返回 CONFLICT，原目录保持不变，新内容需新版本。

这些是全局声明表，尚不包含主体角色授予，不以全局元数据可读替代租户数据授权。真实 PostgreSQL 测试使用无超级用户、无 BYPASSRLS 的应用角色；验证权限/资源/模板落库、两宿主冲突不覆盖，以及策略包写入阶段数据库触发器拒绝时整笔回滚。平台 metadata 与真实属性声明在同一版本真实登记后仍是同一份行，不产生重复。


## 租户角色与默认授权

内部 `PostgresTenantRoleStore` 用当前已验证租户上下文建立内置角色、角色主体绑定，并把已登记组件策略实例化到 grants。既有 Owner / 使用者角色成员不需要在安装每个组件时重复授予：安装默认策略后，真实角色关联查询自动得到对应权限。事务以租户维度加锁，避免跨组件共同创建内置角色时竞争；重复安装同版本不重复写入，版本变更要求匹配已有版本，只替换该组件拥有的默认授权。

roles、role_assignments、grants 保留可空 tenant_id 的平台角色存储形状；当前租户路径显式设置非空归属，用当前 DbContext 成员查询过滤，并复用 Data 的严格 RLS 迁移标记。缺失上下文和平台 NULL 行都不因此放行。平台超管的显式旁路和审计由 AZ-003 实现，不能把平台 NULL 自动当作任意请求的通行条件。

当前内部读取只覆盖直接用户角色与有效期，用于验证落库结果，不能代替完整 IPermissionChecker、组织/岗位/组继承、缓存或对外 me/permissions 接口。开通调用方仍须先证明租户与成员归属，真实 Tenancy / Identity / Lifecycle 接线尚未完成。直接 SQL 验证本租户可见、越租户写入被拒；另有两个宿主并发幂等、升级失败回滚以及旧宿主上下文退出后的新宿主查询验收。


## 活动权限与资源的可逆注册

`PermissionRegistry` 实现 Definition 中的 `IPermissionRegistry` / `IResourceTypeRegistry`，服务键分别为 `Authz.Permissions` 与既有扩展点名 `qian.authz.resource-type/v1`。声明按组件版本读取，避免两个版本的风险或终端配置相互覆盖。持久目录、活动注册、主体授权是三个独立状态：有目录记录不等于当前已挂载，有活动声明也不等于用户已获得授权。

`Register` 与 `RegisterResources` 返回可交给 `ctx.Effect` 的释放句柄。整批先检查冲突再登记；相同声明可以有多个独立持有者，最后一个释放时才消失。旧句柄重复释放不会误删后来重新登记的条目。显式资源描述撤销后，可回退为仍由权限点派生的资源身份；仅有资源描述不会凭空产生权限。

运行时测试使用真实 `PluginLoader` 与本注册表，通过声明贡献组件的 Effect 注册和卸载验证撤销语义。测试贡献组件不替代尚未完成的完整 Authz 判定 Provider，Check、缓存和对外接口仍需接通。

## RBAC 判定基础（S1-AZ-003）

`RbacPermissionChecker` 实现 Kun 的 Check、BatchCheck、ListPermissions、ListAllowedActions 和 Explain 端口。当前查询真实租户角色与授权、有效期，并结合该租户策略版本和活动权限登记。缺少租户上下文、主体租户不一致、非法主体、未安装策略版本、未登记权限或无匹配授权均拒绝。`ExplainDetailedAsync` 返回命中角色、实际授权记录定位和缺少权限，不返回另一租户的命中信息。

主体须由已认证宿主提供，不从请求角色或权限数组推导授权。step-up 与终端限制需要可信身份/终端适配器提供 `IAuthzExecutionConstraints` 证据；未接入时保持拒绝。资源实例 ACL、L4/L5 仍在各自阶段，当前只消费未限定 ResourceRef 的角色授权。

组织/岗位/组继承、Redis 缓存与 revision 失效、身份有效性接线、平台超管显式旁路和审计、完整 AuthzPlugin 及 HTTP 端点尚未完成。真实 PG 测试当前证明直接角色路径、解释、批量一致、活动声明释放与附加约束缺证据拒绝，不作为完整 AZ-003 或 QA-002 验收。

## 命令 PEP 接线

RbacCommandPolicyPlugin 消费 Data 和活动 IPermissionRegistry，调用真正的 RbacPermissionChecker，按生成能力的 requiredPermission 和 requiredScopes 判定后才委托实际 handler；令牌 roles 不授予权限。SDK 同时保留 Kun 的逐命令权限行为。生产 HTTP 已验证合法签名且声称 Owner 的服务，在数据库只有 Reader 授权时仍不能创建客户。S1 的 Agent/act 委托入口拒绝。此子插件只提供命令 PEP；完整 IAuthzService 的数据/字段策略、管理面、缓存和平台旁路仍待完成，qian.authz 骨架不撤销。
