# Qian.Authz

Authz seam 的 Provider，Definition 为 Qian.Authz.Abstractions；当前按 S1-AZ-002 建立权限声明与默认角色模板入口。组件表面为 metadata/permissions.json、metadata/roles.json；平台来源为 Registry 的宿主预装声明，独立模式来源为 PermissionAttribute。后续通过 qian.authz 装入 platform，也支持 standalone；qian add authz 的完整安装、正式发布与真实服务仍按各项门禁验收。

本包尚未完成租户角色存储与默认授予、完整判定引擎、管理 API 或可安装组件，不能以声明解析代替授权成功。S1-AZ-002 保持 doing，具体工作与验收记录在任务文件中。

## 声明编译

`PermissionCatalogCompiler` 复用表面解析器并使用本组件的两个 Schema：`authz-permissions.schema.json`、`authz-roles.schema.json`。v2 的 metadata 文件保留既有字段：权限为 `{ resource, actions[], risk{}, stepUp[] }`，角色为 `{ role, grant[] }`；风险采用按动作的稀疏映射，未标记动作取 low，未列入 stepUp 的动作不要求 step-up。terminals 同样按动作映射，省略表示未限定终端。风险和终端数据不产生授权。

权限点始终由组件 id 加资源、动作拼成全名。重复权限、重复角色、风险/step-up/终端引用未声明动作均被拒绝。默认模板的 grant 必须属于本组件，精确权限或资源动作通配符只展开为当前目录已登记的权限；未匹配的模式报错，平台角色不能由组件默认模板授予。模板编译结果包含精确 Grants 和原始 Patterns，持久化及租户授予尚未接入。

平台入口 `FromInstalled` 消费 Registry 的只读预装快照；独立入口 `FromAttributes` 消费真实 `PermissionAttribute` 实例，可由编译期代码或宿主反射提供。本库不扫描程序集，不把属性声明误作入口 PEP，也不从令牌权限集合取值。两种来源得到同一 PermissionDefinition / RoleTemplate 形状，包含风险、step-up 与终端字段。错误统一为 INPUT_INVALID + AUTHZ_METADATA_INVALID 字段错误，指向 metadata 文件，不回显非法值。


## 目录持久化

`AuthzDbContext` 继承 QianDbContext 并使用 plugin_qian_authz；宿主通过 Data 强制装配连接与应用角色边界。内部目录存储把 permission_catalogs、permission_definitions、resource_types、policy_packs 放在同一事务中，组件/版本维度的 PostgreSQL 事务锁防止多副本竞争。相同版本、相同内容幂等；内容不同返回 CONFLICT，原目录保持不变，新内容需新版本。

这些是全局声明表，尚不包含主体角色授予，不以全局元数据可读替代租户数据授权。真实 PostgreSQL 测试使用无超级用户、无 BYPASSRLS 的应用角色；验证权限/资源/模板落库、两宿主冲突不覆盖，以及策略包写入阶段数据库触发器拒绝时整笔回滚。平台 metadata 与真实属性声明在同一版本真实登记后仍是同一份行，不产生重复。
