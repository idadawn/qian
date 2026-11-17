> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# ADR-005 身份与 SSO

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-005-identity-sso.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-005-identity-sso.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Kun 只做依赖方（RP）一侧：OIDC 发现 + 授权码 + PKCE 跳转、id_token 验签、Cookie 不透明会话、外部 Bearer 校验、网关换发 S2S（基线 K §2.2）。已确认缺口 G1–G8：无鉴权抽象；外部 issuer 只能配一个；`ExchangeCodeAsync` 不发 `code_verifier`、nonce 不校验、无 refresh / client_credentials；会话无 user→sessions 索引、无后通道登出、日志打印明文 sid；CSRF 推迟；三套 S2S 签发器；claim 名不统一且请求头兜底 fail-open；`SessionData` 只有单 TenantId、无一人多租户模型（基线 S §6.1）。

团队现用 Authentik 2024.10 单实例，只做员工 / 运维认证；其多租户 Tenancy 是企业版 alpha 且按租户收费，不适合客户 CIAM（基线 K §2.2）。候选对比：Keycloak 26.x Organizations 协议最全但 Java、每 Pod 约 1.25 GB、国内登录要写 SPI、租户主数据双真相；Logto OSS 国内连接器全但 Node 栈、无 LDAP、部分付费；Casdoor 2026-05 一次 9 个 CVE；Zitadel 为 AGPL；Duende 为商业许可（基线 K §2.2；基线 S §9.2）。参考平台的反例：接口层权限校验恒 true、会话不可吊销、SSO 开启后服务端密码接口仍开放、匿名回调返回明文密码（基线 S §6.2、§10 第 1 条）。

## 2 决策（Decision）

- **D5.1 自建 `qian.identity`**：基于 OpenIddict（Apache-2.0，.NET 10，原生 OIDC / OAuth2，支持 RFC 8707）作为平台**唯一 issuer**；每环境一个规范 issuer；S1 单 issuer，租户自定义登录域名 S3 再做且只换登录页宿主（Q4）。
- **D5.2 账号模型**：全局自然人 Person + 租户成员 Membership 双层为主；「租户托管身份」作为 S3 可选；两者并存时以邮箱 / 手机验证后的**显式合并**为准（Q2 补充裁决）。命中多个 Membership 时签发 5 分钟选择令牌；切换租户重签令牌。
- **D5.3 认证因子（S1）**：密码（Argon2，策略化锁定 / 延时）、短信 OTP、TOTP（Q18：TOTP S1 做）。短信渠道 S1 必做（登录验证码与找回密码依赖它）。初始密码必须修改。
- **D5.4 会话中心（S1 必做）**：refresh 轮换、后通道登出双向、user→sessions 索引、强制下线、单端 / 并发上限（默认 5）、`identity_revision` 递增失效、CSRF；日志只记 sid 哈希。会话失效触发：改密、停用、锁定、租户冻结 / 到期、强制下线、权限变更。
- **D5.5 客户端注册表**：平台与官方插件 client S1 建立；`client_credentials` S1 必做；S3「应用安装即建 client」。
- **D5.6 上游联邦全部做成连接器插件 `qian.identity.connector.*`**：S1 只做通用 OIDC + 企业微信（扫码与工作台免登）；钉钉、飞书 S2 必做（P0）；微信网站扫码 S2 可选（P1）；S3 加 SAML / CAS / LDAP（先代理 IdP 连接器过渡）、租户自助配 IdP、域名验证与 HRD、Passkey、SCIM、ISV 第三方应用模式。登录流水线统一：所有方式构造同一「登录输入」走同一签发路径。
- **D5.7 Authentik 退居运维侧**：升级后只服务 Apollo Portal、Headlamp、Argo，可作为一个上游连接器；客户 CIAM 不用它；与 iClaw 共用运维侧 Authentik（Q15）。
- **D5.8 同时修复 Kun.Identity G2–G8**：`codeVerifier`、nonce、refresh、discovery 刷新、会话索引、后通道登出、CSRF、签发器收敛、claim 统一、`membership_id`。
- **D5.9 令牌只含身份**：`sub`、`tenant_id`、`membership_id`、`scope`、`identity_revision`、预留 `profile`；权限不进令牌（见 ADR-006）。
- **D5.10 交付形态假定有私有化 / 离线交付**（Q3），因此不使用 AGPL 与商业许可的身份组件。
- **D5.11 租户自助申请审核、「企业 × 应用」关系、邀请格式** 由 Qian 自行设计，写入 PRD 租户生命周期章（Q13 补充裁决）；第三方绑定同租户唯一、需显式确认，不做静默自动绑定。
- **D5.12 代码复用**：允许从 iClaw 搬运 OIDC 授权码实现（含 `code_verifier`）与多渠道登录绑定模型（Q40），其余只借鉴思路。
- **D5.13 「仅企业 IdP 登录」在服务端策略层强制**，不靠前端跳转。

## 3 理由（Rationale）

- 唯一 issuer 让租户主数据只有一个权威，避免 Keycloak 式双真相；外部 IdP 降级为连接器后，「每租户信任各自 IdP」不再要求网关多 issuer（基线 S §6.1 G2）。
- OpenIddict 与 .NET 10 同栈、许可宽松、内存占用小，适合私有化交付；国内生态（企微 / 钉钉 / 飞书 ISV 模式）开源 IdP 普遍缺，无论选谁都要自研连接器（基线 S §6.2 第 7 条）。
- 会话中心与 `identity_revision` 是「权限不进令牌」能即时失效的前提（基线 S §3.3）。

## 4 备选方案与放弃原因（Alternatives）

- **Keycloak 26.x Organizations**：协议最全，但 Java 栈、资源重、国内登录要写 SPI、租户数据双真相。放弃（若首期必须 SAML/LDAP 可重议）。
- **Logto OSS**：国内连接器全，但 Node 栈、无 LDAP、部分功能付费。放弃。
- **Casdoor / Zitadel / Duende**：安全记录差 / AGPL / 商业许可。不推荐。
- **Authentik 做客户 CIAM**：Tenancy 企业版 alpha、按租户收费。放弃。

## 5 后果与代价（Consequences）

- 正面：账号、租户成员、会话、客户端在一处；连接器 SPI 使登录方式可插拔；与 iClaw 共享 claim 形状。
- 负面：自建身份核心的安全责任全在团队——OIDC 一致性测试、渗透测试、长期补丁节奏必须进预算；这是 S1 最大的两项工作量之一，且与鉴权引擎互为前置（基线 K §7.1 第 1 条）。OpenIddict 8.0 的 resource 存储与会话特性尚在预览（基线 S §6.2 第 11 条）。
- 配套：登录与密码策略、在线用户、登录审计；per-env 独立 OIDC client（现网 prod / staging 共用一个 client 是反例）。

## 6 对各阶段的影响

- **S1**：OpenIddict Server、Person + Membership、三因子、会话中心、通用 OIDC + 企业微信连接器、client_credentials、G2–G8 修复、认证页族（yue）。
- **S2**：钉钉、飞书连接器；委托 grant 原语；用户分组。
- **S3**：租户自助 IdP、HRD、品牌登录域、Passkey、SCIM、SAML/CAS/LDAP、ISV 模式、安装即建 client、自助注销与导出。
- **S4**：Agent 主体、令牌交换 `act` 链、CIMD、RFC 9728 元数据。

## 7 待确认事项

- 等保 2.0 / 商用密码（SM2/SM3）要求：Q18 裁定「不做假设，需确认」；若有，影响签名算法与库选型。
- 已裁决（R4）：微信网站扫码连接器为 S2 P1；S1 连接器只有通用 OIDC 与企业微信（扫码与工作台免登）。
- 已裁决（R4）：决策清单 D5 正文已修订为钉钉 / 飞书 S2 P0、ISV 第三方应用模式 S3，与本 ADR 一致。
- OIDC 一致性测试与渗透测试的预算与时点。

## 8 参考

- 相关 ADR：ADR-004、ADR-006、ADR-010、ADR-016。
- PRD：`PRD §07`、`PRD §09`、`PRD §13`。
- 基线：K §2.2、§4.1、§7.1 第 1 条；S §3.1、§3.3、§6.1、§6.2、§9.2、§10 第 1/7/8/13 条、§11；P §5 主体身份行、§8.3 第 3/15 条。

## 9 身份客户端与产品联调分阶段验收（2026-10-11）

产品负责人已通过 [S1-DEC-015](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-015.md) 确认分阶段验收，消除 S1-KUN-004 与 S1-IDP-002 的循环等待。

- SDK 阶段（S1-KUN-004）：以真实 HTTP/RSA 协议夹具验证 PKCE verifier、nonce、state/到期/重放保护，以真实 Redis 和 HTTP 会话验证吊销、CSRF 与日志脱敏，并通过 S2S 冻结 Schema 和 Qian 编译门禁。该阶段可以独立完成，不要求尚未实现的产品身份服务先启动。
- 产品阶段（S1-IDP-002）：使用已修复的 Kun Identity 客户端，对真实 `qian-identity` 完成强制 PKCE 的授权码换取，并拒绝篡改 nonce 的 id_token。这是身份服务完成及后续发布的必过项，不能用协议夹具、外部 IdP 或 SDK 单测代替。

SDK 阶段完成不代表真实身份服务已实现、上游新包已发布或 Qian 已消费该版本。接入时仍须记录实际 Kun 包版本、issuer、客户端配置与联调结果。
