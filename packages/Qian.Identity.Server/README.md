# Qian.Identity.Server

身份 seam 的 Provider：基于 OpenIddict 7.7.2 的协议面（S1-IDP-002）加内置的 Person / Membership 账号模型与统一登录流水线（S1-IDP-003）。可嵌入独立 ASP.NET Core 宿主，拥有 discovery、JWKS、授权码、token、userinfo、revocation、introspection 和 end_session 协议面，以及账号、凭据、会话与登录审计的持久存储。OpenIddict 是唯一的令牌发行者；Kun 只提供 OIDC 依赖方与 Cookie 原语，不持有业务用户与租户。

## 两种装配方式

- **内置账号模型（默认）**：`new IdentityServerPlugin()`，需要 Data 与 Tenancy 两个组件。身份组件自己提供 `IIdentityAccounts`（账号管理）、`ISignInPipeline`（统一登录流水线）、`IAuthenticatorFactorRegistry`（认证因子注册表），并向 Tenancy 提供 `ITenantMembershipProvisioner`（开通编排第 5 步）与 `ITenantUserCounter`（配额用量）。
- **宿主自带账号服务**：`new IdentityServerPlugin(accounts)`，本组件只承担协议面，账号流水线由宿主负责；此时不提供上面几个端口。

两种方式都只允许 global scope、每环境唯一 issuer；`plugins/qian.identity.server` 仍保留明确标记的配置骨架，`qian add identity` 的完整产品入口待外壳与会话中心接入后再发布。

## 宿主装配顺序（内置账号模型）

1. 注册 Kun 的 `ITenantContext`，`AddKunEventBus(r => r.RegisterIdentityEvents())`、`AddKunMediator(m => m.AddQianIdentityEvents())`、`AddQianEvents()`；登录、锁定、改密事件在账号写入的同一事务里入 Outbox，漏装时保存回滚，不会静默丢事件。
2. 经 `AddQianData` 注册 `IdentityDbContext`（schema `plugin_qian_identity`）。在线角色必须 NOSUPERUSER / NOBYPASSRLS，仅获该 schema 的 DML 权限；迁移由部署迁移任务先运行，在线副本不建表。
3. 建立受信账号 Cookie scheme（默认名 `qian.identity.signin`）和 DataProtection；多副本须共享受保护的密钥。调用 `AddQianIdentityServer(options, environment)`。`options` 里的证书私钥与客户端密钥由宿主从密钥引用加载，不进 profile、日志或仓库。账号策略放在 `options.Accounts`（见下）。
4. 构建应用后先 `UseQianIdentityServer()`，再 `UseAuthentication()`，最后 `MapQianIdentityServer()` 与 `MapQianIdentityAccounts()`。前者含 Qian 错误映射、安全响应头（no-store / nosniff / frame-ancestors none）与组件挂载门禁，卸载后账号接口与协议路径都是 404。
5. 经 `PluginLoader` 依次挂载 Data、Tenancy，再挂载 `IdentityServerPlugin`；identity 条目的 config 只接收 `issuer`，必须与 `options` 的规范 issuer 一致。

**与业务宿主同机混合部署**：账号接口在 `/api/identity` 下，协议面在 `/connect`、`/.well-known` 下，登录前没有租户上下文。业务宿主若对 `/api/*` 统一施加「必须已选租户」的边界（Kun 多租户中间件、生产模式拒绝回退租户），须把这三组路由排除在外（单独分支或独立宿主）；CRM 联调测试用 `MapWhen` 分支演示了做法。

## 账号模型

全局表（无租户列，不适用 RLS）：`persons`、`person_identifiers`、`password_history`、`clients`、`sessions`、`login_audits`、`login_tickets`、`login_captchas`。租户表（`tenant_id` 列，强制 RLS，只能在对应租户上下文中读写）：`memberships`、`external_identities`。

- **Person** 是全局自然人，`Id` 即令牌的 `sub`，创建后永不改写；换绑标识不改 `sub`。`sub` 用 UUID v7 生成，主键冲突时换号重试，绝不覆盖既有账号；并发创建同一标识只有一个成功。
- **标识**：手机（规范化为 E.164，缺省国家码取 `DefaultPhoneCountryCode`）、邮箱（小写）、用户名（字母开头 3–32 位，不会形似手机或邮箱，所以按语法即可判定类型，无需用户选择）。同一标识同一时刻只属于一个有效 Person（唯一索引加软删除，解绑后释放）。
- **绑定守卫**：解绑最后一个已验证标识、或让账号没有任何标识，返回结构化的 `LAST_VERIFIED_IDENTIFIER`（422）；解绑外部身份若这是账号仅剩的登录方式，返回 `LAST_LOGIN_METHOD`。对外投影只含掩码，手机、邮箱原值不出身份组件。
- **凭据**只存 Argon2id（纯托管实现，PHC 串，成本可调）；密码先 NFKC 规范化，比较常量时间，参数越界或格式残缺的哈希一律视为不匹配；历史保留最近 N 条哈希（含当前），新密码不得与其中任何一条相同；成本参数调高后旧哈希在下一次成功登录时顺带升级。
- **Membership**（person × tenant）：显示名、`is_tenant_administrator`、状态、`profile` 预留（S1 不启用）、成员级修订；租户内账号唯一且创建后不可改。`persons.tenant_ids` 是登录前用的租户索引（登录时无租户上下文，靠它逐租户进入作用域读取），只由建立成员的同一事务维护，不是授权依据。
- **ExternalIdentity** 绑定：同租户 connector × subject 唯一，须显式确认，软删除；连接器登录本身由 IDP-007 接入。
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

写接口要求 JSON、防伪令牌、16 KB 体积上限（声明长度超限直接 `Connection: close` 返回 413，不读取请求体）、未知字段拒绝。成功登录写入服务端账号 Cookie（只来自流水线，从不接受请求体里的主体），随后 `/connect/authorize` 据此签发授权码。错误是 `ErrorDetail`；`423 ACCOUNT_LOCKED` 带 `Retry-After`。

## 登录策略与参数

`IdentityServerOptions.Accounts`（`IdentityAccountOptions`）：Argon2id 成本（缺省 19 MiB、2 次迭代、1 并行度）、密码策略（最小长度 ≥ 8、历史条数 1–24）、登录策略（失败阈值 ≥ 3、锁定时长、延时起点与上限、失败计数窗口、验证码模式、选择令牌寿命 ≤ 5 分钟、改密令牌寿命）、会话策略（绝对寿命，缺省 30 天）。选项校验拒绝放宽这些安全底线。租户级覆盖（登录策略与密码策略 Schema，经 `ITenantSettings`）由 IDP-006 接入，在此之前全平台共用这里的取值。

## 协议与客户端边界

- 每环境一个规范 issuer；生产只接受 HTTPS。`AllowLoopbackHttp` 只有 Development 且 loopback issuer 时有效，不能用于生产反向代理。代理须正确恢复原始 HTTPS scheme。
- 授权码一律 PKCE S256；openid 请求必须有 nonce。access token 为 15 分钟，授权码为 5 分钟，refresh 为 14 天并逐次轮换。复用旧 refresh 会吊销同授权族。关闭实体缓存，跨副本从数据库读取当前吊销状态。
- 客户端从宿主可信注册配置持久化，注册过程用数据库锁协调副本。S1 只接受官方受信的 implicit-consent 客户端；grant、scope 与 redirect URI 仍由 OpenIddict 检查。resource 必须命中该客户端的精确白名单，刷新不能扩大原授权受众。服务主体 sub=client_id，tenant 与 plugin 来自注册配置。
- client_credentials 签发身份 access token，内部调用仍经 Kun.Gateway 的 S2sTokenIssuer 换成冻结的短期 S2S 契约；不把 15 分钟外部令牌直接当作 S2S。令牌不携带权限码；业务授权仍由 Authz 判断。
- 默认交互和 prompt=none 受支持。明确要求重新认证、选择账号或同意时返回 interaction_required，不能静默复用旧 Cookie。max_age 缺少可信 auth_time 或已过期时拒绝。
- OAuth/OIDC 协议错误由 OpenIddict 按协议输出；宿主自己的 CSRF 等错误使用 ErrorDetail。

## 登出、密钥与日志

GET /connect/logout 只展示确认表单；POST 必须通过 antiforgery 检查。会话对应的授权族吊销、账号会话行吊销（原因 `logout`）与后通道待投递记录在同一数据库事务提交，再撤销浏览器 Cookie。后台投递使用 FOR UPDATE SKIP LOCKED 协调副本，每秒检查，HTTP 最长 5 秒，失败逐步退避至 60 秒后继续重试。接收方按 issuer/client/sid 幂等吊销；重试会重新签发短期 logout_token。已从可信配置移除的客户端不会继续发送到旧地址。后通道只向登记的精确 HTTPS 地址发送，禁止自动重定向；Development 的 loopback 接收器仅用于本机验收。Redis 会话索引、个人强制下线与权限修订传播由 IDP-004 接入，不能把协议族吊销当成该任务已经完成。

签名与加密均使用 RSA 证书，允许同时配置多张证书。轮换时先向所有副本配置新旧密钥的重叠集合，再启用新密钥；旧解密密钥须保留到相关授权码、refresh 等材料到期，旧公钥保留到依赖方缓存与令牌有效期结束。证书变更通过配置及进程重启生效，不宣称文件监听或自动轮换已交付。

OpenIddict 的低级日志及 ASP.NET Hosting 的请求日志可能包含令牌或 id_token_hint，因此宿主装配将这两个类别限制为 Warning 及以上；不要用更具体的类别设置放开它们。账号服务的日志与审计不含密码、验证码、票据、原始标识与原始 sid。

## 错误码

`IdentityErrors` 在挂载时登记到 `ErrorCatalog`，卸载即撤销：`INVALID_CREDENTIALS` 401、`ACCOUNT_LOCKED` 423（可重试）、`ACCOUNT_DISABLED` 403、`LOGIN_TICKET_INVALID` 401、`TENANT_NOT_ACTIVE` 403、`NO_AVAILABLE_TENANT` 403、`CAPTCHA_REQUIRED` 428、`CAPTCHA_INVALID` 400、`MFA_UNAVAILABLE` 503；业务类 `PASSWORD_POLICY_VIOLATION`（参数 `rule`）、`PASSWORD_REUSED`（参数 `count`）、`CURRENT_PASSWORD_INVALID`、`IDENTIFIER_IN_USE` 409、`LAST_VERIFIED_IDENTIFIER` 422、`LAST_LOGIN_METHOD` 422、`ACCOUNT_IN_USE` 409；配额满额复用 Tenancy 的 `CARDINALITY_USER_LIMIT`（422，参数 `limit`）。

## 对其他任务登记的接口

- **S1-TEN-003**：`ITenantMembershipProvisioner`（服务键 `TenantProvisioning.MembershipKey`）按 (成员 ID, Person) 幂等建立租户管理员 Membership；`TenantAdministrator.SubjectId` 即 Person 的 `sub`，必须是已存在的 Person，也是开通第 7 步授予 `tenant.owner` 的主体。组件就位后第 5 步由 `skipped` 变为 `succeeded`。
- **S1-TEN-004**：成员创建经 `ITenantQuota.EnsureCanAddUserAsync(currentUsers, kind)`，在同一事务内持租户级计数锁；租户管理员计入，具备平台资格的 Person 不计；`ITenantUserCounter`（`TenantSettings.UserCounterKey`）给出真实人数。
- **S1-TEN-010**（R110）：`IIdentityAccounts.ResetPasswordAsync` 是运营重置密码的端口——旧凭据立即失效、默认下次登录强制改密、使既有会话失效、发布不含密码的改密事件。
- **S1-IDP-004**：`sessions` 表、`SessionRevocation` 的 PG 层吊销与 `identity_revision` 已就位；Redis 索引、强制下线事件、后通道扩展由 IDP-004 接入。
- **S1-IDP-006**：`IAuthenticatorFactorRegistry` 与 `MFA_UNAVAILABLE` 的不降级行为；租户级登录策略覆盖。
- **S1-IDP-007**：连接器登录复用同一 `LoginInput`；在此之前 `ConnectorId` 非空明确拒绝（`#/connector_id`），不会悄悄降级。

## 验收

真实 PostgreSQL（受限应用角色、强制 RLS）、Kestrel、OpenIddict 与已发布 Kun 2026.10.14 客户端：协议面（PKCE、nonce、refresh 轮换与跨宿主整族吊销、机器身份、受众、撤销与内省、CSRF 与后通道验签）；账号模型（并发创建不碰撞、绑定守卫的结构化错误、历史不可复用、状态与修订、外部身份）；登录流水线（审计、限流与并发、强制改密、选择与切换、租户状态、平台身份、MFA 不降级、其他因子、验证码、会话有效性）；账号 HTTP 面端到端（登录 → 选择 → 授权码 → 令牌 → 切换 → 旧刷新被拒）。CRM 联调把本组件挂进真实的 Tenancy 宿主：开通编排第 5 步、用户配额 5 的第 6 个成员被拒、真实租户目录对 pending 租户拒绝登录，以及租户管理员登录后只看到本租户数据、以 A 的令牌请求 B 的记录是 404。

## 模型体验

协议与账号端点不是模型工具，不派生登录、刷新或密码相关 capability；模型不会由本组件获得签发、重置密码或登录能力。

## 已知限制与延期项

- MFA 的登记、挑战与校验（TOTP、短信）属于 IDP-006；此前已登记第二因子的账号一律拒绝登录，没有降级路径。
- Redis 会话索引、并发上限、单端踢出、强制下线事件、身份修订的跨服务传播与历史清理属于 IDP-004；本组件只做 PG 层会话与吊销。
- 连接器登录、JIT 建号与合并属于 IDP-007；RP/外壳接入属于 IDP-005 与 yue 任务。
- 登录失败的 IP 维度限流与设备指纹不在本任务内（账号维度的限流、锁定与验证码已交付）。
- identity profile 的完整宿主组装与 `qian add` 产品入口仍待上述组件接入；本任务没有发布 NuGet 制品或部署到集群。
