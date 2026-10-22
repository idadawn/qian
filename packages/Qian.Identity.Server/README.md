# Qian.Identity.Server

身份 seam 的 Provider：基于 OpenIddict 7.7.2 的协议面（S1-IDP-002）加内置的 Person / Membership 账号模型与统一登录流水线（S1-IDP-003）、会话中心（S1-IDP-004）与连接器登录（S1-IDP-007：统一回调、确认绑定、JIT）。可嵌入独立 ASP.NET Core 宿主，拥有 discovery、JWKS、授权码、token、userinfo、revocation、introspection 和 end_session 协议面，以及账号、凭据、会话与登录审计的持久存储。OpenIddict 是唯一的令牌发行者；Kun 只提供 OIDC 依赖方与 Cookie 原语，不持有业务用户与租户。

## 两种装配方式

- **内置账号模型（默认）**：`new IdentityServerPlugin()`，需要 Data 与 Tenancy 两个组件。身份组件自己提供 `IIdentityAccounts`（账号管理）、`ISignInPipeline`（统一登录流水线）、`IAuthenticatorFactorRegistry`（认证因子注册表），并向 Tenancy 提供 `ITenantMembershipProvisioner`（开通编排第 5 步）与 `ITenantUserCounter`（配额用量）。
- **宿主自带账号服务**：`new IdentityServerPlugin(accounts)`，本组件只承担协议面，账号流水线由宿主负责；此时不提供上面几个端口。

两种方式都只允许 global scope、每环境唯一 issuer；`plugins/qian.identity.server` 仍保留明确标记的配置骨架，`qian add identity` 的完整产品入口待外壳接入后再发布。

## 宿主装配顺序（内置账号模型）

1. 注册 Kun 的 `ITenantContext`，`AddKunEventBus(r => r.RegisterIdentityEvents())`、`AddKunMediator(m => m.AddQianIdentityEvents())`、`AddQianEvents()`；登录、锁定、改密事件在账号写入的同一事务里入 Outbox，漏装时保存回滚，不会静默丢事件。
2. 经 `AddQianData` 注册 `IdentityDbContext`（schema `plugin_qian_identity`）。在线角色必须 NOSUPERUSER / NOBYPASSRLS，仅获该 schema 的 DML 权限；迁移由部署迁移任务先运行，在线副本不建表。
3. 建立受信账号 Cookie scheme（默认名 `qian.identity.signin`）和 DataProtection；多副本须共享受保护的密钥。调用 `AddQianIdentityServer(options, environment)`。`options` 里的证书私钥与客户端密钥由宿主从密钥引用加载，不进 profile、日志或仓库。账号策略放在 `options.Accounts`（见下）。
4. 构建应用后先 `UseQianIdentityServer()`，再 `UseAuthentication()`，最后 `MapQianIdentityServer()` 与 `MapQianIdentityAccounts()`。前者含 Qian 错误映射、安全响应头（no-store / nosniff / frame-ancestors none）与组件挂载门禁，卸载后账号接口与协议路径都是 404。
5. 经 `PluginLoader` 依次挂载 Data、Tenancy，再挂载 `IdentityServerPlugin`；identity 条目的 config 只接收 `issuer`，必须与 `options` 的规范 issuer 一致。
6. 可选：注册 StackExchange.Redis 的 `IConnectionMultiplexer`，会话中心就启用 Redis 索引与吊销墓碑；不注册则完全由 PostgreSQL 承担，行为一致（见「会话中心」）。事件总线配置里再调用 `events.AddQianIdentitySessionHandlers()`，登记 `tenant.suspended.v1` / `tenant.expired.v1` 的处理器。
7. 可选：在 identity 之后挂载连接器组件（如 [Qian.Identity.Connector.Oidc](../Qian.Identity.Connector.Oidc/README.md) 的 `OidcConnectorPlugin`），全局或某个租户的 scope 都可以；它们在自己的上下文里登记到 `Identity.Connectors`，见「连接器登录」。

**与业务宿主同机混合部署**：账号接口在 `/api/identity` 下，协议面在 `/connect`、`/.well-known` 下，登录前没有租户上下文。业务宿主若对 `/api/*` 统一施加「必须已选租户」的边界（Kun 多租户中间件、生产模式拒绝回退租户），须把这三组路由排除在外（单独分支或独立宿主）；CRM 联调测试用 `MapWhen` 分支演示了做法。

## 账号模型

全局表（不适用 RLS；其中 `sessions`、`login_audits`、`login_tickets`、`connector_flows` 只带可空的 `tenant_id` 作记录）：`persons`、`person_identifiers`、`password_history`、`clients`、`sessions`、`login_audits`、`login_tickets`、`login_captchas`、`connector_flows`。租户表（`tenant_id` 列，强制 RLS，只能在对应租户上下文中读写）：`memberships`、`external_identities`。

- **Person** 是全局自然人，`Id` 即令牌的 `sub`，创建后永不改写；换绑标识不改 `sub`。`sub` 用 UUID v7 生成，主键冲突时换号重试，绝不覆盖既有账号；并发创建同一标识只有一个成功。
- **标识**：手机（规范化为 E.164，缺省国家码取 `DefaultPhoneCountryCode`）、邮箱（小写）、用户名（字母开头 3–32 位，不会形似手机或邮箱，所以按语法即可判定类型，无需用户选择）。同一标识同一时刻只属于一个有效 Person（唯一索引加软删除，解绑后释放）。
- **绑定守卫**：解绑最后一个已验证标识、或让账号没有任何标识，返回结构化的 `LAST_VERIFIED_IDENTIFIER`（422）；解绑外部身份若这是账号仅剩的登录方式，返回 `LAST_LOGIN_METHOD`。对外投影只含掩码，手机、邮箱原值不出身份组件。
- **凭据**只存 Argon2id（纯托管实现，PHC 串，成本可调）；密码先 NFKC 规范化，比较常量时间，参数越界或格式残缺的哈希一律视为不匹配；历史保留最近 N 条哈希（含当前），新密码不得与其中任何一条相同；成本参数调高后旧哈希在下一次成功登录时顺带升级。
- **Membership**（person × tenant）：显示名、`is_tenant_administrator`、状态、`profile` 预留（S1 不启用）、成员级修订；租户内账号唯一且创建后不可改。`persons.tenant_ids` 是登录前用的租户索引（登录时无租户上下文，靠它逐租户进入作用域读取），只由建立成员的同一事务维护，不是授权依据。
- **ExternalIdentity** 绑定：同租户 connector × subject 唯一，须显式确认，软删除；连接器登录与确认绑定见下文「连接器登录」。
- **Client** 是宿主可信注册配置的镜像（类型 confidential / public / machine、端类型 `DeviceType`），OpenIddict 应用仍是协议侧事实来源。
- **身份修订**：令牌里的 `identity_revision` = Person 修订 + 成员修订，二者只增不减；改密、停用、锁定、标识变更、成员状态变更都会递增，使旧令牌与旧会话失效。

## 统一登录流水线

所有登录方式构造同一个 `LoginInput`，走同一条「验证码 → 账号查找 → 限流占位 → 因子验证 → 状态检查 → 身份选择 → 会话签发 → 审计」路径；`FactorId` 缺省为 `password`，其他因子（短信、TOTP）由各自组件经 `IAuthenticatorFactorRegistry` 注册后复用同一条限流、审计与签发路径（同名独占，释放句柄交给 `ctx.Effect`）。

- **一次调用恰好一条 `LoginAudit`**：成功、失败、需要选择、需要改密都是一条；标识只记类型与掩码，sid 只记哈希，不记凭据、验证码或令牌。账号不存在、密码错误、标识格式非法对外是同一个 `INVALID_CREDENTIALS`；账号状态（停用、租户不可登录、无可用身份、MFA 不可用）只在凭据验证通过之后才暴露。账号不存在时也做一次等价成本的哈希，响应时间不泄露账号是否存在。
- **限流占位在先、验证在后**：每次验证前在行锁内先占用一次尝试，所以并发猜测不会在计数落库前各自验证一遍；达到阈值那次尝试起账号进入锁定（`Lock`，固定时长）或累进延时（`Delay`，起始延时每越线一次翻倍，封顶）；锁定期间连正确密码也不验证，`Retry-After` 给出剩余秒数。阈值那次尝试若密码输对，账号不被锁定，失败计数清零，不发锁定事件；`identity.locked.v1` 只在越过阈值且验证确认失败之后发一次。管理员置为 `locked` 的账号只能由管理员解锁。
- **强制改密**：初始密码（或运营重置，默认）账号登录后得到 5 分钟的一次性改密票据，改密成功前没有任何会话；改密令牌不能当选择令牌用，弱密码与复用被拒时票据保留可重试；改密成功后继续同一登录流程（多个身份仍会进入租户选择）。
- **租户选择与切换**：可选身份只含此刻可登录的 Membership（成员 `active`，租户状态 `trial` / `active`；`pending` / `grace` / `suspended` 的租户被拒，全部不可用返回 `TENANT_NOT_ACTIVE`，没有任何成员返回 `NO_AVAILABLE_TENANT`）。多个身份发 5 分钟一次性 `selection_token`（库里只存 SHA-256，兑换在建立会话的同一事务内消费，选择集合以签发时确认的为准，兑换时逐项重新校验）。登录时可指定 `tenant_id` 只在该租户内选。具备平台资格的 Person 另有平台身份（令牌 `tenant_id` 为 JSON null、无 `membership_id`，不计租户配额）。在外壳切换租户 = 以当前会话换一枚新的选择令牌再兑换：新会话沿用最初的认证时刻、方式与绝对寿命，旧会话在同一事务内吊销（原因 `switch`），不提升认证强度。
- **第二因子不降级**：已登记第二因子的账号一律返回 `MFA_UNAVAILABLE`，在认证因子任务（IDP-006）把挑战与校验接入流水线之前绝不退化成单因子。
- **图形验证码**按策略开关：`Off` / `Always`（先于账号查找校验）/ `AfterFailures`（该账号近期连续失败达到次数后要求）。缺失是 `CAPTCHA_REQUIRED`（428），错误或过期是 `CAPTCHA_INVALID`（400），都不消耗密码尝试；挑战是 2 分钟、单次有效（答对答错都消耗）的 SVG，库里只存加盐哈希。它只挡最廉价的脚本，不是反机器人的终点。
- **会话有效性**每次授权前对照数据库：会话行未吊销且未过绝对寿命，Person 与成员 `active`，租户 `trial` / `active`，平台身份仍具平台资格；协议端点在 authorize、换码、refresh、登出时都校验，任何一项失效都不再签发，旧 refresh 返回 `invalid_grant`。

令牌 claim：`sub / tenant_id / membership_id / scope / identity_revision / sid / amr / acr`（后两者按需），不含权限；`principal_kind` 为 `user`。授权码与 refresh token 的序列化会把平台身份的 JSON null `tenant_id` 退化成空串，兑换时由协议端点恢复。

## 会话中心

会话的唯一事实来源是 PostgreSQL 的 `sessions` 表；Redis 索引只是派生数据，任何时候都不据它放行。

- **吊销分两段**：① 即时生效——改密、停用、锁定、成员停用、租户冻结、强制下线、登出、并发挤下线，都与引起它的变更同事务写 `sessions.revoked_at / revoke_reason / revoked_by`；此后账号 Cookie、授权码、换码、refresh 与 userinfo 的校验都读库，立刻拒绝。② 旁路——后台处理器 `IdentitySessionProcessor` 对每个「已吊销、未处理」的会话收尾（进程内的吊销写入后经信号立即唤醒，另有 1 秒轮询兜底）：吊销协议授权族与令牌（与登出端点共用 `IdentityProtocolSessions`）、为登记了后通道登出地址且确实持有该会话授权的客户端登记投递、清除 Redis 索引并写墓碑，最后在会话所属租户的作用域内把集成事件与 `revocation_processed_at` 标记写进同一事务。旁路每一步幂等，失败按指数退避重试且不挡住后面的会话；多副本用 `FOR UPDATE SKIP LOCKED` 分担，同一会话的事件恰好一条——登出是 `identity.logout.v1`，其余是 `identity.session_revoked.v1`，信封的行为主体是动手的人或服务（后台触发的是 `service:qian.identity`），租户是会话所属租户。
- **吊销的来源与原因**：改密（`password_changed`，含运营重置 `password_reset`）、Person 停用 / 管理员锁定（`disabled` / `locked`）、成员停用 / 锁定（`member_disabled` / `member_locked`，只吊销该成员身份下的会话）、租户冻结 / 到期 / 目录里消失（`tenant_*`）、强制下线（`admin_revoke`、`admin_revoke_all`、本人 `user_revoke`、`user_revoke_all`）、登出（`logout`）、切换租户（`switch`）、并发上限与单端（`concurrency_limit`、`single_device`）。登录限流产生的临时锁定**不**吊销已有会话——否则任何人故意输错密码就能把受害者踢下线。权限变更不吊销会话，而是递增身份修订（见「身份修订」）。
- **后通道登出（双向）**：身份服务 → 依赖方（RP）：任何吊销来源（不只是登出）都向「登记了后通道登出地址、且持有该会话授权」的客户端投递 `logout_token`（持久队列、逐步退避重试、接收方按 issuer / client / sid 幂等）；依赖方 → 身份服务：RP 经 `end_session` 登出，身份服务吊销该会话后向其余持有授权的 RP 扇出。RP 侧的接收与验签用 Kun 的 `ValidateLogoutTokenAsync`（测试里用的就是已发布的 Kun 客户端），外壳 RP 的接入属于 IDP-005。
- **Redis 索引（可选）**：`qian:identity:sessions:{person}` 是有序集合（成员为 `N` 格式的会话标识，分值为绝对到期的 Unix 秒）；`qian:identity:revoked:{sid}` 是吊销墓碑，只在 PG 已提交吊销之后才写、TTL 到会话自然到期，所以「有墓碑 ⇒ 已吊销」恒成立，校验先看墓碑可省一次查询，没有墓碑不代表有效。会话列表读 PG，并顺带以 PG 为准修复索引（补缺、去多）。Redis 的任何故障都吞掉并退回 PG，同时熔断 5 秒，只记一条不含标识的降级告警。
- **并发上限与单端模式**：`SessionPolicy.MaxConcurrentSessions`（缺省 5，跨租户跨端合计）；新登录在建会话的同一事务里（已锁定 Person 行，同一人的并发登录因此串行，上限不会被击穿）踢掉最早建立的会话，原因 `concurrency_limit`。开启 `SingleSessionPerDeviceType` 后，同一端类型（客户端登记的 `DeviceType`，如 pc、mobile）后登录的会话踢掉先登录的，原因 `single_device`，不影响其他端；并发上限仍然适用。
- **租户冻结 / 到期（R95）**：每次会话校验都读租户目录，租户不再是 Active / Trial，会话立即失效（账号接口 401，refresh 返回 `invalid_grant`）。失效的持久化与事件有两条路径：事件处理器 `TenantSuspendedSessionHandler` / `TenantExpiredSessionHandler`（订阅 `tenant.suspended.v1` / `tenant.expired.v1`，到达即吊销该租户的全部会话，原因 `tenant_suspended` / `tenant_expired`）；以及后台对账（每 `ReconcileInterval` 一次，缺省且至多 30 秒：核对仍有有效会话的租户在目录里的状态，非 Active / Trial 或目录里已不存在的租户，其会话一律吊销，原因 `tenant_<状态>` / `tenant_missing`）。事件投递由消费端（S1-EVT-002）接线；即使事件丢失、迟到或尚未接线，对账也保证会话在一个对账间隔加旁路时间内失效，远小于 60 秒。租户恢复后已吊销的会话不会复活，用户重新登录即可。
- **身份修订**：`IIdentityAccounts.BumpIdentityRevisionAsync(personId, reason)` 递增该 Person 在**当前租户**的成员修订（行级安全把更新限在当前租户，同一 Person 在别的租户的成员身份不变），返回新的 `identity_revision`（Person 修订 + 成员修订）；该租户没有这个成员返回 `NOT_FOUND`。同进程宿主里，授权组件在授予的增、改、撤提交后直接调用它，并用返回值抬高 PEP 的修订下限（S1-AZ-009，S1-DEC-027）；拆分部署（platform / identity 两个 Pod）由本组件订阅 `authz.grant.changed.v1` 后调用，随 S1-EVT-002 交付（S1-IDP-012）。旧令牌随即被服务端拒绝：userinfo 与会话校验比较令牌里的 `identity_revision` 与库里的当前值，更低就是 `invalid_token`；会话本身不吊销，refresh 换到的新令牌带新修订。网关须把 `identity_revision` 放进 `ForwardedClaimTypes` 白名单，供 7.5 的 PEP 比对缓存的权限快照。
- **管理面**（见下表）：本人管自己的会话；平台身份（平台资格 + 平台会话，租户身份下不继承）管任何人，可按租户缩小；租户管理员只管本租户内的会话（传别的租户的参数也越不了权）。无权限时单个会话按不存在（404）处理，不泄露它是否存在，列表类 403。这是授权引擎的权限点（`qian.identity:session:*`）接入 PEP 之前的内置判定，接入后改为权限点加数据范围。
- **在线用户**：窗口内（`OnlineWindow`，缺省 15 分钟，即访问令牌寿命）有活动的有效会话，按 Person × 租户聚合；平台视角含平台身份会话。活动指会话校验（授权、换码、刷新、userinfo、账号接口），`last_seen_at` 最多每分钟刷新一次。
- **组件卸载**：后台工作（旁路、对账）互斥运行，卸载时先排空再放行 Data 释放会话；`ctx.Effect` 按登记的逆序执行，所以服务先于 Data 关闭，不会让正在执行的命令撞上被释放的连接。

## 连接器登录

上游身份源（企业 IdP、Authentik、企业微信……）以连接器组件接入：连接器在自己的 Apply 里 `ctx.IdentityConnectors().Register(name, connector)`，登记成运行时注册表的具名服务键 `Identity.Connectors:<name>`（R38），卸载即撤销。连接器只做「跳转 → 拿回外部断言」，其余都在这里（PRD 7.4.2、ADR-005 D5.6 / D5.11）。

- **可见性按租户 scope**：发起时必须给租户号（绑定键是 tenant × connector × subject），一律在该租户的 scope 里解析连接器：租户 scope 的同名注册遮蔽全局项，restriction 过滤掉的、没挂载的与不存在的同样是 404，响应不可区分；列表接口按同一规则只列可见的。
- **发起**：建一条 `connector_flows` 流程（10 分钟），库里只存 state 与浏览器密钥的 SHA-256。浏览器密钥放在 `qian.identity.browser` Cookie（HttpOnly、SameSite=Lax，同一浏览器的流程共用）；PKCE `code_verifier` 由浏览器密钥与流程号 HMAC 派生，库里没有，偷到授权码也换不出令牌。连接器拿到 `ConnectorContext`（state、nonce、verifier、回调地址），回调地址取自受信的 issuer，不取请求的 Host 头。连接器调用在流程租户的上下文里执行（租户 scope 挂载的连接器解析 `secretRef://tenant/…` 时按它核对），限时 30 秒，与请求是否断开无关。
- **统一回调 `/connect/callback/{connectorId}`**：state 未知、属于别的连接器、或不是发起它的浏览器（含同名参数重复出现）一律 422 `AUTH_CONNECTOR_STATE_INVALID`，流程不受影响；发起之后连接器被卸载或过滤则 404。回调幂等：只有 pending 且未过期的流程会被条件更新抢占为 processing 并交给连接器，重放与并发的同一回调只回到落点，连接器只被调用一次，不签第二个会话。上游带 `error` 时记 `AUTH_CONNECTOR_DENIED`；`error_description` 是不可信数据，去掉控制字符、截断到 200 字，只作技术详情，文案一律取错误码目录。连接器校验断言失败时：nonce 不符 `AUTH_CONNECTOR_STATE_INVALID`，上游响应无效或签名不符 `BAD_GATEWAY`，超时 `GATEWAY_TIMEOUT`。处理完 302 到界面落点 `LandingPath?flow=<流程号>`（缺省 `/login/connector/{connectorId}`），界面按流程的 `next`（`wait` / `bind` / `done` / `error` / `restart`）驱动下一步；处理中的流程过了时限再一分钟仍未结束（进程中途退出），读取时按 `GATEWAY_TIMEOUT` 失败呈现，界面不会一直等待。每次回调恰好一条登录审计（stage `connector`）。
- **已绑定**直接签发会话；**未绑定**进入确认绑定（绑定票据 10 分钟，过期后任何一步都是 `AUTH_BINDING_EXPIRED`）。界面只拿到上游资料的掩码（名称、邮箱、用户名），原值只在服务端。第一步在流程的租户里登录已有账号：新鲜的一因子认证，与密码登录同一条验证码、限流与审计，且必须是该租户的 active 成员。第二步显式确认：`link` 须在认证后 5 分钟内（否则 401 `UNAUTHENTICATED`，重新认证即可），在同一事务里写入 ExternalIdentity；该外部身份在本租户已绑到别的账号时返回 409 `AUTH_BINDING_CONFLICT`，不透露对方是谁。`create` 是 JIT 建号，`cancel` 作废流程。流程先被抢占再落库，重复确认不会签出第二个会话。
- **JIT 建号缺省关闭**：只有本组件在该租户的设置 `connectors:jitProvisioning` 为 true 时才允许（经 `ITenantSettings` 读取；没装租户设置、未配置或读取失败一律视为关闭）。先过租户用户配额（满额 `CARDINALITY_USER_LIMIT`），再以上游邮箱（未验证）为标识建 Person；没有邮箱就用由绑定键派生的用户名 `ext-<20 位十六进制>`，不设密码；然后建成员并绑定。
- **会话签发走统一流水线**：绑定已存在或刚确认后，服务端发一张 1 分钟、一次性的连接器票据（`login_tickets.purpose = connector`），再以 `ConnectorId` + 票据构造同一个 `LoginInput` 走 `SignInAsync`，审计、单端与并发策略、登录事件与密码登录完全一致；外部拿不到这张票据，伪造的一律 `LOGIN_TICKET_INVALID`。会话 `amr` 含 `fed`（确认绑定时前面还有那次认证用的因子，如 `pwd`）。连接器登录不要求修改初始密码；已登记第二因子的账号与密码登录一样 `MFA_UNAVAILABLE`，不降级。
- **参数** `IdentityAccountOptions.Connectors`（`ConnectorPolicy`）：流程寿命与绑定票据寿命（缺省都是 10 分钟，取值 1 分钟–1 小时）、重新认证窗口（缺省 5 分钟，不超过绑定票据寿命）、界面落点（站内路径，须含 `{connectorId}`）。

## HTTP 接口

`MapQianIdentityAccounts()` 映射（均在 `/api/identity` 下，响应一律 no-store）：

| 方法与路径 | 作用 |
| --- | --- |
| `GET /csrf` | 取防伪令牌（响应给出请求头名与令牌）。令牌绑定当前登录身份，**登录、切换、改密之后要重新取** |
| `GET /captcha` | 签发验证码挑战（`id`、`image` 为 data URL、`expires_at`）；策略为 Off 时 404 |
| `POST /login` | `client_id`、`identifier`、`password`、`tenant_id?`、`captcha?` → `authenticated` / `selection_required` / `password_change_required` |
| `POST /login/select` | `ticket`、`membership_id?`（空表示平台身份）→ `authenticated` |
| `POST /login/password` | `ticket`、`new_password` → 改密并继续登录流程 |
| `POST /session/switch` | 已登录用户发起租户切换 → `selection_required` |
| `GET /me` | 当前会话的身份投影（按数据库校验，失效为 401） |
| `POST /me/password` | `current_password`、`new_password`；成功后该 Person 的全部会话失效，Cookie 清除，204 |
| `GET /sessions?person=&tenant_id=` | 会话列表，缺省本人，当前会话带 `current`；平台可看任何人并按租户缩小，租户管理员只看本租户内的会话 |
| `POST /sessions/{sid}:revoke` | 吊销一个会话，返回 `{revoked}`；无权限或不存在一律 404；吊销的是当前会话时同时清除 Cookie |
| `POST /persons/{id}:revoke-all` | 吊销某人的全部会话（平台可加 `tenant_id` 缩小范围，租户管理员固定在本租户）；本人吊销连当前会话一并吊销并清除 Cookie |
| `GET /online-users?tenant_id=&limit=&offset=` | 在线用户（平台视角可按租户过滤，租户管理员固定在本租户，普通成员 403）；`limit` 1–200，缺省 50 |
| `GET /connectors?tenant=` | 该租户可见的连接器 `{items: [{id, display_name, protocol, terminals}]}`；租户号未知或缺省时列全局项 |
| `GET /connectors/{id}?tenant=` | 连接器详情即清单：`protocol`、`terminals`、`config_schema`、`callback_template` 与本环境的 `callback_uri`（供在上游登记）；不含任何配置值，密钥与 secretRef 都不回传 |
| `POST /connectors/{id}/begin` | `client_id`、`tenant`、`return_url?`（只接受站内路径）→ `{flow, redirect_uri, expires_at}`，浏览器随后跳往 `redirect_uri` |
| `GET /connector-flows/{flow}` | 界面落点读取流程：`status`、`next`、连接器与租户、`binding`（上游资料掩码、`allow_jit`、`verified` 与已认证的账号）、`error`（ErrorDetail）、`upstream_error_description`、完成后的 `return_url`；只认发起它的浏览器，否则 404 |
| `POST /connector-flows/{flow}/signin` | 确认绑定第一步：`identifier`、`password`、`captcha?` → 更新后的流程，不建立会话 |
| `POST /connector-flows/{flow}/bind` | 确认绑定第二步：`mode` 为 `link` / `create` / `cancel` → `authenticated`（写账号 Cookie，带 `return_url`）或 204 |

统一回调 `GET /connect/callback/{connectorId}` 不在 `/api/identity` 下（处理后 302，见上文「连接器登录」）。写接口要求 JSON、防伪令牌、16 KB 体积上限（声明长度超限直接 `Connection: close` 返回 413，不读取请求体）、未知字段拒绝；两个吊销接口没有请求体，只要求防伪令牌。成功登录写入服务端账号 Cookie（只来自流水线，从不接受请求体里的主体），随后 `/connect/authorize` 据此签发授权码。错误是 `ErrorDetail`；`423 ACCOUNT_LOCKED` 带 `Retry-After`。

## 登录策略与参数

`IdentityServerOptions.Accounts`（`IdentityAccountOptions`）：Argon2id 成本（缺省 19 MiB、2 次迭代、1 并行度）、密码策略（最小长度 ≥ 8、历史条数 1–24）、登录策略（失败阈值 ≥ 3、锁定时长、延时起点与上限、失败计数窗口、验证码模式、选择令牌寿命 ≤ 5 分钟、改密令牌寿命）、会话策略（绝对寿命缺省 30 天；并发上限 1–1000，缺省 5；单端模式缺省关闭；租户对账间隔 1–30 秒，缺省 30 秒；在线窗口 1 分钟–1 天，缺省 15 分钟）。选项校验拒绝放宽这些安全底线。租户级覆盖（登录策略与密码策略 Schema，经 `ITenantSettings`）由 IDP-006 接入，在此之前全平台共用这里的取值。

## 协议与客户端边界

- 每环境一个规范 issuer；生产只接受 HTTPS。`AllowLoopbackHttp` 只有 Development 且 loopback issuer 时有效，不能用于生产反向代理。代理须正确恢复原始 HTTPS scheme。
- 授权码一律 PKCE S256；openid 请求必须有 nonce。access token 为 15 分钟，授权码为 5 分钟，refresh 为 14 天并逐次轮换。复用旧 refresh 会吊销同授权族。关闭实体缓存，跨副本从数据库读取当前吊销状态。
- 客户端从宿主可信注册配置持久化，注册过程用数据库锁协调副本。S1 只接受官方受信的 implicit-consent 客户端；grant、scope 与 redirect URI 仍由 OpenIddict 检查。resource 必须命中该客户端的精确白名单，刷新不能扩大原授权受众。服务主体 sub=client_id，tenant 与 plugin 来自注册配置。
- client_credentials 签发身份 access token，内部调用仍经 Kun.Gateway 的 S2sTokenIssuer 换成冻结的短期 S2S 契约；不把 15 分钟外部令牌直接当作 S2S。令牌不携带权限码；业务授权仍由 Authz 判断。
- 默认交互和 prompt=none 受支持。明确要求重新认证、选择账号或同意时返回 interaction_required，不能静默复用旧 Cookie。max_age 缺少可信 auth_time 或已过期时拒绝。
- OAuth/OIDC 协议错误由 OpenIddict 按协议输出；宿主自己的 CSRF 等错误使用 ErrorDetail。

## 登出、密钥与日志

GET /connect/logout 只展示确认表单；POST 必须通过 antiforgery 检查。会话对应的授权族吊销、账号会话行吊销（原因 `logout`）与后通道待投递记录在同一数据库事务提交，再撤销浏览器 Cookie。后台投递使用 FOR UPDATE SKIP LOCKED 协调副本，每秒检查，HTTP 最长 5 秒，失败逐步退避至 60 秒后继续重试。接收方按 issuer/client/sid 幂等吊销；重试会重新签发短期 logout_token。已从可信配置移除的客户端不会继续发送到旧地址。后通道只向登记的精确 HTTPS 地址发送，禁止自动重定向；Development 的 loopback 接收器仅用于本机验收。登出与会话中心的旁路共用同一处协议收尾（`IdentityProtocolSessions`），登出发的是 `identity.logout.v1`；Redis 索引、强制下线、并发控制与修订见上文「会话中心」。

签名与加密均使用 RSA 证书，允许同时配置多张证书。轮换时先向所有副本配置新旧密钥的重叠集合，再启用新密钥；旧解密密钥须保留到相关授权码、refresh 等材料到期，旧公钥保留到依赖方缓存与令牌有效期结束。证书变更通过配置及进程重启生效，不宣称文件监听或自动轮换已交付。

OpenIddict 的低级日志及 ASP.NET Hosting 的请求日志可能包含令牌或 id_token_hint，因此宿主装配将这两个类别限制为 Warning 及以上；不要用更具体的类别设置放开它们。账号服务的日志与审计不含密码、验证码、票据、原始标识与原始 sid：全部日志点都是 `[LoggerMessage]`，参数与模板里没有会话标识（测试用源码扫描加反射把关），登录审计只留 sid 的哈希。`POST /sessions/{sid}:revoke` 的路径含会话标识：宿主的 ASP.NET 请求日志已限制在 Warning，但网关与入口控制器的访问日志不在本组件控制范围，部署须对该路径脱敏。

## 错误码

`IdentityErrors` 在挂载时登记到 `ErrorCatalog`，卸载即撤销：`INVALID_CREDENTIALS` 401、`ACCOUNT_LOCKED` 423（可重试）、`ACCOUNT_DISABLED` 403、`LOGIN_TICKET_INVALID` 401、`TENANT_NOT_ACTIVE` 403、`NO_AVAILABLE_TENANT` 403、`CAPTCHA_REQUIRED` 428、`CAPTCHA_INVALID` 400、`MFA_UNAVAILABLE` 503；业务类 `PASSWORD_POLICY_VIOLATION`（参数 `rule`）、`PASSWORD_REUSED`（参数 `count`）、`CURRENT_PASSWORD_INVALID`、`IDENTIFIER_IN_USE` 409、`LAST_VERIFIED_IDENTIFIER` 422、`LAST_LOGIN_METHOD` 422、`ACCOUNT_IN_USE` 409；连接器登录（业务类）`AUTH_CONNECTOR_STATE_INVALID` 422、`AUTH_CONNECTOR_DENIED` 422、`AUTH_BINDING_CONFLICT` 409、`AUTH_BINDING_EXPIRED` 422，这四个码沿用认证页族规格的 `AUTH_` 前缀，与上面不带前缀的登录码命名不同；配额满额复用 Tenancy 的 `CARDINALITY_USER_LIMIT`（422，参数 `limit`）。这些码都归本组件（owner `qian.identity`），不进平台内置目录 `contracts/v0/error-codes.json`；连接器四个码的导出样例在 `contracts/v0/fixtures/error-catalog.valid.json`，测试比对防漂移。

## 对其他任务登记的接口

- **S1-TEN-003**：`ITenantMembershipProvisioner`（服务键 `TenantProvisioning.MembershipKey`）按 (成员 ID, Person) 幂等建立租户管理员 Membership；`TenantAdministrator.SubjectId` 即 Person 的 `sub`，必须是已存在的 Person，也是开通第 7 步授予 `tenant.owner` 的主体。组件就位后第 5 步由 `skipped` 变为 `succeeded`。
- **S1-TEN-004**：成员创建经 `ITenantQuota.EnsureCanAddUserAsync(currentUsers, kind)`，在同一事务内持租户级计数锁；租户管理员计入，具备平台资格的 Person 不计；`ITenantUserCounter`（`TenantSettings.UserCounterKey`）给出真实人数。
- **S1-TEN-010**（R110）：`IIdentityAccounts.ResetPasswordAsync` 是运营重置密码的端口——旧凭据立即失效、默认下次登录强制改密、使既有会话失效、发布不含密码的改密事件。
- **S1-IDP-004**（本任务）：会话中心已就位，见上文「会话中心」。对其他任务登记的接口见下面三条。
- **授权任务（S1-AZ-004 / S1-AZ-009）**：同进程宿主里，`qian.authz` 在授予变更提交后，对受影响的 user 主体在其租户作用域内调用 `IIdentityAccounts.BumpIdentityRevisionAsync`。主体不是 GUID 或返回 `NOT_FOUND` 时跳过。之后用返回值抬高 PEP 的修订下限。PEP 比对令牌 `identity_revision` 与缓存快照由授权侧负责，陈旧令牌回 401。拆分部署下，本组件对 `authz.grant.changed.v1` 的订阅处理器由 S1-IDP-012 交付；同进程宿主不登记它。
- **S1-EVT-002（事件消费端）**：宿主在事件总线配置里调用 `AddQianIdentitySessionHandlers()`；订阅投递由消费端接线，在此之前租户冻结 / 到期靠后台对账兜底。
- **S1-GW-001（网关）**：`ForwardedClaimTypes` 白名单须包含 `identity_revision`。
- **S1-IDP-006**：`IAuthenticatorFactorRegistry` 与 `MFA_UNAVAILABLE` 的不降级行为；租户级登录策略覆盖。
- **S1-IDP-007**（本任务）：连接器登录已就位，见上文「连接器登录」。`LoginInput.ConnectorId` 只接受服务端自己签发的连接器票据；该租户看不见的连接器仍是 `#/connector_id`。
- **S1-IDP-008（企业微信）**：实现 `IIdentityConnector`（工作台免登另实现 `IImplicitIdentityConnector`），在自己的 Apply 里 `ctx.IdentityConnectors().Register("wecom", …)`；回调、绑定与会话签发都复用这里，不另起回调路由。
- **yue / 外壳（S1-YUE-002 等）**：登录页按 `GET /connectors?tenant=` 渲染连接器按钮；界面落点路由 `/login/connector/{connectorId}?flow=` 读流程并按 `next` 驱动确认绑定页。
- **S1-IDP-006**：JIT 的租户策略目前读本组件在租户设置里的 `connectors:jitProvisioning`；租户级设置 Schema 与运营界面随 IDP-006 一起接入。

## 验收

真实 PostgreSQL（受限应用角色、强制 RLS）、真实 Redis、Kestrel、OpenIddict 与已发布 Kun 2026.10.14 客户端：协议面（PKCE、nonce、refresh 轮换与跨宿主整族吊销、机器身份、受众、撤销与内省、CSRF 与后通道验签）；账号模型（并发创建不碰撞、绑定守卫的结构化错误、历史不可复用、状态与修订、外部身份）；登录流水线（审计、限流与并发、强制改密、选择与切换、租户状态、平台身份、MFA 不降级、其他因子、验证码、会话有效性）；账号 HTTP 面端到端（登录 → 选择 → 授权码 → 令牌 → 切换 → 旧刷新被拒）；会话中心（改密 / 强制下线 / 登出后即时失效与旁路送达、并发上限含并发登录、单端模式、修订递增与旧令牌被拒、租户冻结的处理器与对账、Redis 索引的登记 / 墓碑 / 修复 / 宕机降级、管理面三种视角、两个副本竞争同一批吊销时事件恰好一条、日志卫生）；连接器登录（`ConnectorFlowTests`：假连接器 TCK 的清单校验、state 篡改拒绝、回调幂等含并发，确认绑定、冲突不泄露、过期与重新认证窗口、取消、JIT 与配额、租户 scope 过滤与遮蔽、详情不回传密钥；`AuthentikConnectorTests`：容器里的真实 Authentik 经通用 OIDC 连接器走完首次绑定、再次登录与同租户重复绑定被拒）。CRM 联调把本组件挂进真实的 Tenancy 宿主：开通编排第 5 步、用户配额 5 的第 6 个成员被拒、真实租户目录对 pending 租户拒绝登录，以及租户管理员登录后只看到本租户数据、以 A 的令牌请求 B 的记录是 404，以及真实 Tenancy 状态机冻结租户后该租户会话立即失效、refresh 被拒、对账持久吊销并发出事件，恢复后可重新登录。

## 模型体验

协议与账号端点不是模型工具，不派生登录、刷新或密码相关 capability；模型不会由本组件获得签发、重置密码或登录能力。

## 已知限制与延期项

- MFA 的登记、挑战与校验（TOTP、短信）属于 IDP-006；此前已登记第二因子的账号一律拒绝登录，没有降级路径。
- 已吊销 / 已过期会话行的历史清理（保留期与归档）不在本任务内；订阅投递的接线（S1-EVT-002）未交付，接线前租户冻结靠对账兜底；授权变更触发修订递增在同进程宿主已由 `qian.authz` 直接调用（S1-AZ-009），拆分部署的订阅处理器归 S1-IDP-012。
- 管理面的权限判定是内置规则（本人 / 平台身份 / 租户管理员），待授权引擎的 PEP 接入后改为权限点加数据范围。
- 账号合并不在 S1。JIT 建号是先建 Person、再建成员并绑定的几步操作（Person 与成员分属全局与租户作用域）；配额已预先检查，但若建成员时并发撞上配额或数据库出错，会留下一个只带上游邮箱标识的 Person，需要运营清理。一个成员在同一连接器下可以绑定多个外部身份，是否限制为一个待产品裁决。RP/外壳接入属于 IDP-005 与 yue 任务。
- 连接器流程表的过期行在每次发起时顺带清理（结束超过一天的），没有独立的后台清理任务。
- 登录失败的 IP 维度限流与设备指纹不在本任务内（账号维度的限流、锁定与验证码已交付）。
- identity profile 的完整宿主组装与 `qian add` 产品入口仍待上述组件接入；本任务没有发布 NuGet 制品或部署到集群。
