---
title: 网关分层：Traefik（北向入口）→ qian.gateway（Kun.Gateway 加固，业务 API 边界）；Higress 只管模型与 MCP 流量
status: Accepted
date: 2026-10-06
decision_id: D10
supersedes: none
---

# ADR-010 网关分层

## 1 背景与问题（Context）

现网有两个 IngressClass：`traefik`（业务北向）与 `higress`（AI 南向），各有 MetalLB VIP；集群不直接暴露公网，公网 nginx 终结 TLS 后经 WireGuard 直连；集群内无 cert-manager，Ingress 全 HTTP；Traefik `forwardedHeaders.trustedIPs` 必须配置否则 OIDC issuer 变成 http（基线 K §6.1）。Higress 2.2.3 两副本 + PDB，是模型流量的 chokepoint，对租户无感（基线 K §6.4）。

`Kun.Gateway` 是 YARP 反代 + 外部 JWT 校验（单 issuer）+ `S2sTokenIssuer`（RS256 / 60 秒）+ JWKS + 三种限流策略 + `IS2sPrincipalResolver` + `ForwardedClaimTypes` 白名单 + gRPC-Web，可执行亦可作库；但**默认装配不可上线**：不强制认证、匿名透传下游；`kun-default` 限流未挂到任何路由；IP 分区信任 `X-Forwarded-For`；换私钥须重启；无动态 `IProxyConfigProvider`；网关宿主未接 OTel（基线 K §1 Gateway 行、附录 B-11）。S2S 签发器有三套（网关 RS256 / 60 秒；`Kun.Security.S2SJwt` ES256 / 300 秒含 `scope / session_id / membership_id / project_id`；`Kun.Identity.KunJwtIssuer`），且默认 issuer 值写死为下游产品名（基线 K §1 Security 行、§5.1 第 7 条）。白皮书声称的 `/s2s/issue` 机器身份端点不存在（基线 K 附录 B-13）。gRPC 三个拦截器只覆盖 Unary，流式调用绕过认证与 `tenant_id` 校验（基线 K §2.4 ⑤）。参考平台开放接口用用户级 AppSecret 并允许「公有」免鉴权；接口体 AES 加密中间件本质是混淆（基线 D §9.1 第 8 条；基线 P §B.2）。

## 2 决策（Decision）

- **D10.1 三层网关，职责从 S1 画清、不互相承担**：Traefik（北向公网入口）→ `qian.gateway`（业务 API 边界）→ Higress（模型与 MCP 流量）。
- **D10.2 Traefik 不做鉴权**，只做入口路由与 TLS 终结衔接；`trustedIPs` 必配；ingress 全 HTTP 直到引入 cert-manager。
- **D10.3 `qian.gateway` 以库方式引用 `Kun.Gateway` 并加固**：认证回退策略（默认拒绝匿名）；路由级 `RateLimiterPolicy` 与全局限流器；可信代理网段；私钥热轮换（JWKS 双钥重叠）；Cookie 会话换 S2S（`IS2sPrincipalResolver` 注入租户 / 应用上下文）；按上游 cluster 铸造 per-service audience；`AddKunObservability`；`/healthz /readyz`；OpenAPI 聚合与能力目录端点（生产不公开 OpenAPI）。
- **D10.4 S2S 签发器收敛为一套**：保留网关 `S2sTokenIssuer`（短 TTL、JWKS），吸收 `scope / session_id / membership_id / project_id` claim；claim 形状对齐 iClaw 契约 `iss / aud[] / sub / tenant_id / project_id? / roles[] / scope 串 / session_id / iat,nbf,exp,jti`，`exp − iat ≤ 300s`；「新能力授权 = 加 scope 串，不加 claim」；`roles[]` 作为与 iClaw 同形的例外保留，但限定为粗粒度角色代码而非权限点集合，Qian 自身服务不得仅凭 `roles[]` 放行、必须调用 `Kun.Authorization` 决策端口，时效性由 `identity_revision` 管；`project_id` 保留为可选 claim（iClaw 侧语义，Qian 只透传不解释）（R24）；`Kun.Identity.KunJwtIssuer` 与 `Kun.Security.S2SJwt` 标记废弃；去产品化（issuer 默认值等约 30 处）。
- **D10.5 机器身份签发**：`client_credentials` 由 `qian.identity` 提供（S1 必做）；后台任务 / 作业以服务自持私钥签发为过渡，S4 前统一经签发端点。
- **D10.6 多 Pod 共享限流计数**：Redis `PartitionedRateLimiter`，S1 可选、S2 必做。
- **D10.7 S3 动态路由 provider**：租户目录 × 已安装应用 → YARP routes / clusters，由插件清单 `components.service.protocols` 与 `tenancy` 派生；「未启用」在网关路由、SDK 入口、前端外壳三处同时拦截；域名 → 租户解析 S1 可选 / S3 必做。
- **D10.8 开放接口凭据**（Q35）：废弃用户级 AppSecret，统一为网关的应用级客户端凭据（`client_credentials` / API Key，经 `qian.identity` 客户端注册表）；取消「公有」免鉴权。
- **D10.9 gRPC 流式拦截器补齐**（server / client / duplex streaming），`service` 型插件用流式 RPC 前必须完成。
- **D10.10 Higress 只管模型与 MCP 流量**，对租户无感；不在 S1 把 SSO / S2S 交给 Higress；网关策略类插件直接用 Higress `WasmPlugin` CRD，不自造。
- **D10.11 不做**：接口体 AES 加密中间件（下沉网关 TLS）；网关解析 JSON 体做鉴权（MCP 按 `Mcp-Method / Mcp-Name` 头路由，敏感参数不入头）。

## 3 理由（Rationale）

- 两个网关是不同层次：`qian.gateway` 是「外部 JWT → S2S」的业务边界，Higress 是模型 / MCP 入口；混用会让租户语义泄漏进 Higress 或让模型流量绕过平台计量（基线 K §6.4 与 `Kun.Gateway` 的关系）。
- Kun 的安全默认值必须写成内核强制项而不是沿用默认装配，这是基线 P §5 的明确结论。
- 收敛签发器是插件生态不分裂的前提；选网关签发器是因为它已有 JWKS 与短 TTL 实践，缺的只是 claim 形状（基线 K §5.1 第 7 条）。

## 4 备选方案与放弃原因（Alternatives）

- **Higress 统一所有流量与认证**：Higress 对租户无感，认证收编（Wasm）已被 iClaw 明确延后；会把 S1 的身份边界交给第三方网关。放弃。
- **单层 Traefik + 服务各自鉴权**：参考平台「鉴权分散」的反例。放弃。
- **保留 `Kun.Security.S2SJwt` 作唯一签发器**（基线 P §5 的建议）：与网关 JWKS 轮换链路重复；本 ADR 采 K 的方案并吸收其 claim。
- **每服务独立网关**：三节点集群不可承受。放弃。

## 5 后果与代价（Consequences）

- 正面：匿名流量在 S1 起就被挡在业务边界外；S2S 令牌一种形状；模型流量与业务流量的计量各归其位。
- 负面：`qian.gateway` 承担动态路由后成为有状态的配置消费者，需与注册表一致性设计；签发器收敛需要 Kun 上游配合或补丁包；Cookie 会话换 S2S 的性能与会话一致性需要验证。
- 配套：JWKS 双钥轮换运维手册；路由级限流基线值；S3 动态路由的灰度与回滚。

过渡态（2026-10-06）：集群内无 cert-manager、Ingress 全 HTTP；网关宿主未接 OTel；三套签发器并存。S1 完成 D10.3–D10.4 前，Qian 不对外提供任何租户可见 API。

## 6 对各阶段的影响

- **S1**：D10.2–D10.5、D10.9、D10.10；OpenAPI 聚合与能力目录端点。
- **S2**：Redis 共享限流；低代码数据引擎 API 经网关统一鉴权；外链能力令牌经网关匿名路由 + 服务端校验。
- **S3**：动态路由 provider；按租户选 IdP 多 issuer（如需）；OPA 粗粒度 ABAC 可选。
- **S4**：Higress 托管每租户 MCP 逻辑门面；Agent 一律经 Higress MCP 路由，令牌受众绑定、禁透传。

## 7 待确认事项

- 已裁决（R5）：保留 Kun.Gateway 的 `S2sTokenIssuer`，吸收 `scope / session_id / membership_id / project_id` claim，`Kun.Security.S2SJwt` 与 `Kun.Identity.KunJwtIssuer` 标记废弃（即 D10.4）。
- 域名 → 租户解析是否进入 S1。
- cert-manager 引入时点（TLS 进集群）。

## 8 参考

- 相关 ADR：ADR-001、ADR-003、ADR-005、ADR-006、ADR-015、ADR-016、ADR-017。
- PRD：`PRD §07`、`PRD §10`、`PRD §12`、`PRD §13`。
- 基线：K §1 Gateway / Security 行、§2.4 ④⑤⑥、§5.1 第 4/5/7 条、§6.1、§6.4、附录 B-11/B-13；P §5 主体身份行、§6 `qian.gateway` 行、§8.2 调用、§8.6；D §9.1 第 8 条。
