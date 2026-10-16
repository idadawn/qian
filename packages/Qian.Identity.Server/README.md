# Qian.Identity.Server

身份 seam 的协议 Provider（S1-IDP-002），基于 OpenIddict 7.7.2；可嵌入独立 ASP.NET Core 宿主。它拥有 discovery、JWKS、授权码、token、userinfo、revocation、introspection 和 end_session 协议面及持久存储。账号验证来自宿主选定的 Cookie scheme，不从请求体、请求头或 query 接收主体。

## 组件与宿主装配

文件夹包含 Provider、HTTP 协议适配、EF 迁移与后通道投递器；没有密码表或登录页面。默认落在 identity profile，提供 Identity seam。IdentityServerPlugin 通过 ctx.Provide 发布宿主传入的 IIdentityService，并以 ctx.Effect 撤销协议门禁；卸载后协议路径为 404。后续账号 Provider 由 S1-IDP-003 提供，当前独立宿主可接自己的账号服务。

plugins/qian.identity.server 继续保留明确标记的配置骨架，尚不能把 qian add identity 当成已经具备完整账号登录的安装入口。独立宿主通过项目引用或包引用组合本包与 Qian.Data；平台账号、外壳及正式制品发布按各自任务完成后接入。组件清单不会把未交付的账号功能标为 Ready。

宿主装配顺序：

1. 注册 Kun 的 ITenantContext 与事件目录，经 AddQianData 注册 IdentityDbContext，使用 IdentityDbContext.Schema。身份数据处于独立数据库角色与 schema；逻辑 identity 对应遵循 Data 统一命名门禁的物理 plugin_qian_identity。在线角色必须 NOSUPERUSER / NOBYPASSRLS，且仅获此 schema 的 DML 权限。迁移由部署迁移任务先运行，在线副本不建表。
2. 建立受信账号 Cookie scheme，默认名称 qian.identity.signin；宿主账号流水线负责认证及该 Cookie 的有效性；多副本须共享受保护的 Cookie/DataProtection 密钥。协议测试使用仅存内存的独立密钥，不写入用户密钥目录。调用 AddQianIdentityServer(options, environment) 装配 OpenIddict。options 中的证书私钥与客户端密钥由宿主从密钥引用加载，不进入 profile、日志或仓库。
3. 构建 WebApplication 后先调用 UseQianIdentityServer，再 UseAuthentication，最后 MapQianIdentityServer。前者包含 Qian 错误映射、安全响应头与组件挂载门禁。协议面不经过要求已选租户的业务 TenantRequestBoundary。
4. 通过 PluginLoader 挂载 DataPlugin，再挂载 IdentityServerPlugin(accounts)，提供 IdentityDefinition.Key；identity 条目的 config 只接收 issuer，必须与宿主 options 的规范 issuer 完全一致。只允许 global scope；同一宿主不允许第二个身份协议实例。

账号 Cookie 的租户用户输入必须含唯一 sub、tenant_id、membership_id、identity_revision、sid，UUID 与修订会重新校验；允许 amr、acr、auth_time。其它 Cookie claim 不会复制到令牌。平台用户登录、多 Membership 选择与初始密码强制修改由 IDP-003 的统一流水线继续接入；本包的机器身份已支持显式 tenant_id=null 的平台服务。

## 协议与客户端边界

- 每环境一个规范 issuer；生产只接受 HTTPS。AllowLoopbackHttp 只有 Development 且 loopback issuer 时有效，不能用于生产反向代理。代理须正确恢复原始 HTTPS scheme。
- 授权码一律 PKCE S256；openid 请求必须有 nonce。access token 为 15 分钟，授权码为 5 分钟，refresh 为 14 天并逐次轮换。复用旧 refresh 会吊销同授权族。关闭实体缓存，跨副本从数据库读取当前吊销状态。
- 客户端从宿主可信注册配置持久化，注册过程用数据库锁协调副本。S1 只接受官方受信的 implicit-consent 客户端；grant、scope 与 redirect URI 仍由 OpenIddict 检查。resource 必须命中该客户端的精确白名单，刷新不能扩大原授权受众。服务主体 sub=client_id，tenant 与 plugin 来自注册配置。
- client_credentials 签发身份 access token，内部调用仍经 Kun.Gateway 的 S2sTokenIssuer 换成冻结的短期 S2S 契约；不把 15 分钟外部令牌直接当作 S2S。令牌不携带权限码；业务授权仍由 Authz 判断。
- 独立宿主可提供账号登录页面；当前支持默认交互和 prompt=none。明确要求重新认证、选择账号或同意时，账号流水线接入前返回 interaction_required，不能静默复用旧 Cookie。max_age 缺少可信 auth_time 或已过期时拒绝。
- OAuth/OIDC 协议错误由 OpenIddict 按协议输出；宿主自己的 CSRF 等错误使用 ErrorDetail。不把标准 token 端点错误改造成无法被 RP 解析的另一种格式。

## 登出、密钥与日志

GET /connect/logout 只展示确认表单；POST 必须通过 antiforgery 检查。会话对应的授权族吊销与后通道待投递记录在同一数据库事务提交，再撤销浏览器 Cookie。后台投递使用 FOR UPDATE SKIP LOCKED 协调副本，每秒检查，HTTP 最长 5 秒，失败逐步退避至 60 秒后继续重试。接收方按 issuer/client/sid 幂等吊销；重试会重新签发短期 logout_token。已从可信配置移除的客户端不会继续发送到旧地址。

后通道只向登记的精确 HTTPS 地址发送，禁止自动重定向；Development 的 loopback 接收器仅用于本机验收。通知包含 sub、sid、jti、事件与短期时间窗，不含 nonce；Kun 客户端可直接验签。Redis 会话索引、个人强制下线与权限修订传播由 IDP-004 接入，不能把协议族吊销当成该任务已经完成。

签名与加密均使用 RSA 证书，允许同时配置多张证书。轮换时先向所有副本配置新旧密钥的重叠集合，再启用新密钥；旧解密密钥须保留到相关授权码、refresh 等材料到期，旧公钥保留到依赖方缓存与令牌有效期结束。证书变更通过配置及进程重启生效，不宣称文件监听或自动轮换已交付。

OpenIddict 的低级日志及 ASP.NET Hosting 的请求日志可能包含令牌或 id_token_hint，因此宿主装配将这两个类别限制为 Warning 及以上；不要用更具体的类别设置放开它们。Qian 的业务审计不受此限制。投递异常只记录异常类型，不记录 sid、令牌、密钥或异常原文。

## 验收

真实 PostgreSQL、Kestrel 和 OpenIddict 的协议测试使用已发布 Kun 2026.10.14 客户端，覆盖 PKCE、nonce 失配、refresh 轮换、跨宿主整族吊销、机器身份、受众拒绝、撤销/内省、CSRF 与后通道 Kun 验签；全局 Trace 下检查协议日志无原始 sid、访问/身份/刷新令牌与客户端密钥。测试中的账号 Cookie 夹具只代表此前已认证的用户，身份服务本身是本包真实实现，不用外部 IdP 或手写 discovery/token 夹具替代。

CRM 联调用真实身份服务的机器令牌，经实际 Kun S2S 签发器换签，再调用现有 CRM 写能力，经过租户边界、RBAC、数据库事务与审计；本任务不宣称完整网关产品或集群部署完成。

## 模型体验

协议端点不是模型工具，不派生登录、刷新或密码相关 capability。IIdentityService 的 Person/Membership/会话业务方法由宿主账号 Provider 提供，契约存在不意味着本包代替它实现了账号管理。

## 已知限制与延期项

- 账号数据库、密码与租户选择、平台用户登录属于 IDP-003；本包只消费已认证 Cookie，不提供临时密码或伪登录入口。
- Redis 会话索引、并发上限、强制下线、身份修订及历史清理属于 IDP-004；后通道队列不替代会话中心。
- RP/外壳接入属于 IDP-005 与 yue 任务；连接器、短信/TOTP 属于对应 IDP 后续任务。
- identity profile 的完整宿主组装与 qian add 产品入口仍待上述账号组件接入；本任务没有发布 NuGet 制品或部署到集群。
