# Qian.Authz

Authz seam 的 Provider，Definition 为 Qian.Authz.Abstractions。组件表面为 metadata/permissions.json、metadata/roles.json；平台来源为 Registry 的宿主预装声明，独立模式来源为 PermissionAttribute 或随包 metadata。通过 qian.authz 装入 platform，也支持 standalone；qian add authz 的完整安装、正式发布仍按各项门禁验收。

本包已实现：声明编译与目录持久化、租户角色与默认授权、活动权限登记、真实 RBAC 判定（Check / BatchCheck / ListPermissions / ListAllowedActions / Explain）与主体快照缓存、平台超管显式旁路与审计、组件入口 `AuthzPlugin`（目录登记、命令 PEP、`tenant/provisioned` 时实例化默认角色），以及当前主体的有效权限（HTTP 端点 `GET /api/authz/me/permissions` 在 Qian.Authz.AspNetCore）。组织 / 岗位 / 组继承（S1-AZ-005，依赖 S1-IDP-009 组织目录）、授权变更事件与 identity_revision 失效（S1-AZ-004）、管理 API 与角色 CRUD（S1-AZ-005）、数据与字段权限编译（S2）、真实开户编排派发 `tenant/provisioned`（S1-TEN-003）以及 `qian add authz` 的可安装组件仍未完成，不能以声明解析代替授权成功。各项工作与验收记录在 S1-AZ-002 / S1-AZ-003 任务文件中。

## 声明编译

`PermissionCatalogCompiler` 复用表面解析器并使用本组件的两个 Schema：`authz-permissions.schema.json`、`authz-roles.schema.json`。v2 的 metadata 文件保留既有字段：权限为 `{ resource, actions[], risk{}, stepUp[] }`，角色为 `{ role, grant[] }`；风险采用按动作的稀疏映射，未标记动作取 low，未列入 stepUp 的动作不要求 step-up。terminals 同样按动作映射，省略表示未限定终端。风险和终端数据不产生授权。

权限点始终由组件 id 加资源、动作拼成全名。重复权限、重复角色、风险/step-up/终端引用未声明动作均被拒绝。默认模板的 grant 必须属于本组件，精确权限或资源动作通配符只展开为当前目录已登记的权限；未匹配的模式报错，平台角色不能由组件默认模板授予。模板编译结果包含精确 Grants 和原始 Patterns，持久化与租户实例化见下文「租户角色与默认授权」「租户开通」。

平台入口 `FromInstalled` 消费 Registry 的只读预装快照；独立入口 `FromAttributes` 消费真实 `PermissionAttribute` 实例，可由编译期代码或宿主反射提供。本库不扫描程序集，不把属性声明误作入口 PEP，也不从令牌权限集合取值。两种来源得到同一 PermissionDefinition / RoleTemplate 形状，包含风险、step-up 与终端字段。错误统一为 INPUT_INVALID + AUTHZ_METADATA_INVALID 字段错误，指向 metadata 文件，不回显非法值。


## 目录持久化

`AuthzDbContext` 继承 QianDbContext 并使用 plugin_qian_authz；宿主通过 Data 强制装配连接与应用角色边界。内部目录存储把 permission_catalogs、permission_definitions、resource_types、policy_packs 放在同一事务中，组件/版本维度的 PostgreSQL 事务锁防止多副本竞争。相同版本、相同内容幂等；内容不同返回 CONFLICT，原目录保持不变，新内容需新版本。

这些是全局声明表，尚不包含主体角色授予，不以全局元数据可读替代租户数据授权。真实 PostgreSQL 测试使用无超级用户、无 BYPASSRLS 的应用角色；验证权限/资源/模板落库、两宿主冲突不覆盖，以及策略包写入阶段数据库触发器拒绝时整笔回滚。平台 metadata 与真实属性声明在同一版本真实登记后仍是同一份行，不产生重复。


## 租户角色与默认授权

内部 `PostgresTenantRoleStore` 用当前已验证租户上下文建立内置角色、角色主体绑定，并把已登记组件策略实例化到 grants。既有 Owner / 使用者角色成员不需要在安装每个组件时重复授予：安装默认策略后，真实角色关联查询自动得到对应权限。事务以租户维度加锁，避免跨组件共同创建内置角色时竞争；重复安装同版本不重复写入，版本变更要求匹配已有版本，只替换该组件拥有的默认授权。

roles、role_assignments、grants 保留可空 tenant_id 的平台角色存储形状；当前租户路径显式设置非空归属，用当前 DbContext 成员查询过滤，并复用 Data 的严格 RLS 迁移标记。缺失上下文和平台 NULL 行都不因此放行；平台角色行只经专用平台登录读写，见下文「平台超管显式旁路」。roles 与 role_assignments 的唯一索引把 null 租户视为同一值（NULLS NOT DISTINCT），平台角色不会被重复建出。

每次角色、绑定、授权或策略安装的变更都在同一事务里递增 `tenant_authz_revisions` 的租户修订号（没有实际变更时不递增），判定快照以它为缓存键。`RevokeAsync` 撤销直接主体的角色绑定，不存在时返回 false；`ListMembersAsync` 返回 Authz 视角的租户成员（持有本租户角色绑定的主体及角色），平台超管的绑定是平台行，不会出现在其中。真实租户成员目录归 Identity（S1-IDP），本包不另建。租户开通经 `tenant/provisioned` 监听接通（见「租户开通」）；谁成为租户的 Owner 由开通编排的初始授权与授权管理 API 决定，不在这里。直接 SQL 验证本租户可见、越租户写入被拒；另有两个宿主并发幂等、升级失败回滚以及旧宿主上下文退出后的新宿主查询验收。


## 活动权限与资源的可逆注册

`PermissionRegistry` 实现 Definition 中的 `IPermissionRegistry` / `IResourceTypeRegistry`，服务键分别为 `Authz.Permissions` 与既有扩展点名 `qian.authz.resource-type/v1`。声明按组件版本读取，避免两个版本的风险或终端配置相互覆盖。持久目录、活动注册、主体授权是三个独立状态：有目录记录不等于当前已挂载，有活动声明也不等于用户已获得授权。

`Register` 与 `RegisterResources` 返回可交给 `ctx.Effect` 的释放句柄。整批先检查冲突再登记；相同声明可以有多个独立持有者，最后一个释放时才消失。旧句柄重复释放不会误删后来重新登记的条目。显式资源描述撤销后，可回退为仍由权限点派生的资源身份；仅有资源描述不会凭空产生权限。

运行时测试使用真实 `PluginLoader` 与本注册表，通过声明贡献组件的 Effect 注册和卸载验证撤销语义。`AuthzPlugin` 以同样方式登记它持有的目录，卸载时活动声明随之撤销，持久目录与租户授予保留。

## RBAC 判定基础（S1-AZ-003）

`RbacPermissionChecker` 实现 Kun 的 Check、BatchCheck、ListPermissions、ListAllowedActions 和 Explain 端口。当前查询真实租户角色与授权、有效期，并结合该租户策略版本和活动权限登记。缺少租户上下文、主体租户不一致、非法主体、未安装策略版本、未登记权限或无匹配授权均拒绝。`ExplainDetailedAsync` 返回命中角色、实际授权记录定位和缺少权限，不返回另一租户的命中信息。组件端口 `IAuthzService.ExplainDetailedAsync`（经 `ctx.Authz()`）委托同一方法，消费方只依赖 Qian.Authz.Abstractions 就能取得完整解释；继承自 Kun 的 `ExplainAsync` 只返回判定。

主体须由已认证宿主提供，不从请求角色或权限数组推导授权。step-up 与终端限制需要可信身份/终端适配器提供 `IAuthzExecutionConstraints` 证据；未接入时保持拒绝。资源实例 ACL、L4/L5 仍在各自阶段，当前只消费未限定 ResourceRef 的角色授权。平台主体（TenantId 为 null）在普通判定中一律拒绝，原因 `platform_subject_requires_bypass`；平台超管只能经显式旁路放行。

### 判定快照缓存与失效

求值先读当前租户的授权修订号，再按 `(租户, 主体种类, 主体, 修订)` 取主体快照：快照含该租户的策略安装版本、主体的直接角色绑定（带有效期）与这些角色在活动策略版本下的授权。有效期、活动权限登记与 step-up / 终端约束仍在每次判定时求值，所以快照不会让过期绑定或已释放的声明继续放行。构造 `RbacPermissionChecker` 时传入 Kun.Caching 的 `IKunCache`（内存或 Redis 后端）即启用缓存，TTL 10 分钟（PRD 7.5）；不传则每次读库。

授予、撤销与策略安装提交时修订号已递增，旧快照的键不再被任何宿主读取，所以失效不依赖 TTL，也不需要跨宿主广播；TTL 只负责回收旧键。键用 JSON 数组编码各段，读回后再比对快照内的租户、主体与修订，分隔符或缓存投毒拼不出另一个主体的快照。快照用源生成 JSON 走 Kun.Caching 的字节接口。PRD 7.5 的缓存键是 `(tenant, membership, identity_revision)`：组织 / 岗位 / 组归属与 identity_revision 由 Identity 与 S1-AZ-004 / S1-AZ-005 接入后再并入键，本包当前以租户授权修订覆盖直接主体的授予与撤销。

真实 PG + 真实 Redis 测试用两个宿主验证：宿主一授予后宿主二首次判定即放行，宿主二撤销后宿主一首次判定即拒绝（实测 10–15 ms，验收上限 3 秒）；第二次判定命中缓存，撤销不存在的绑定不递增修订、已缓存快照继续命中。内存后端同样通过。

## 命令 PEP 接线

命令 PEP 消费 Data 和活动 IPermissionRegistry，调用真正的 RbacPermissionChecker，按生成能力的 requiredPermission 和 requiredScopes 判定后才委托实际 handler；令牌 roles 不授予权限。SDK 同时保留 Kun 的逐命令权限行为。生产 HTTP 已验证合法签名且声称 Owner 的服务，在数据库只有 Reader 授权时仍不能创建客户。S1 的 Agent/act 委托入口拒绝。`AuthzPlugin` 在 Apply 中注册同一个 PEP；`RbacCommandPolicyPlugin` 是只含 PEP 的子插件，不登记目录、不监听开通，可装在任意 scope，供只需要命令判定的装配使用。PEP 的统一入口、决策日志与授权变更传播归 S1-AZ-004。

## 组件入口（AuthzPlugin）

`AuthzPlugin` 装在 global scope，提供四个服务键：`Authz`（`IAuthzService`，五个判定方法与有效权限列表委托真实 RBAC，宿主装配 Kun.Caching 时缓存主体快照）、`Authz.Permissions` 与 `qian.authz.resource-type/v1`（活动权限登记表；宿主 DI 已有 `IPermissionRegistry` 时共用同一实例，供宿主端点的判定器与注册表管理面读取），以及 `Authz.SuperadminBypass`（平台超管显式旁路）；另经 `CommandPolicies.Register` 提供命令 PEP（`CommandAuthorization`）。错误码随 Apply 登记、卸载时撤销。

权限目录有三种来源，对应三个构造方式：`new AuthzPlugin()` 不预置目录，组件经 `Authz.Permissions` 自行登记；`new AuthzPlugin(catalogs)` 用于独立宿主，目录由宿主从 PermissionAttribute 或随包 metadata 编译后传入（每个组件一份），依赖 Data；`AuthzPlugin.FromInstalledComponents()` 用于平台宿主，Apply 时从 `ctx.Registry()` 的预装组件读取 metadata/permissions.json 与 roles.json（缺 roles.json 视为没有默认角色），依赖 Data 与 Registry，宿主须登记 `ISurfaceReader`。Apply 对每份目录先持久化（同版本同内容幂等，内容不同 CONFLICT 使 Apply 失败），再登记活动声明并交给 Effect。平台模式只读 Apply 时的预装快照；运行期新安装组件的目录登记不在本组件内。

清单声明的 provides 受 plugin.schema.json 服务键模式约束（`Name` 或 `Name.Port:variant`），`Authz.Permissions`、`Authz.SuperadminBypass` 与扩展点名 `qian.authz.resource-type/v1` 不符合该模式。参考应用与 T23 宿主的清单只声明 `CommandAuthorization` 与 `Authz`；其余三个键由 Provider 额外提供（装载器只要求声明的键都已提供，不禁止额外提供），运行期可按服务键 `TryInject` 取用（注册表管理面取旁路端口即如此），但其它组件不能把它们写进 inject：装载器按 provides 声明校验依赖并排定挂载顺序，找不到声明时报 DEPENDENCY_MISSING。键名与模式如何统一另行裁定。

配置只接受两个字段：`advancedMode` 只能为 false（S1 没有高级模式，数据 / 字段权限编译属 S2）；可选的 `platformRole` 是专用平台授权登录角色名，配置后旁路才可能放行，未配置时旁路端口仍在但一律拒绝（`platform_role_not_configured`），因为清单的 provides 是静态的，缺配置不等于放行。tenant scope 装载、未知字段、非法角色名都使 Apply 失败；`platformRole` 指向的连接若是普通应用凭据（可继承、能读租户授权表），Apply 以 `QianDataMisconfiguredException` 失败。

数据与字段端口在 S2 编译器交付前恒拒绝：`GetExpressionAsync` 返回恒假表达式，`GetPredicateAsync` 抛 AUTHZ_DENIED（谓词 AST 还没有「恒假」的约定词汇，返回任何谓词都可能被编译器当作放宽），`IFieldPolicy` 不给任何可读、可写字段。

`plugins/qian.authz` 的组件目录仍是骨架（清单 `x-skeleton: true`、provides 为空），`qian add authz` 的安装、发布与 Ready 承诺另有验收；配置 Schema 已按上述两个字段更新。

## 租户开通（tenant/provisioned）

`AuthzPlugin` 以 serial 模式监听 `tenant/provisioned`（组件规格 §6.3：qian.authz 监听开通事件实例化 metadata/roles.json 的默认角色）。只接受 `qian.tenancy` 在目标租户 scope 派发、事件主体即该租户的通知，其余以 INPUT_INVALID 拒绝且不写任何行，与 hooks 桥接的生产方校验相同。通过后进入该租户上下文，在一个事务里建四个内置角色（tenant.owner / admin / developer / user），并为尚未装进该租户的每个组件实例化默认策略：模板里的角色不存在时建出，授权按组件当前版本写入。已装任意版本的组件保持不变，所以重复投递幂等，也不会把旧租户静默升到新模板（版本升级归 Lifecycle 的租户级升级）；没有实际变更时授权修订号不递增，已缓存的判定快照继续有效。监听器返回 null，不读载荷里的 plan（S1 的默认角色不随套餐变化）。

开通只建角色与默认授权，不绑定任何主体：租户管理员的 Membership 与初始授权由开通编排写入（S1-TEN-003），之后的角色分配走授权管理 API（S1-AZ-005）。开通编排须在派发 `tenant/provisioned` 之后再写初始授权，否则内置角色尚不存在；真实开户流程派发本事件的接线同样归 S1-TEN-003。

## 当前主体的有效权限（me/permissions）

`IAuthzService.ListEffectivePermissionsAsync(subject)` 列出主体经角色授权命中的权限点：按当前租户的策略安装版本取活动声明，逐个求值，只返回存在匹配授权的条目，按权限点序数排序。每条为 `EffectiveGrant(Definition, Allowed, Reason)`，与同一上下文下 Check 的结果一致；需要 step-up 或终端证明的权限点照样列出，`Allowed` 为 false、原因为 `step_up_required` 或 `terminal_context_required`，界面据此提示二次验证而不是隐藏入口。主体无效、租户上下文缺失或不一致、平台主体都返回空列表，不抛错、不泄露其它租户。

HTTP 端点由 Qian.Authz.AspNetCore 提供：`MapQianAuthz(source)` 登记 `GET /api/authz/me/permissions`，`source` 返回可注入 `Authz` 的插件上下文（参考应用传入 authz 组件按请求租户收窄的上下文）。主体与租户只取自已认证请求（`ICurrentUser`），不读请求参数；未认证 401（UNAUTHENTICATED），委托令牌与 Agent 主体在 S1 一律 403（AUTHZ_DENIED），授权组件未就绪或已卸载时与不存在同为 404，响应带 `Cache-Control: private, no-store`。响应为 `{ items: [{ permission, resource, action, risk, stepUp, terminals, allowed, reason }] }`，resource 为组件前缀的资源类型（如 `qian.sample.crm:customer`），risk 为 low / medium / high / critical。授权的其余 HTTP 端点（Check / Explain、角色与授权管理）归 S1-AZ-005。

参考应用 TCK（Qian.Sample.Crm.Tests 的 `参考应用清单经tenant_provisioned实例化默认角色_…`）在真实宿主上验证：清单声明的 customer 读、写、导出登记为全名；非 qian.tenancy 生产方或主体不符的通知被拒；新租户经真实 qian.tenancy 上下文派发开通后，Owner 经 HTTP 读到三项（导出 high、step-up、web 终端、allowed=false），使用者只读到 read，与 Check 一致；重复开通不改变任何行与修订；卸载 Provider 后端点 404，持久目录与授予保留。平台模式由 Qian.Registry.Tests 的 `平台模式Authz从预装组件读取权限目录_…` 验证：从注册表预装组件编译、持久化并登记目录，开通后按 qian.registry 的默认角色授权，卸载后活动声明撤销。

## 平台超管显式旁路（ADR-006 D6.3 / D6.11）

`ISuperadminBypass.AuthorizeAsync(SuperadminAccess)` 是平台超管的唯一放行路径：调用方以平台主体（TenantId 为 null、种类 user）显式请求，普通判定对平台主体一律拒绝。放行条件依次为：通道合法；目标租户与当前租户上下文一致（租户资源须在目标租户的上下文中判定，平台资源须无租户上下文）；权限点已登记（租户资源要求该组件策略已装进目标租户且版本仍在活动登记中，平台资源要求持久目录中某个版本仍在活动登记中）；主体持有效期内的 `platform.superadmin`；审计写入成功。任一条件不满足即拒绝，只给稳定原因码，不写审计。

放行前先写审计：租户资源经 `IAuditWriter.AppendAsync` 记入目标租户的链，平台资源经 `AppendPlatformAsync` 以 Authz 组件自己的 global 上下文为生产方记入平台链。记录的能力 ID 为 `qian.authz/superadmin.bypass`，结果 `allowed`，payload 为 `{ bypass: "superadmin", role: "platform.superadmin", permission }`，资源与通道取自请求，输入哈希由权限、租户、资源、通道算出。Audit 未装配或写入失败一律拒绝。放行结果的原因为 `superadmin_bypass`，命中策略为 `role:platform.superadmin` 与 `bypass:superadmin`。

超管对租户不可见：超管的角色与绑定只存平台行，旁路不在目标租户创建任何角色、绑定或成员记录，租户的成员视图看不到超管，审计链里看得到（D6.11「对租户不可见，但审计可见」）。注册表管理面的 scope 视图已接入：平台主体在目标租户上下文中读取时只走旁路，见 Qian.Registry 主文档。tenancy 运营接口（S1-TEN-006，R85）按同一端口以平台资源接入。

### 平台角色存储与专用登录

平台角色行（tenant_id 为 null 的 roles / role_assignments）只经专用平台授权登录读写，形式与审计平台链相同：宿主在迁移阶段用 `AuthzPlatformAccess.CreateMigrationSql(role)` 改写这两张表的 `qian_tenant_guard` 策略，普通租户凭据仍只能读写当前租户的行，平台登录只能在无租户上下文时读写平台行；平台登录只获授这两张表的增删改查，没有授权表、修订表的任何权限。角色须事先独立创建为受限 NOINHERIT LOGIN、无角色成员关系，凭据由宿主单独绑定为 `PlatformAuthzDbContext` 的连接，不写进配置。每次使用前校验当前与会话角色都是该登录、属性受限、表权限符合上述边界。

内部 `PostgresPlatformRoleStore` 只供平台运营流程与测试引导：授予与撤销必须在无租户上下文中进行（从某个租户的请求里顺手提升被拒绝），只授予人类用户；判定时临时退到无租户上下文读取平台行，退出后恢复原上下文。授予平台超管的管理能力属 humanOnly（PRD 7.5 Agent 契约），随 S1-AZ-005 的管理 API 交付。真实 PG 测试验证：应用角色看不到、也写不进平台行；平台登录看不到租户行、读不到授权表；在租户上下文中授予被拒；平台登录复用普通凭据时组件装载失败。

S1-TEN-003 增补 `TenantRoleGrant.Key` / `ITenantRoleGrant`：为当前租户主体幂等绑定既有内置角色，实际写入仍走 PostgresTenantRoleStore.Write，不新增跨租户权限或第二套授权表。TEN-003 的开户 hook 使用经批准的 DEC-023 内部凭据：仅绑定能力的一次执行可以走初始化授权，其余命令继续真实 RBAC 判定。
