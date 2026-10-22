# Qian.Identity.Connector.Oidc

通用 OIDC 上游连接器（S1-IDP-007，PRD 7.4.2、ADR-005 D5.6 / D5.12）：把企业 IdP、运维侧 Authentik 这类标准 OpenID Provider 接成身份 seam 的连接器。只做协议：授权码 + PKCE（S256）、换码、校验 id_token，输出 `ExternalAssertion`；state / nonce 的生成与关联、统一回调、确认绑定、JIT 与会话签发都由身份 Provider（[Qian.Identity.Server](../Qian.Identity.Server/README.md)）负责。

## 装配位置

identity profile 的连接器组件（`qian.identity.connector.oidc`）。宿主经 `PluginLoader` 在身份 Provider 之后挂载 `OidcConnectorPlugin`：

- 无参构造：`Inject` 为 `Identity` 与 `Config`，客户端密钥经 `ctx.ResolveSecretAsync` 按本组件的声明、审批与当前租户解析。
- `new OidcConnectorPlugin(secretResolver, handler?)`：不装 Config 的独立宿主自带 `ISecretResolver`；出站处理器可选，传入时归宿主所有。

Apply 按配置构造 `OidcIdentityConnector`，以配置的 `name`（缺省 `oidc`）登记到当前 scope 的 `Identity.Connectors`（服务键 `Identity.Connectors:oidc`，组件条目的 `provides` 写同一个键）。挂在某个租户的 scope 时只对该租户可见，并遮蔽全局的同名连接器，即租户自带 IdP。卸载即撤销注册、释放连接器与自有连接池。不经组件模型时也可以直接 `new OidcIdentityConnector(options, secrets, handler)` 再自行登记。

## 配置

与组件文件夹的 [schemas/config.schema.json](../../plugins/qian.identity.connector.oidc/schemas/config.schema.json) 同一份（嵌入程序集，即清单的 `ConfigSchema`）：

| 成员 | 说明 |
| --- | --- |
| `issuer` | 必填。上游 issuer，须与其发现文档里的 `issuer` 逐字一致 |
| `clientId` | 必填。在上游登记的客户端标识 |
| `clientSecret` | 必填，只接受 `secretRef://platform/…`、`secretRef://tenant/…`（或开发用 `dev`），明文与其他格式一律拒绝（INPUT_INVALID，pointer `#/clientSecret`，错误里不回显输入） |
| `name` | 注册名与回调路径段，缺省 `oidc`；改名时组件条目的 `provides` 要写 `Identity.Connectors:<name>` |
| `displayName` | 登录页按钮上的名称，缺省「企业身份源」 |
| `scopes` | 缺省 `openid profile email`，必须含 `openid` |
| `terminals` | 缺省 `pc`、`mobile` |
| `requireHttps` | 缺省 true；只有 issuer 是回环地址时才允许关闭（本机开发与测试） |

在上游登记的回调地址是 `{身份服务 issuer}connect/callback/{name}`，身份服务的连接器详情接口会给出 `callback_uri`。

## 协议行为

- **发现文档**：取 `{issuer}/.well-known/openid-configuration`，`issuer` 必须与配置逐字一致；授权、令牌与 JWKS 端点必须满足与 issuer 相同的传输要求（https，或关闭要求时的回环 http），不带用户信息。发现文档缓存 1 小时。上游声明了 `code_challenge_methods_supported` 却不含 S256 时拒绝。
- **发起**：授权地址带 `response_type=code`、`client_id`、`redirect_uri`、`scope`、身份服务给的 `state` 与 `nonce`、由 `code_verifier` 算出的 S256 `code_challenge`。授权地址本身已有查询串时追加在后面。
- **回调**：授权响应带 `iss` 时必须等于配置的 issuer；上游声明 `authorization_response_iss_parameter_supported` 时缺了它也拒绝（RFC 9207，防混淆攻击），这两种情况都是 `AUTH_CONNECTOR_STATE_INVALID`。换码默认 `client_secret_basic`（按 RFC 6749 先对 id 与 secret 做表单编码），上游只声明 `client_secret_post` 时放进表单；客户端密钥每次换码现取现用，不缓存明文。响应体上限 1 MiB，不跟随重定向。
- **id_token**：签名只接受 RS / PS / ES 族（JWKS 里的对称密钥不参与验签，`alg: none` 与 HS 族一律拒绝），校验 `iss`、`aud`、`exp`（容差 2 分钟），`aud` 有多个值时 `azp` 必须是本客户端，给了 `azp` 也必须是本客户端，`sub` 必填；`nonce` 必须与本次请求一致（常量时间比较），否则 `AUTH_CONNECTOR_STATE_INVALID`。遇到未知 `kid` 刷新一次 JWKS 再验，强制刷新至少间隔 30 秒，伪造的 kid 不能逼它反复拉取；其余时候 JWKS 缓存 1 小时。
- **断言**：`Issuer` 为配置的 issuer，`Subject` 为 `sub`，`Profile` 只取 `name`、`preferred_username`、`email`、`email_verified`，`Amr` 取上游 `amr` 里的短字母数字项。
- **失败**：上游不可达或响应无效、签名不符是 `BAD_GATEWAY`，超时是 `GATEWAY_TIMEOUT`；身份服务把它们记进流程并呈现给界面。

## 依赖边界

只依赖 `Qian.Identity.Abstractions`、`Qian.Config.Abstractions`（secretRef 与密钥解析端口）与 `Microsoft.IdentityModel.JsonWebTokens`；不依赖 `Qian.Identity.Server` 实现包，CI 的依赖图门禁与 `tests/gates/test_check_deps.py` 的 `RealConnectorClosureTests` 对此把关。

## 验收

`tests/Qian.Identity.Connector.Oidc.Tests`：进程内的 OP 替身走完整授权码 + PKCE，逐项篡改签名（同 kid 换私钥、不签名、用客户端密钥 HS256 伪签且 JWKS 发布了对称密钥）、iss、aud、sub、azp、多受众、nonce、有效期、RFC 9207 的 iss 参数、发现文档 issuer、PKCE verifier、超大与非 JSON 响应，以及发现文档与 JWKS 的缓存和刷新间隔；配置只接受 secretRef、清单合法且 Schema 与组件文件夹同一份、描述不含任何配置值、组件按 scope 注册与卸载。关键校验做过变异验证（去掉任一校验，至少一个用例失败）。端到端由 `Qian.Identity.Server.Tests` 的 `AuthentikConnectorTests` 对容器里的真实 Authentik 完成。

## 模型体验

连接器不是模型工具，不派生 capability；模型不会由本包获得登录、换码或读取密钥的能力。

## 已知限制与延期项

- 组件文件夹 `plugins/qian.identity.connector.oidc` 的清单仍是配置骨架：`inject`（Identity、Config）与 `provides`（`Identity.Connectors:oidc`）要等身份 Provider 的清单去骨架后才能写进去（否则 identity profile 找不到 `Identity` 的 Provider），SDK 生成器也还不支持具名 seam（QIANSDK002）；`policy.egress` 取决于部署接的是哪个 IdP，由部署方在出口白名单里登记上游主机。
- 发现文档里的端点来自上游，连接器只校验传输要求，不限制主机；出网目标的约束靠部署的出口策略。
- 不调用 userinfo，资料只取 id_token 里的声明；不支持 `private_key_jwt` 等非对称客户端认证、PAR、JARM 与前通道 / 后通道登出。
