# Qian.Gateway

网关组件以库方式组合 Kun.Gateway 的签发器、JWKS、热轮换、可信代理与限流契约，把外部访问令牌或 `qian.sid` 会话转换为目标服务的短时 S2S。真实宿主入口为 [Qian.Gateway.Host](../../hosts/Qian.Gateway.Host/README.md)。

## 组件与装配

- 组件文件夹是 `plugins/qian.gateway`：唯一清单、派生配置 Schema 与说明；没有模型工具、技能或 UI 表面。
- 默认进入 gateway profile。Definition 是 `Qian.Gateway.Abstractions` / `Gateway`，本包是 Provider，消费 `Identity` 与 `Config`。
- `GatewayComponent` 是唯一 `runtime.entry`，仅允许 global scope。宿主的网络配置与挂载条目必须一致；卸载即停止代理请求，撤销服务键，并令 `/readyz` 返回 503。
- 本包只依赖 Runtime、SDK、所需 Abstractions 与 Kun，不引用 Identity.Client、Data 或 Audit Provider；它们由宿主组合。
- `qian add gateway` 的正式私有源安装尚未验证，组件保留 `x-skeleton`。真实代码入口与可构建宿主不等于已完成发布或安装验收。

组件配置由 `ComponentConfiguration.Schema` 派生到唯一 Schema。`GatewayHosting.ReadConfiguration()` 严格拒绝重复成员、未知字段、缺失下游、带凭据的 URL、相对密钥路径与全网代理 CIDR。示例在 [gateway 组合包](../../plugins/qian.bundle.gateway/config/qian.patch.yml)。密钥路径指向宿主的 Secret 文件，永不把 PEM 内容写进配置。

## 固定安全边界

所有代理路由强制认证，不能通过路由配置设置 `Anonymous`。匿名可达的只有宿主明确映射的身份协议端点、JWKS、`/healthz`、`/readyz`，以及宿主选择装上的外壳静态托管（入口、带哈希资源、`/app-config.json`、`/remotes/{组件 id}/**`，见下文「外壳」）；bootstrap 聚合端点不经代理认证，但自己要求会话，未认证 401。无凭据和错误凭据在代理之前拒绝，错误使用 ErrorDetail。

外部 Bearer 只接收配置中唯一身份发行方的 `at+jwt`，验证签名、issuer、audience、寿命，拒绝重复载荷字段和 S2S 类型。Cookie 由 Identity.Client 读取真实 Redis 会话并验证访问令牌；网关不自行解析会话 Cookie。带显式 Authorization 的请求必须独立通过 Bearer 验证，不能在失败时退回已登录 Cookie。

统一主体经 Kun `S2sTokenIssuer` 签 RS256，固定 60 秒，每个 cluster 只有自己的 audience。用户的 OIDC `sid` 映射到 `session_id`，服务主体由签发器令 `session_id = jti`；不透明 Cookie 值不进入令牌。只保留冻结契约字段，任意附加声明不透传。转发前移除原 Authorization 与 Cookie，再写入新 S2S。

Kun 的默认代理变换使用固定 audience，Qian 在代理前先为实际路由签发并校验目标令牌，最后一个请求变换覆盖为该令牌；两次都使用同一个 Kun 签发器与轮换环，没有另建签发实现。目前会产生一次额外签名，属于现有 Kun 装配 API 的性能限制，不影响每跳受众边界。

默认不信任任何代理头。部署时 `trustedProxyNetworks` 仅填写实际 Traefik CIDR，拒绝 `/0`；只由 `UseForwardedHeaders` 更新连接地址，IP 限流仅使用更新后的 `RemoteIpAddress`；租户与用户策略仅使用已认证的规范 claim。转发下游时重新写入已归一的客户端 IP、协议和配置中的网关主机名，丢弃原始 `Forwarded`。

签名密钥通过原子替换私钥文件热轮换，旧公钥固定重叠 10 分钟。重叠状态在进程内，副本之间与重启前后须协调发布；消费者必须刷新 JWKS 并验证令牌寿命。错误密钥替换由 Kun 保留上一把有效密钥。不要把移除 JWKS 项表述为瞬间撤销所有消费者的缓存。

## 路由限流（S1-GW-002）

每条 `routes[]` 必须显式填写 `rateLimiterPolicy`，值为 `kun-default`、`tenant` 或 `user`。组件 Schema、静态 profile/CLI 校验与宿主装配均拒绝缺失、空值、`disable` 或未知值。既有 C# 宿主也须初始化 `GatewayRoute.RateLimiterPolicy`，例如 `new GatewayRoute("api", "/api/{**rest}", "qian-platform") { RateLimiterPolicy = "tenant" }`；仓库宿主与组合包已同步。

| 策略 | 分区依据 |
|---|---|
| `kun-default` | 可信转发中间件处理后的连接 IP；IPv4 映射地址归一为 IPv4 |
| `tenant` | 已认证主体的唯一、合法 `tenant_id`；同租户用户共用额度 |
| `user` | 已认证主体的 `principal_kind`、租户及 `sub`；平台主体使用独立平台分区 |

缺少规范身份的请求退回该路由的 IP 限额，不能用请求头或查询参数指定主体。限流在认证之后、授权和换签之前执行；无凭据请求仍会受 IP 限制。所有代理路由仍要求认证。

计数键由规范 issuer、路由 ID、策略与分区值的摘要组成，Redis 不存明文主体或 IP。每条路由独立计数，同一逻辑网关的副本必须共用 Redis、issuer、路由 ID 和以下配置；重启实例不会重置额度。`rateLimits` 及其各字段可省略，缺省值如下：

| 配置 | 缺省值 | 边界 |
|---|---|---|
| `keyPrefix` | `qian:gateway:rate` | 1–128 个 ASCII 字母、数字、冒号、下划线或连字符；同组副本保持一致 |
| `windowSeconds` | 60 | 1–86400 秒，窗口从该桶首次请求开始 |
| `ipPermitLimit` | 100 | 1–1000000，IP 策略及身份缺失时使用 |
| `tenantPermitLimit` | 100 | 1–1000000 |
| `userPermitLimit` | 100 | 1–1000000 |

计数端口复用 Kun 的 `IRedisRateLimitStore`。默认适配器用单键 Lua 原子递增并设置 TTL，后续命中不续期，同时修复无 TTL 的遗留桶；每个请求只命中一次 Redis，不使用本机额度或本机排队。ASP.NET 的真实端点策略按 YARP 路由的声明执行，保留宿主对其它端点的限流装配。

超限返回 429 / `RATE_LIMITED`，响应体为 ErrorDetail，`Retry-After` 按 Redis 剩余窗口向上取整且至少为 1 秒；被拒绝的请求不进入换签与下游。Redis 连接或超时故障返回 503 / `UNAVAILABLE`，不当作超限，也不放行。Redis 连接复用宿主登记的 `IConnectionMultiplexer`；完整 `/readyz` 依赖聚合仍由 GW-003 承接。

## 宿主调用顺序

在服务阶段装 Identity.Client 的规范 Cookie 方案，再调用 `AddQianGateway(configuration, IdentityClientDefaults.SessionScheme)`；通过真实 PluginLoader 挂载 Config、Identity 与 Gateway。流水线顺序为：

```csharp
app.UseForwardedHeaders();
app.UseQianShell(shell);          // 可选：外壳静态托管（S1-YUE-012），放在 UseRouting 之前
app.UseRouting();
app.UseQianIdentityClient();
app.UseQianGateway();
app.MapQianIdentityClient();
app.MapQianGateway();
app.MapQianShellBootstrap();      // 可选：GET /api/shell/bootstrap
app.MapQianShell(shell);          // 可选：/app-config.json 与外壳入口兜底，放在最后
```

前置代理头中间件只能安装一次，不能借重复执行增加可信跳数。业务路由受组件挂载门、认证、授权和限流约束；`/readyz` 包含组件挂载、Kun 签名环与 Identity.Client 的现有检查，完整的 Redis/上游等聚合仍由 GW-003 接续。

## 外壳：bootstrap 聚合与静态托管（S1-YUE-012）

外壳（`apps/qian-shell`）是 platform 的 shell 组合包，经网关同源提供。代理路由一律要求认证，所以外壳的静态制品由网关进程自己从目录提供（`GatewayShellHosting`）：

- **`UseQianShell(ShellStaticOptions)`**：`Root` 是外壳构建产物（含 `index.html`），`Remotes` 把已部署组件的远程制品目录挂在 `/remotes/{组件 id}/`。HTML `no-store`，`/assets/` 下带哈希的资源长缓存，其余与远程制品 `no-cache`；全部响应带 `nosniff`、`Referrer-Policy: no-referrer` 与 `Content-Security-Policy: frame-ancestors 'none'`（脚本来源由构建产物 `index.html` 的 meta CSP 声明）。目录缺入口、远程键不是组件 id 或目录不存在时启动即抛错。
- **`MapQianShell(ShellStaticOptions)`**：`ConfigFile`（K8s 里 ConfigMap 挂成的 JSON 文件）以同源 `GET /app-config.json` 提供，每次现读、`no-store`，文件不在即 404。没有命中任何端点、不带扩展名、接受 `text/html` 的 GET / HEAD 回 `index.html`（页面导航与深链接）；API 前缀下打错的路径、脚本请求与其它方法一律 404，不拿 HTML 冒充 JSON。
- **`MapQianShellBootstrap(path = "/api/shell/bootstrap")`**：会话取自 Identity.Client 的 Cookie 会话，`session` 与 `GET /auth/session` 同形，未认证 401。平台身份（没有租户）不调下游，回代管上下文。租户成员：网关按自己的路由表为平台表面的三条路径（`GET /api/capabilities/shell`、`/api/capabilities/navigation`、`/api/authz/me/permissions`）选 cluster，签该 cluster 受众的 60 秒 S2S 后并发调用并组合，与浏览器直调同一路由、同一受众。任一下游 401 即 401（客户端刷新会话后重放），外壳目录或有效权限 403 即 403，其余失败 502，能力导航失败降级为 `null`，下游 404（未挂载或未部署）按空处理。响应 `private, no-store`，不含任何令牌：`{ session, context, remotes, mounted, entitled, permissions, navigation, managed }`；`mounted` / `entitled` 的键是 `scope|组件 id`，权益（entitled）的真实来源归 S1-ENT-002，此前与 mounted 等价。

平台表面（用户主体的能力目录、导航、外壳目录与有效权限）在生产里由 platform profile 的平台宿主提供，网关路由表把 `/api/capabilities/**`、`/api/authz/**` 分簇给它（受众 `qian-platform`）；参考宿主是 `samples/Qian.Sample.Crm` 的 `PlatformSurfaceHost`，生产宿主归 S1-DEP-009，镜像接线（外壳与远程制品进网关 Pod、实测响应头）归 S1-DEP-008。

## 验证

`tests/Qian.Gateway.Tests` 使用真实 Kestrel、Identity/OpenIddict、PostgreSQL、Redis 和 RSA，验证用户 Cookie 与机器令牌换签、逐服务受众、冻结 S2S Schema、原始凭据清除、错误 Bearer 不降级、10 分钟双钥轮换与在途请求、非可信 XFF 不改变 IP/分区、实际 429、卸载与就绪状态。限流专项以真实账号完成 PKCE 后的访问令牌驱动两个独立网关，对每种策略按 200 RPS 发出 200 个请求；两次预热后，50 次共享额度仅余 48 次，断言 48 个成功、152 个 429、Retry-After 与 ErrorDetail、下游总计 50 次。另覆盖窗口过期、孤儿桶 TTL 修复、分区隔离及 Redis 故障关闭。时钟只推进轮换环，旧令牌仍处于真实寿命内，因此窗口后拒绝证明的是旧 kid 移除。

`tests/Qian.Sample.Crm.Tests/GatewayAcceptanceTests.cs` 连接真实 `TenantOperationsHost` 的 `/api/tenancy/tenants`，通过真实账号 PKCE 登录获取 Cookie、真实平台超管角色放行；验证 `aud = qian-platform`、平台 `tenant_id = null` 与 60 秒寿命。租户 UUID 由另一个真实 Cookie 路径单独验证，不把平台运营端点误当作租户业务接口。

`tests/Qian.Gateway.Tests/GatewayShellHostingTests.cs` 验证外壳静态托管的缓存与安全头、深链接与非页面请求、`/app-config.json` 每次现读与远程制品目录，以及选项非法时拒绝启动；`tests/Qian.Sample.Crm.Tests/ShellBootstrapTests.cs` 经真实网关、平台表面参考宿主与 PostgreSQL 验证 bootstrap：匿名 401、成员结果与会话 / 导航 / 有效权限端点一致、平台身份代管、切换租户后远程与权限随之变化、未挂载组件不出现、旧修订 401 刷新后恢复、响应体不含令牌样式串。

`tests/Qian.Gateway.Host.AotTests` 启动实际生产宿主的 Linux 原生二进制，验证真实 Apollo 2.5.2、受限 PostgreSQL、Redis、身份账号与 Cookie/Bearer 换签、平台审计及正常停机。2026-10-23 已在固定 Linux SDK 环境通过严格发布（零 IL 诊断）与运行，Kun 使用正式 `2026.10.23`；`gateway-host-aot` CI 作业持续执行同一发布和 driver。普通托管构建也可能关闭动态代码能力，因此原生证明同时要求严格发布、ELF 文件头、无同名 DLL 和实际业务运行。

## 模型体验

网关没有模型可调用工具。错误返回遵循 Runtime ErrorDetail；令牌、Cookie、授权码、密钥和配置值不写入诊断。能力目录、权限过滤和 Agent 表面按原任务分工接续。

## 已知限制与延期项

DEC-030 的原生 Data / Audit 已接入完整生产宿主，默认 profile 恢复 `build.aot: true`，Gateway / Apollo 清单登记实际验证后的 `x-aot`。原 EF 探针失败与后续执行证据保留在 [AOT 实施路径](../../docs/decisions/gateway-native-aot-options.md)。平台与 Scheduler 继续使用 EF；distroless、供应链签名、只读非 root 与业务镜像发布继续由 DEP-008 验收。

三策略与共享计数已由 GW-002 交付；从组件表面生成完整路由、可用性过滤、通用聚合端点、完整就绪聚合与 gRPC-Web 归 GW-003（外壳专用的 bootstrap 聚合已由 S1-YUE-012 交付），平台观测组件归 AUD-003。GW-003 与 AUD-003 的上述范围仍待交付。
