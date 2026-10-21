# Qian.Authz

Authz seam 的 Provider，Definition 为 Qian.Authz.Abstractions。组件表面为 metadata/permissions.json、metadata/roles.json；平台来源为 Registry 的宿主预装声明，独立模式来源为 PermissionAttribute 或随包 metadata。通过 qian.authz 装入 platform，也支持 standalone；qian add authz 的完整安装、正式发布仍按各项门禁验收。

本包已实现：声明编译与目录持久化、租户角色与默认授权、活动权限登记、真实 RBAC 判定（Check / BatchCheck / ListPermissions / ListAllowedActions / Explain）与主体快照缓存、平台超管显式旁路与审计、组件入口 `AuthzPlugin`（目录登记、命令 PEP、`tenant/provisioned` 时实例化默认角色）、当前主体的有效权限（HTTP 端点 `GET /api/authz/me/permissions` 在 Qian.Authz.AspNetCore），以及 S1-AZ-004 的三处强制执行点（HTTP 端点、gRPC 拦截器、命令管道共用的执行端口）、决策审计、identity_revision 服务端比对、授权变更事件与两条授予守卫（见「强制执行点与决策审计」）。组织 / 岗位 / 组继承（S1-AZ-005，依赖 S1-IDP-009 组织目录）、管理 API 与角色 CRUD（S1-AZ-005）、数据与字段权限编译（S2）、真实开户编排派发 `tenant/provisioned`（S1-TEN-003）以及 `qian add authz` 的可安装组件仍未完成，不能以声明解析代替授权成功。各项工作与验收记录在 S1-AZ-002 / S1-AZ-003 任务文件中。

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

授予、撤销与策略安装提交时修订号已递增，旧快照的键不再被任何宿主读取，所以失效不依赖 TTL，也不需要跨宿主广播；TTL 只负责回收旧键。键用 JSON 数组编码各段，读回后再比对快照内的租户、主体与修订，分隔符或缓存投毒拼不出另一个主体的快照。快照用源生成 JSON 走 Kun.Caching 的字节接口。键的第四段是令牌的 identity_revision（S1-AZ-004，R103；未携带为空串）：身份修订递增后新令牌读新键，旧键只等 TTL 回收。PRD 7.5 的缓存键是 `(tenant, membership, identity_revision)`：组织 / 岗位 / 组归属由 S1-AZ-005 接入后再并入键，本包当前以租户授权修订覆盖直接主体的授予与撤销。

真实 PG + 真实 Redis 测试用两个宿主验证：宿主一授予后宿主二首次判定即放行，宿主二撤销后宿主一首次判定即拒绝（实测 10–15 ms，验收上限 3 秒）；第二次判定命中缓存，撤销不存在的绑定不递增修订、已缓存快照继续命中。内存后端同样通过。

## 命令 PEP 接线

命令 PEP 消费 Data 和活动 IPermissionRegistry，调用真正的 RbacPermissionChecker，按生成能力的 requiredPermission 和 requiredScopes 判定后才委托实际 handler；令牌 roles 不授予权限。SDK 同时保留 Kun 的逐命令权限行为。生产 HTTP 已验证合法签名且声称 Owner 的服务，在数据库只有 Reader 授权时仍不能创建客户。S1 的 Agent/act 委托入口拒绝。`AuthzPlugin` 在 Apply 中注册同一个 PEP；`RbacCommandPolicyPlugin` 是只含 PEP 的子插件，不登记目录、不监听开通，可装在任意 scope，供只需要命令判定的装配使用。命令 PEP 自 S1-AZ-004 起经执行端口判定（决策审计、identity_revision 比对与 HTTP / gRPC 同一路径），见「强制执行点与决策审计」。

## 组件入口（AuthzPlugin）

`AuthzPlugin` 装在 global scope，提供六个服务键：`Authz`（`IAuthzService`，五个判定方法、有效权限列表与完整解释委托真实 RBAC，宿主装配 Kun.Caching 时缓存主体快照）、`Authz.Permissions` 与 `qian.authz.resource-type/v1`（活动权限登记表；宿主 DI 已有 `IPermissionRegistry` 时共用同一实例，供宿主端点的判定器与注册表管理面读取）、`Authz.SuperadminBypass`（平台超管显式旁路），`Authz.TenantRoleGrant`（`ITenantRoleGrant`，开户编排为主体绑定内置租户角色的最小授予端口，S1-TEN-003），以及 `AuthzEnforcement`（`IAuthzEnforcement`，三处 PEP 共用的执行端口，S1-AZ-004）；另经 `CommandPolicies.Register` 提供命令 PEP（`CommandAuthorization`）。错误码随 Apply 登记、卸载时撤销。

权限目录有三种来源，对应三个构造方式：`new AuthzPlugin()` 不预置目录，组件经 `Authz.Permissions` 自行登记；`new AuthzPlugin(catalogs)` 用于独立宿主，目录由宿主从 PermissionAttribute 或随包 metadata 编译后传入（每个组件一份），依赖 Data；`AuthzPlugin.FromInstalledComponents()` 用于平台宿主，Apply 时从 `ctx.Registry()` 的预装组件读取 metadata/permissions.json 与 roles.json（缺 roles.json 视为没有默认角色），依赖 Data 与 Registry，宿主须登记 `ISurfaceReader`。Apply 对每份目录先持久化（同版本同内容幂等，内容不同 CONFLICT 使 Apply 失败），再登记活动声明并交给 Effect。平台模式只读 Apply 时的预装快照；运行期新安装组件的目录登记不在本组件内。

清单声明的 provides 受 plugin.schema.json 服务键模式约束（`Name` 或 `Name.Port:variant`），`Authz.Permissions`、`Authz.SuperadminBypass`、`Authz.TenantRoleGrant` 与扩展点名 `qian.authz.resource-type/v1` 不符合该模式。参考应用与 T23 宿主的清单只声明 `CommandAuthorization` 与 `Authz`；其余四个键由 Provider 额外提供（装载器只要求声明的键都已提供，不禁止额外提供），运行期可按服务键取用（注册表管理面用 `TryInject` 取旁路端口，开户编排用 `Inject` 取授予端口），但其它组件不能把它们写进 inject：装载器按 provides 声明校验依赖并排定挂载顺序，找不到声明时报 DEPENDENCY_MISSING。键名按 R101 统一：服务键改为单段键，`qian.authz.resource-type/v1` 只作扩展点契约名，由 S1-RT-033 落地；`Authz.TenantRoleGrant` 由 S1-TEN-003 新增、晚于 R101，按同一通则处理。

配置只接受三个字段：`advancedMode` 只能为 false（S1 没有高级模式，数据 / 字段权限编译属 S2）；可选的 `platformRole` 是专用平台授权登录角色名，配置后旁路才可能放行，未配置时旁路端口仍在但一律拒绝（`platform_role_not_configured`），因为清单的 provides 是静态的，缺配置不等于放行；可选的 `decisionLogSampling`（0–1，缺省 1）是放行判定写入决策审计的采样比例。宿主还须登记授权变更事件类型（`AddKunEventBus(r => r.RegisterAuthzEvents())`，Apply 时校验）与处理器（`AddKunMediator(m => m.AddQianAuthzEvents())`）。tenant scope 装载、未知字段、非法角色名都使 Apply 失败；`platformRole` 指向的连接若是普通应用凭据（可继承、能读租户授权表），Apply 以 `QianDataMisconfiguredException` 失败。

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

开户默认角色监听以 priority=1000 先于默认 priority=0 的组件 hook 桥接执行；无论 Authz 在挂载或重挂载时先后登记，组件 hook 看到的四个内置角色均已建好。该顺序不改变初始主体授予仍在整个 tenant/provisioned 派发之后的 R96 约束。

## 数据范围契约与元数据（S1-AZ-006）

DataScopeRule 在 Authz Definition 声明 resource、allData 与可选的 JSON Filter AST；规则 Schema、样例与离线用法见 [数据权限契约 v1](../../contracts/authz/v1/README.md)。Filter AST、矩阵和系统变量直接取 Data 的同一份源文件，不在授权库重建字段类型或变量目录，也不为 Definition 增加数据库依赖。

GET /api/authz/meta 与 me/permissions 共用 MapQianAuthz 的认证入口；返回 dataScopeRule、filterAst、operators、systemVariables 和 merge 元数据，不返回主体角色、授权集合或实际数据。匿名拒绝，Agent/委托主体拒绝，Authz 未挂载时 404；响应 private/no-store。

DataScopeComposition.ForResource 只建立逻辑计划：同角色 AND（重复角色输入仍属于同一分支）、不同角色 OR，任何明确 allData 只覆盖该资源的数据范围，缺少规则/空角色默认拒绝。每条规则先经同源 Schema 结构校验，计划克隆 JSON 片段，保留各 Filter AST 内部逻辑，避免展平时改变括号含义或指数展开。allData=true 与 filter 不可同时存在；false 时 filter 必填。

这些类型不开放行级访问。S1 的 DataPermissions/Fields 端口保持原先拒绝实现；字段语义、变量解析、SQL/表达式编译及 RLS 叠加由 S2 执行。元数据可供前端/模型解释规则，不作为工具授予权限。

## 基础接线与拒绝路径复核（S1-AZ-008）

经真实 PluginLoader 装载 AuthzPlugin、从宿主注入 IKunCache 的测试覆盖 memory、Redis 与未装缓存三种情况：第二次判定命中缓存，授予/撤销提交后的下一次判定立即反映变化。该验证经过组件构造路径，不只测试直接创建的 checker。

真实数据库触发器让超管旁路的审计追加抛错时，结果为 audit_failed 且不放行，审计记录与链头都回滚；恢复写入后同一请求放行、链从序号 1 连续建立。完整解释用例按数据库实际 grant ID 核对命中链路，区分命中角色与主体全部角色，Check/Explain 同时核对 Allow 与 Reason，并以租户 B 的同名主体验证 RLS 后结果不泄露 A 的授权。

## 强制执行点与决策审计（S1-AZ-004）

### 执行端口

`IAuthzEnforcement`（服务键 `AuthzEnforcement`，`ctx.Enforcement()`）是 HTTP 端点、gRPC 拦截器与命令管道三处 PEP 共用的执行核心，不是第二套判定：租户主体的判定仍只来自 `RbacPermissionChecker.ExplainDetailedAsync`（作为 `authz/checking` waterfall 的链尾），平台主体只经 `ISuperadminBypass`。`EnforcementRequest` 携带主体快照（id、租户、种类、identity_revision、是否委托）、权限点、可选资源、通道、入口（`http` / `grpc` / `command`）与目标（路由模板、gRPC 方法或能力 id）；返回 `AuthorizationDecision`（id、与 Explain 同源的解释、是否旁路、是否已记录）。

执行顺序与 fail-closed 规则：主体非法、委托（act）或 Agent → 拒绝（`invalid_subject` / `delegation_not_supported`）；主体带租户而宿主没有为本次调用建立租户上下文 → 抛 `InvalidOperationException`（PEP 放在了可信租户边界之前，是装配错误不是判定）；主体租户与上下文不一致 → `subject_tenant_mismatch`；平台主体落在租户上下文里 → `tenant_context_mismatch`；权限点未声明 → `permission_not_declared`；不是 R9 全名 → `invalid_permission`；平台主体（tenant_id=null、无租户上下文）→ 显式旁路（未配置 → `platform_subject_requires_bypass`）；租户主体 → identity_revision 比对 → 判定；判定期间租户上下文被改 → `tenant_context_changed`。通道、入口、目标或资源不在约定内是调用方编程错误，抛 `ArgumentException`。

### 决策审计

每次判定经 `IAuditService` 写进当前租户的哈希链（平台主体的拒绝写平台链，平台链未配置时只能记日志）：能力 ID `qian.authz/decision`、契约版本 1.0.0，actor 为被判定的主体，channel 为请求通道，resource 为请求资源，result 为 `allowed` / `denied`，输入哈希只盖 `[permission, resource, entryPoint, target, channel]`，payload 为 `{ permission, entryPoint, target, resource, decision, reason, matchedRoles, matchedGrants, missing, matchedPolicies, identityRevision, bypass }`（与解释同源，不含输入原文）。拒绝必记；放行按配置 `decisionLogSampling`（0–1，缺省 1）采样。Audit 未装配 → `audit_unavailable` 拒绝，三处入口都把它映射为 404（审计组件已卸载时整个入口不可用，与不存在同为 404，S1-AUD-001 口径）；审计写入失败 → 放行变为拒绝（`decision_log_failed`），拒绝保留原因但 `Logged=false`。参考宿主从配置 `Authz:DecisionLogSampling` 读采样（缺省 1）；CRM 测试里早于本任务、按命令审计数断言链长的用例以 0 启动（拒绝仍必记），PEP 验收显式传 1。超管旁路放行自带 `qian.authz/superadmin.bypass` 审计，不再重复记录。一次 HTTP 请求若再进入命令管道，HTTP PEP 与命令 PEP 各记一条。命令管道的放行决策随命令的可串行化事务提交（`EnforcementRequest.Transaction`，经 `AppendEnlistedAsync`）：命令自己的审计条目在同一事务里接在它后面，链上不会出现该事务快照之外的条目；拒绝会让命令事务回滚，所以拒绝独立写入。每次判定后 emit `authz/decided`；`authz/checking` 是 waterfall，其它组件可在链上观察或包裹判定（只在 global scope 登记可见）。

### identity_revision 服务端比对

令牌只携带 `identity_revision`（`Principal.IdentityRevision`）。本组件按（租户，主体种类，主体）在表 `subject_identity_revisions` 记住见过的最高修订：携带更低修订的令牌即旧令牌，以 `identity_revision_stale` 拒绝并记录；未携带修订的令牌（S2S）不比对；负数拒绝。下限每次判定读库（主键查找、按租户 RLS），不进 Kun.Caching：内存后端不跨实例，靠 TTL 失效会让旧令牌在别的实例上多活 10 分钟，违反 R7 的 3 秒口径。判定快照键的第四段并入 identity_revision（R103）。身份组件在 `authz.grant.changed.v1` 上递增 identity_revision 并推送客户端换发令牌（PRD 7.4 / 7.13）属 Identity；本组件保证的是：新修订一经出现，旧令牌在任一宿主立即失效；角色撤销经租户授权修订立即使旧快照失效（实测毫秒级，验收上限 3 秒）。

### 授权变更事件

`PostgresTenantRoleStore.Write` 有实际变更时在同一事务里：递增租户授权修订 → 修订行作为聚合根 Raise `AuthzChange` 领域事件 → 宿主登记的 `AddQianAuthzEvents` 处理器经 `IIntegrationEventBus` 把 `authz.grant.changed.v1`（subjectType、subjectId、roleCode、change：assigned / updated / revoked、authzRevision）与 `authz.role.changed.v1`（roleCode、change：created / grants_installed、pluginId、pluginVersion、authzRevision）写进 Outbox，提交即可见。信封：producer `qian.authz`，actor 为 service `qian.authz` 代操作者（命令管道内沿用当前信封作用域）。宿主须 `AddKunEventBus(r => r.RegisterAuthzEvents())`（Apply 时校验，缺则装载失败）与 `AddKunMediator(m => m.AddQianAuthzEvents())`（缺则写入在提交前失败、整笔回滚）。没有实际变更（重复开通、撤销不存在的绑定）不发事件、不递增修订。

### 两条授予守卫

- 租户专属权限点不可绑定到子作用域：S1 的权限点全部租户专属，`ITenantRoleGrant.BindAsync(RoleBinding)` 的 Scope 只能是 null 或当前租户本身；应用、Agent 会话或别的租户 → `AUTHZ_SCOPE_MISMATCH`（422，pointer `#/scope`，参数 scope），不落库、不递增修订。
- 防自锁：租户最后一个当前有效的 `tenant.owner` / `tenant.admin` 绑定不可撤销（`RevokeAsync`），也不可用有效期上限降级（`BindAsync` 的 ValidTo）→ `AUTHZ_LAST_ADMIN_LOCKOUT`（409，参数 role）；已过期或未生效的绑定不算数。S1-AZ-005 交付管理权限点后把「持有成员管理权限点的角色」并入判据。

### HTTP 与 gRPC 适配（Qian.Authz.AspNetCore）

`AddQianAuthzEnforcement(http => ctx, options)` 登记授权处理器与结果处理器（`ctx` 通常是 authz 组件按请求租户收窄的上下文）。端点以 `RequireQianPermission("<plugin>:<resource>:<action>")` 声明权限点（可多次，全部须放行；同时加 `RequiresPermissionAttribute` 元数据，OpenAPI 写出 `x-qian-permission`，并声明 401 / 403 的 ErrorDetail 响应）。`UseQianAuthzEnforcement()` 放在 `UseAuthorization` 之后：带 `PluginEndpointBinding` 却未声明权限点的端点在生产模式（`options.Production ?? IHostEnvironment.IsProduction()`）缺省拒绝（403 并记决策日志 `permission_not_declared`），开发模式记警告放行；`[AllowAnonymous]` 与显式标记 `PermissionEnforcedInHandlerAttribute(reason)`（能力目录、菜单、执行入口这类按调用者可见性在处理器内判定的平台表面）不在范围内，标记必须写明理由。gRPC：`AddGrpc(o => o.AddQianAuthzInterceptor())`，四种调用形态在进入 handler 或读取首帧前对方法上的 `[RequiresPermission]` 逐个判定，未声明按同样规则缺省拒绝；拒绝为 `PermissionDenied`（401 → `Unauthenticated`，404 → `NotFound`），trailer `qian-error-bin` 携带 ErrorDetail。主体只取自 `ICurrentUser.Principal`（须与 JWT 的 sub / tenant_id / principal_kind 一致，否则 401），租户由已认证宿主建立；目标为 `METHOD 路由模板` 或 gRPC 方法全名；拒绝响应带 `Cache-Control: private, no-store`。tenancy 运营端点（`PlatformHttpContract`）沿用 S1-TEN-006 的处理器内旁路 PEP，本任务未改接。

### 验收记录

Qian.Authz.Tests/EnforcementTests.cs（真实 PG、真实 Audit 链、真实 PluginLoader）：放行与拒绝各写一条决策日志且与 `ExplainDetailedAsync` 同源，七种边界一律拒绝并记录，无租户上下文抛错，采样 0 只省放行，卸载 Audit 后放行变 `audit_unavailable`；两宿主各自内存缓存下修订 2 出现后修订 1 的令牌在任一宿主立即拒绝、快照键带修订、撤销角色后 3 秒内 `no_matching_grant`；授权变更按修订号发出事件信封，漏装处理器时写入回滚；两条守卫经组件端口返回结构化错误且不落库；卸载 authz 时 `command/dispatching` 监听器、执行端口与错误码按登记逆序撤销、无残留、重装后只有一个监听器。Qian.Sample.Crm.Tests/AuthzEnforcementTests.cs（参考宿主，生产模式）：同一权限点 HTTP 200 / 403 与 gRPC 一元、服务端流 OK / PermissionDenied 一致，未声明的组件端点与 gRPC 方法缺省 403，每次判定进租户链；更高 identity_revision 出现后旧令牌 403，移除角色后持最新修订的令牌 3 秒内 403，`authz.grant.changed.v1` 进 Outbox；OpenAPI 中每个组件端点带 `x-qian-permission` 与 401 / 403 响应。
