# Qian.Tenancy

租户主档目录与严格上下文传播 Provider；Definition 为 Qian.Tenancy.Abstractions。权威设计为 ADR-004、PRD §7.3；主档、开通、运营接口、状态、配置与配额分别按 S1-TEN-001/003/006/002/004 验收。

## 组件与表面

代码目录包含 Provider、租户入口边界、传播器、gRPC 适配器、主档上下文及内部持久化实现。TenancyPlugin 消费 Data、提供 Tenancy 服务键，目录挂在 global scope，供 Identity 等可信服务读取最小投影；不把联系方式、管理员、连接凭据或完整租户列表暴露给匿名调用方。物理 schema 遵循 Data 的插件命名规则，使用 plugin_qian_tenancy。

默认目标为 platform profile。当前尚未完成管理员维护、真实 Identity 联调和 qian add tenancy（包已随 0.1.0 发到 GitHub Packages，R93），因此 plugins/qian.tenancy 保留 x-skeleton，不能视为已安装的完整平台组件。主档目录先提供稳定端口，避免 Identity 登录前查目录依赖尚未创建的 Membership。

## 主档存储

租户 UUID 由服务端生成，code 为 1 至 64 位小写 ASCII 字母数字；name 非空且最多 200 字符。当前只允许 row 隔离，新建状态为 pending；不会在创建目录记录时冒充已完成管理员开户或激活。code 与 isolation 不在编辑 DTO 中，EF 持久化后也拒绝变更。唯一索引处理跨宿主同号竞争，过期编辑由 Data/Kun 的 xmin 并发令牌拒绝。

主档是租户控制面全局目录，不承载业务行。内部写入由平台运营服务与 HTTP 门面消费，运营入口须经过真实平台超管旁路与审计。tenant_admins 只保存 Membership UUID 指针，一租户一行，继承 Data 强制租户过滤与 RLS；Membership、密码和自然人内容归 Identity。管理员指针的开户编排属于 S1-TEN-003，不在目录写入时跨插件直写身份表。

## 上下文与传播

只复用 Kun.Core.MultiTenancy.ITenantContext，默认实现为已修复并正式发布的 Kun AsyncLocalTenantContext；Provider 不安装第二个上下文，也不改变宿主已有选择。

TenantRequestBoundary 在宿主完成令牌认证后使用 Kun 的 Production 解析器，只接受已认证身份中唯一的 tenant_id。Qian 再按冻结契约校验非空 UUID；缺失、重复、非法值、与现有范围冲突均返回 UNAUTHENTICATED，X-Tenant-Id 不参与解析。调用结束或异常时释放范围。平台运营主体没有租户，必须走独立的运营授权入口，不能由本边界回退为 default 租户。

TenantContextPropagator 从当前上下文写入 JSON 信封/作业参数或文本元数据。消费端显式传入从认证令牌或受保护调度记录获得的 authorizedTenantId，传播字段必须与之相同，且不得覆盖当前不同租户范围。该参数绝不能取自同一未受信消息。传播器本身不认证消息来源，不代替完整事件信封校验，也不允许缺失/null 平台消息进入租户业务入口；平台事件走独立通道。重复 tenant_id 保留并拒绝，不能先转字典而丢掉重复项。

TenantGrpcClientInterceptor 从当前上下文为四类 RPC 写入 tenant_id，不覆盖调用方已有的冲突或重复字段，也不修改可复用的原始 Metadata。TenantGrpcServerInterceptor 在宿主 JWT 验签后比较 claim 与元数据，再进入业务处理器；unary、服务端流、客户端流和双向流都使用同一边界。拒绝时返回 gRPC Unauthenticated，并在 qian-error-bin 二进制 trailer 中附带由 ErrorCatalog 生成的冻结 ErrorDetail JSON。宿主仍须配置认证和标准 HTTP 错误管道，适配器不自行签发令牌。

独立模式可以使用宿主实现的 ITenantContext，但必须支持可释放的 BeginScope；多租户入口没有 SingleTenant/default 隐式回退。

## 验证与剩余验收

使用共用 PostgreSQL 16 fixture 和受限应用角色验证真实目录写入、两宿主同号竞争、编辑冲突、租户过滤与原生 SQL RLS；通过真实 Data/Runtime/Tenancy 插件装载验证目录生命周期。HTTP 边界单元测试使用已认证 ClaimsPrincipal 测试宿主管道；另有真实 Kestrel、JwtBearer 验签与受 RLS 约束的应用角色集成测试：A 的令牌请求 B 的记录 id 与不存在的 id 一样返回 404，X-Tenant-Id 不能切换租户，无令牌、缺 tenant_id 或签名被篡改的请求在处理器之前被拒。两类测试都不代表真实 Qian Identity 的签发或管理员登录。JSON/元数据序列化往返验证通用传播逻辑；另外已用真实 Kestrel HTTP/2 与 gRPC 客户端验证四种 RPC、双租户并发、24 组非法入口和错误详情。测试令牌由临时 RSA 密钥签发并经 JwtBearer 完整验签，这是传输适配器集成测试，不代表已完成真实 Qian Identity 的签发/管理员登录验收。事件消费者和作业执行器尚未接通。

按裁决 R81，TEN-001 的验收收口为上述 HTTP 租户边界、gRPC 对端传播与「目标租户不经请求体」：运营后台页面归 TEN-005，管理员登录归 IDP-003，事件消费端回灌归 EVT-002，作业执行器归 SCH-002。HTTP 运营接口已由 S1-TEN-006 发布：Qian.Tenancy.AspNetCore.MapQianTenancy(source) 提供 GET/POST /api/tenancy/tenants、PUT /api/tenancy/tenants/{id}，source 返回 global qian.tenancy 的实际上下文；宿主须排除这组路由的 TenantRequestBoundary，并配置 JwtBearer、ICurrentUser 与 ErrorDetail 管道。平台主体经 ISuperadminBypass 放行后写平台审计链 bypass=superadmin；租户主体 403，无凭据 401。状态迁移、开户编排、租户设置分别归 TEN-002/003/004；不以目录基础实现关闭这些任务。

## 命令执行入口

TenancyPlugin 已在 SDK 的真实 command/dispatching 上登记租户策略，按已验证主体的租户 UUID 查询主档，只有 active/trial 可进入业务；pending/grace/suspended 和未知租户拒绝。此处不实施租户激活或到期调度，不代替尚未完成的生命周期和身份会话吊销。真实 CRM 命令已覆盖冻结拒绝及双租户并发。

能力目录可经 ITenantDirectory.GetByIdAsync 读取已验证主体的租户主档，复用命令策略的 active/trial 状态边界；该内部端口未增加匿名查询或管理写接口。

## 开通编排（S1-TEN-003）

TenancyPlugin 额外提供 `TenantProvisioning.Key` 的 `ITenantProvisioning`。`ProvisionAsync(CreateTenant, TenantAdministrator, actor)` 以租户号和不可变输入判定同一请求；不同输入返回 CONFLICT。可信运营面授权后可调用 `RetryAsync(id, actor)` 续跑，或 `GetStepsAsync` 读取诊断；这些内部端口只接受无租户上下文，不能直接映射无授权 HTTP。目录基础功能仍只依赖 Data；开通运行时必须能解析 Registry、Lifecycle 与 Authz 的授予端口，缺失即失败。

`tenant_provision_steps` 受租户过滤和 RLS 保护。步骤为主档（同事务保存输入和成功标记）、Kun Shared 开户器与 GUC/RLS 校验、逐组件 Lifecycle.ProvisionAsync、tenant/provisioned、Membership、管理员指针、tenant.owner 授予、通知。每步独立提交，同一租户由 PostgreSQL 会话锁串行化，进程退出后锁释放；成功步骤不再执行，running/failed 按稳定幂等键重试。组件级仍使用 Lifecycle 的独立步骤表。步骤原因只保存稳定码，不保存上游异常内容。

管理员输入含已有或待 Identity 确认的 Membership UUID 与主体 ID；指针存在不代表成员已能登录。Identity/Notify 通过 `TenantProvisioning.MembershipKey` / `NotificationKey` 的可选端口提供适配器，缺席时分别保存 skipped 和 identity_unavailable / notify_unavailable；后续续跑重新解析端口，仅补未成功步骤。初始授权通过 `Qian.Authz.Abstractions.ITenantRoleGrant`，不引用 Authz Provider 或跨插件写表。

真库用例验证第三步失败后的续跑、平台组件和参考应用的 lifecycle_runs、管理员指针与初始授权、缺席端口的 skipped，以及重复请求的幂等。

S1-DEC-023 经产品负责人批准：全局组件配置 `initializationCapabilities` 是初始化能力精确许可清单，缺省为空、不接受通配符，不能由请求体或租户 patch 增加。只有真实开通编排在派发 tenant/provisioned 时签发进程内短期凭据，绑定生产方上下文、目标租户、真实操作者和能力清单；SDK 的实际 hook 适配器只消费一次，每次调用再绑定请求类型与权限点。普通 Invoke/Send 不签发凭据；嵌套重入、重复消费、越租户及派发结束后逃逸的异步任务均拒绝。只接受 read/write 能力，拒绝 destructive/external。业务入口继续拒绝 pending，开户初始化通过专用凭据判定，既有 Kun 权限行为、command/dispatching、同事务审计仍执行，审计 channel=hook、actor 为真实平台操作者。该执行快照不是登录令牌或 Membership，不写入 Identity。

## 平台运营 HTTP（S1-TEN-006）

POST 复用 CreateTenant，Administrator 为必填管理员指针，id 由服务端生成；调用真实开通编排，返回 201、pending 与当前 Revision。GET 返回按 code 排序的分页 items（skip 默认 0、take 默认 50、最多 100）。PUT 复用 UpdateTenant，只按路径目标写主档，过期 expectedRevision 返回 409 CONFLICT，未知目标 404 NOT_FOUND；tenantId、tenant_id、id 及不可编辑的 code/isolation 字段均在反序列化时拒绝为 400 BAD_REQUEST。运营 DTO 不包含联系方式或数据库凭据。

权限目录位于 plugins/qian.tenancy/metadata/permissions.json，Authz 必须实际登记该目录且配置专用平台登录，Audit 必须配置独立平台链角色。运营端点使用 PlatformHttpContract 标注构建契约归属，不走只接受租户的 PluginEnabledMiddleware；每次请求仍核对实际 qian.tenancy 上下文与超管旁路。IHttpContractSource 从实际端点生成 contracts/http-v1.openapi.json，经真实 oasdiff 与 OpenApiContractPlugin 准入；该构建描述不会出现在租户可见契约聚合里。TEN-005 页面与 S1-GW-001 探针可消费上述已发布接口，尚不表示相关任务已验收。

## 状态机与到期扫描（S1-TEN-002）

pending 可激活为 trial/active；有效期到期由扫描迁移到 grace；suspend 记录当前 S1 状态，resume 恢复原状态。重复同目标操作不重写状态或再发集成事件；非法迁移返回 409 TENANCY_STATE_CONFLICT。POST /api/tenancy/tenants/{id}:activate、:suspend、:resume 与 GET /api/tenancy/meta/state-machine 复用运营超管旁路。审批使用 TenantLifecycle.Approving 的进程内 serial 事件，无监听器放行、任一 false 否决，不加入 hooks.json 矩阵。监听器不得递归请求同一租户迁移。purge 只在元数据保留 S3 的 destructive + humanOnly 描述，不发布执行入口；humanOnly 按 R52 是 agent.expose=false。

主档创建、激活、冻结、恢复、到期分别在同一数据库事务发出 tenant.created/activated/suspended/resumed/expired.v1；宿主必须在唯一的 Kun 事件登记与 Mediator 登记中调用 RegisterTenancyEvents、AddQianTenancyEvents，并装配 AddQianEvents。缺少领域事件处理器时保存回滚，不会留无事件的状态。tenant.created.v1 与开通的进程内 tenant/provisioned 分工不变。

qian.tenancy/tenant.scan-expired 是真实运行时能力（非 humanOnly、幂等），只接受 global qian.tenancy 或 qian.scheduler 的可信调用，输入为 idempotencyKey 与 limit（1–1000，默认100），拒绝请求体租户字段；扫描平台审计以 job 通道记录服务操作者。每次只处理 active/trial 且 valid_until 已过的租户；重复扫描不新增状态事件。到期业务命令立即403，能力目录按现有过滤语义返回空集合。调度器准点触发、登录与会话吊销仍归 SCH-002、IDP-003/004。

## 设置读模型与配额（S1-TEN-004）

ITenantSettings.Get<T>(pluginId, key) 返回 ValueTask，读取当前可信租户的实际 Registry 合成配置；key 支持点或冒号路径。租户配置是 scope_patches 的 config 整体替换，不建立 TenantSetting 表、不逐字段混入旧租户配置。没有覆盖时读取全局条目；缺失键经 Config seam 按 pluginId:key 回退平台配置源（可由 Apollo 提供），仍缺失返回 default。缓存键含租户前缀、最长1秒且有容量上限；真实 registry/entry-mounted 事件立即失效当前宿主缓存，跨宿主通过数据库监听与有界缓存收敛，源宿主退出不影响持久值。

GET/PUT /api/tenancy/tenants/{id}/settings/{plugin} 使用同一平台超管授权与旁路审计。PUT 仅接受 scopes 含 tenant 且已挂载的唯一组件条目，复用 IRegistryService.MountAsync：同一 configSchema、JSON Pointer 错误、expectedRevision 并发与 registry.scope.mounted.v1 事务；应用成功后追加 settings.write 成功审计，只有配置哈希、组件、条目和修订，不记录配置原文。读取与注册表授权管理视图中的 config 来自同一合成器。发布 OpenAPI 对 JsonElement 的布尔 Schema 做等价对象展开，以兼容锁定的读取器，default/examples 的实例值保持原样。

主档 quotas 保存 TenantQuotas（users=null 为无限额），CreateTenant/UpdateTenant 支持设置，GET /api/tenancy/tenants/{id}/quota 返回 limits、usersUsed、usageAvailable。Identity 未提供 TenantSettings.UserCounterKey 的 ITenantUserCounter 时，usersUsed=null、usageAvailable=false，不报告成零。ITenantQuota.EnsureCanAddUserAsync 接受服务端统计与已验证成员类别，普通成员和租户管理员计入，平台超管不计；满额复用既有 AuthzErrors.CardinalityUserLimit 原对象（422 CARDINALITY_USER_LIMIT，参数 limit），不重定义全局错误码。计数锁、成员写入同事务与登录消费分别归 IDP-003/006。

真实验收使用两个独立宿主、两个 Runtime 和受限 PG 角色，写实际 Audit 组件配置后另一宿主在5秒内读到，并与 GET /api/registry/scopes/{scopeKey}/entries 的 config 相同；也验证非法值指针、过期修订、global-only 组件拒绝、Config 默认值回退与配额规则。Identity 尚缺，因此用真实可挂载组件验证通用配置链路，不伪造 Identity、登录或成员统计。完整租户可用六步见 TEN-004 的测试与任务日志。
