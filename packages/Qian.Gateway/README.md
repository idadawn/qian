# Qian.Gateway

网关组件以库方式组合 Kun.Gateway 的签发器、JWKS、热轮换、可信代理和限流原语，把外部访问令牌或 `qian.sid` 会话转换为目标服务的短时 S2S。真实宿主入口为 [Qian.Gateway.Host](../../hosts/Qian.Gateway.Host/README.md)。

## 组件与装配

- 组件文件夹是 `plugins/qian.gateway`：唯一清单、派生配置 Schema 与说明；没有模型工具、技能或 UI 表面。
- 默认进入 gateway profile。Definition 是 `Qian.Gateway.Abstractions` / `Gateway`，本包是 Provider，消费 `Identity` 与 `Config`。
- `GatewayComponent` 是唯一 `runtime.entry`，仅允许 global scope。宿主的网络配置与挂载条目必须一致；卸载即停止代理请求，撤销服务键，并令 `/readyz` 返回 503。
- 本包只依赖 Runtime、SDK、所需 Abstractions 与 Kun，不引用 Identity.Client、Data 或 Audit Provider；它们由宿主组合。
- `qian add gateway` 的正式私有源安装尚未验证，组件保留 `x-skeleton`。真实代码入口与可构建宿主不等于已完成发布或安装验收。

组件配置由 `ComponentConfiguration.Schema` 派生到唯一 Schema。`GatewayHosting.ReadConfiguration()` 严格拒绝重复成员、未知字段、缺失下游、带凭据的 URL、相对密钥路径与全网代理 CIDR。示例在 [gateway 组合包](../../plugins/qian.bundle.gateway/config/qian.patch.yml)。密钥路径指向宿主的 Secret 文件，永不把 PEM 内容写进配置。

## 固定安全边界

所有代理路由强制认证，不能通过路由配置设置 `Anonymous`。唯一匿名端点为宿主明确映射的身份协议端点、JWKS、`/healthz` 与 `/readyz`。无凭据和错误凭据在代理之前拒绝，错误使用 ErrorDetail。

外部 Bearer 只接收配置中唯一身份发行方的 `at+jwt`，验证签名、issuer、audience、寿命，拒绝重复载荷字段和 S2S 类型。Cookie 由 Identity.Client 读取真实 Redis 会话并验证访问令牌；网关不自行解析会话 Cookie。带显式 Authorization 的请求必须独立通过 Bearer 验证，不能在失败时退回已登录 Cookie。

统一主体经 Kun `S2sTokenIssuer` 签 RS256，固定 60 秒，每个 cluster 只有自己的 audience。用户的 OIDC `sid` 映射到 `session_id`，服务主体由签发器令 `session_id = jti`；不透明 Cookie 值不进入令牌。只保留冻结契约字段，任意附加声明不透传。转发前移除原 Authorization 与 Cookie，再写入新 S2S。

Kun 的默认代理变换使用固定 audience，Qian 在代理前先为实际路由签发并校验目标令牌，最后一个请求变换覆盖为该令牌；两次都使用同一个 Kun 签发器与轮换环，没有另建签发实现。目前会产生一次额外签名，属于现有 Kun 装配 API 的性能限制，不影响每跳受众边界。

默认不信任任何代理头。部署时 `trustedProxyNetworks` 仅填写实际 Traefik CIDR，拒绝 `/0`；只由 `UseForwardedHeaders` 更新连接地址，限流按更新后的 `RemoteIpAddress` 分区。转发下游时重新写入已归一的客户端 IP、协议和配置中的网关主机名，丢弃原始 `Forwarded`。

签名密钥通过原子替换私钥文件热轮换，旧公钥固定重叠 10 分钟。重叠状态在进程内，副本之间与重启前后须协调发布；消费者必须刷新 JWKS 并验证令牌寿命。错误密钥替换由 Kun 保留上一把有效密钥。不要把移除 JWKS 项表述为瞬间撤销所有消费者的缓存。

## 宿主调用顺序

在服务阶段装 Identity.Client 的规范 Cookie 方案，再调用 `AddQianGateway(configuration, IdentityClientDefaults.SessionScheme)`；通过真实 PluginLoader 挂载 Config、Identity 与 Gateway。流水线顺序为：

```csharp
app.UseForwardedHeaders();
app.UseRouting();
app.UseQianIdentityClient();
app.UseQianGateway();
app.MapQianIdentityClient();
app.MapQianGateway();
```

前置代理头中间件只能安装一次，不能借重复执行增加可信跳数。业务路由受组件挂载门、认证、授权和限流约束；`/readyz` 包含组件挂载、Kun 签名环与 Identity.Client 的现有检查，完整的 Redis/上游等聚合仍由 GW-003 接续。

## 验证

`tests/Qian.Gateway.Tests` 使用真实 Kestrel、Identity/OpenIddict、PostgreSQL、Redis 和 RSA，验证用户 Cookie 与机器令牌换签、逐服务受众、冻结 S2S Schema、原始凭据清除、错误 Bearer 不降级、10 分钟双钥轮换与在途请求、非可信 XFF 不改变 IP/分区、实际 429、卸载与就绪状态。时钟只推进轮换环，旧令牌仍处于真实寿命内，因此窗口后拒绝证明的是旧 kid 移除。

`tests/Qian.Sample.Crm.Tests/GatewayAcceptanceTests.cs` 连接真实 `TenantOperationsHost` 的 `/api/tenancy/tenants`，通过真实账号 PKCE 登录获取 Cookie、真实平台超管角色放行；验证 `aud = qian-platform`、平台 `tenant_id = null` 与 60 秒寿命。租户 UUID 由另一个真实 Cookie 路径单独验证，不把平台运营端点误当作租户业务接口。

`tests/Qian.Gateway.Host.AotTests` 启动实际生产宿主的 Linux 原生二进制，验证真实 Apollo 2.5.2、受限 PostgreSQL、Redis、身份账号与 Cookie/Bearer 换签、平台审计及正常停机。2026-10-23 已在固定 Linux SDK 环境通过严格发布（零 IL 诊断）与运行，Kun 使用正式 `2026.10.23`；`gateway-host-aot` CI 作业持续执行同一发布和 driver。普通托管构建也可能关闭动态代码能力，因此原生证明同时要求严格发布、ELF 文件头、无同名 DLL 和实际业务运行。

## 模型体验

网关没有模型可调用工具。错误返回遵循 Runtime ErrorDetail；令牌、Cookie、授权码、密钥和配置值不写入诊断。能力目录、权限过滤和 Agent 表面按原任务分工接续。

## 已知限制与延期项

DEC-030 的原生 Data / Audit 已接入完整生产宿主，默认 profile 恢复 `build.aot: true`，Gateway / Apollo 清单登记实际验证后的 `x-aot`。原 EF 探针失败与后续执行证据保留在 [AOT 实施路径](../../docs/decisions/gateway-native-aot-options.md)。平台与 Scheduler 继续使用 EF；distroless、供应链签名、只读非 root 与业务镜像发布继续由 DEP-008 验收。

三策略/共享计数归 GW-002，路由生成、可用性过滤、聚合端点、完整就绪聚合与 gRPC-Web 归 GW-003，平台观测组件归 AUD-003。当前不宣称这些能力已交付。
