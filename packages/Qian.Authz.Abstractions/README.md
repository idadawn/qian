# Qian.Authz.Abstractions

Authz seam 的 Definition，承接 ADR-006、ADR-019 与 S1-AZ-001。文件夹包含权限元数据、服务键、Kun 端口引用、错误码与命令注册桥接，不包含授权数据库或 RBAC 求值引擎。

## 装配与组件边界

所有 profile 均可引用 Definition；真正提供 `Authz` 的组件是 `Qian.Authz` / `qian.authz`。`ctx.Authz()` 按当前 scope 解析 `IAuthzService`，缺 Provider 或上下文已卸载时拒绝解析。Provider 必须在 Apply 中提供服务，并将错误码登记句柄交给 `ctx.Effect`。

`qian add authz` 安装组件及其 Provider，本 Definition 包不自行启动服务，也不把平台能力绑定到宿主 DI。当前组件目录仍是骨架；Provider `Qian.Authz` 的 `AuthzPlugin` 已提供 `Authz`、`Authz.Permissions`、`qian.authz.resource-type/v1` 与 `Authz.SuperadminBypass` 四个服务键及命令 PEP，并在 `tenant/provisioned` 时实例化默认角色（S1-AZ-002 / S1-AZ-003），细节见 Qian.Authz 主文档。

## 平台超管显式旁路

`ISuperadminBypass`（服务键 `AuthzDefinition.SuperadminBypassKey`）是平台超管的唯一放行端口（ADR-006 D6.3）。调用方以平台主体（`AuthorizationSubject.TenantId` 为 null）显式请求，普通 Check 对平台主体一律拒绝；`SuperadminAccess` 携带权限点、目标租户（平台资源为 null，须与当前租户上下文一致）、可选资源引用与调用通道（ui / api / mcp / a2a / job / event / hook）。Provider 只在主体持有效 `platform.superadmin`（`PlatformRoles.Superadmin`）且审计写入成功后放行，审计 payload 的 bypass 值为 `PlatformRoles.SuperadminBypass`（`superadmin`）；拒绝不写审计，只给稳定原因码。端口缺失时调用方必须 fail-closed，不得回退到租户判定器或令牌角色。

## 权限契约

`IAuthzService` 直接继承 `Kun.Authorization.IPermissionChecker`，复用 Check、BatchCheck、ListAllowedActions、ListPermissions、Explain 的原始类型；DataPermissions 与 Fields 分别使用原 `IDataPermissionFilter`、`IFieldPolicy`。另加 `ListEffectivePermissionsAsync`：返回主体经角色授权命中的每个权限点 `EffectiveGrant`（完整权限元数据、与 Check 一致的当前判定与原因），需要 step-up 或终端证明的权限点照样列出、判定为拒绝；供 `GET /api/authz/me/permissions` 使用。再加 `ExplainDetailedAsync`：返回 `AuthorizationExplanation`，判定与 Check 一致，外加命中角色、授权记录定位与缺少的权限点；继承来的 `ExplainAsync` 只给判定，Authz API 的 explain 与管理界面等要说明「为什么」的调用方用前者（S1-AZ-003）。不复制同名接口，不从令牌中的权限集合推导有效授权。S1 的数据与字段权限端口不等于 S2 求值能力已完成。

`PermissionCode.Format("qian.sample.crm", "customer", "read")` 产生 `qian.sample.crm:customer:read`。组件 id 至少含一个点，资源与动作是插件内短名；拒绝空值、大写、通配符、额外前缀和换行，不静默修正。`PermissionDefinition` 保留 resource/action 短名；Code 始终派生。风险词汇是 low/medium/high/critical，step-up 与终端列表是元数据，不能单凭客户端传来的终端或风险值放行。

## Mediator 注册边界

沿用 Kun 的逐命令 `PermissionBehavior`。受保护命令实现 `IPermissionProtectedRequest`，并用 `AddAuthorizedHandler<TRequest, TResponse, THandler>(denied)` 一次登记权限行为与处理器；带能力 ID 和源生成 JSON 元数据的重载遵守相同规则。这个桥接只委托 Kun，实际授权仍由运行时注入的 `IPermissionChecker` 求值，缺引擎、匿名主体或任一权限被拒均不会进入 handler。

编译门禁 QIAN0005 禁止对受保护请求直接调用或引用 Kun `AddRequestHandler` 的两个重载，也拦截请求类型未知的泛型包装器；只有本包的桥接实现可调用底层入口。普通未受保护命令和无关同名方法不受影响。门禁覆盖测试、生成代码及关闭 TreatWarningsAsErrors 的项目。它检查显式注册代码，不把反射或未纳入此构建的外部程序集宣称为已审计。

这里不实现 `qian.authz` 的完整入口 PEP、生命周期和决策审计；HTTP、gRPC、命令事件及授权变更失效仍由 S1-AZ-004 完成。

## 错误目录

`AuthzErrors.Denied` 是冻结 `PlatformErrors.AuthzDenied` 原对象的别名，保持 AUTHZ_DENIED 的既有 owner、403 与中英文文案。CONSTRAINT_MUTEX（409）、CARDINALITY_USER_LIMIT（422）、PARTIAL_SUCCESS（409）归属 qian.authz。

`AuthzErrors.Register(catalog)` 成组登记，失败会撤销已完成的登记；释放句柄幂等，默认目录中的 AUTHZ_DENIED 不会被误删。`catalog.ExportJson()` 导出多语言目录，`catalog.CreateDetail()` 生成统一 ErrorDetail；测试用冻结 JSON Schema 验证。PARTIAL_SUCCESS 只定义错误词汇，批量逐项结果属于具体业务能力输出，不能向封闭的 ErrorDetail 私加 items 字段。

## 框架依赖与模型体验

直接引用只有 Qian.Runtime 与 Kun.Authorization，全部 Kun 版本沿根目录 KunVersion。Mediator 桥接复用 Qian.Runtime 已有的 Kun.Mediator / Kun.Core 依赖，不引入 Registry、Lifecycle、数据库或 ASP.NET Provider。

模型可读取权限名称、风险、step-up 和错误词汇；Definition 不赋予模型管理授权或执行工具的能力。Check 等工具发布和完整平台的真实权限过滤仍有各自验收门。

## 验证

本包测试覆盖命名、JSON 元数据往返、真实运行时注入与卸载、错误目录回滚及 Schema、多语言文案；真实 Kun Mediator 分别验证两个注册重载、缺引擎、拒绝、放行和匿名主体。测试中的判定端口只用于证明框架装配，不代表 RBAC 引擎已实现。`tests/gates/authz` 用真实 Kun 包编译正反样例，避免只检查源码字样。

## 权限声明输入（S1-AZ-002）

`PermissionAttribute` 是独立宿主的声明输入，不是执行鉴权特性；与平台 metadata 一样由 Provider 编译为已有 `PermissionDefinition`。`PermissionCatalog` 与 `RoleTemplate` 是只读词汇，角色的 Patterns 为作者声明，Grants 为当前版本已展开的确切权限点。构造这些 DTO 不会登记权限、实例化租户角色或放行请求。字段错误 AUTHZ_METADATA_INVALID 由 Provider 放进 INPUT_INVALID.errors，沿用冻结 ErrorDetail。


`IPermissionRegistry` 是活动权限声明的可逆端口，`IResourceTypeRegistry` 对应资源类型扩展点；版本参数是确切组件版本。返回句柄由组件交给 Effect，登记与撤销均不直接修改主体角色。Provider 实现及租约、冲突和派生资源规则统一见 Qian.Authz 主文档。

`AuthorizationExplanation` 保留命中角色、授权定位与缺失权限，经 `IAuthzService.ExplainDetailedAsync` 返回；`IAuthzExecutionConstraints` 是可信身份和终端适配端口，不接受请求体布尔值作为 MFA 或终端证明。判定快照缓存与超管旁路审计已由 Qian.Authz 实现（S1-AZ-003）；组织 / 岗位 / 组目录主体尚未接入（S1-AZ-005）。
