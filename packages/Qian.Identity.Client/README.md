# Qian.Identity.Client

身份 seam 的依赖方（RP）Provider，薄包装 Kun.Identity / Kun.Identity.AspNetCore / Kun.Security，不复制它们的实现：

- **浏览器会话（BFF）**：服务端授权码 + PKCE（S256）换取，校验 `state`（一次性、防重放）、RFC 9207 `iss`、id_token 的签名 / 受众 / `nonce`；会话放在 Cookie `qian.sid`（256 位不透明标识，HttpOnly、Secure、SameSite=Lax），令牌只留在服务端会话存储里。
- **刷新**：访问令牌到期前用 refresh token 换新；同一会话在进程内只发一次刷新请求，跨副本用 Kun 会话注册表的一次性标记互斥（qian-identity 开启了 refresh 轮换与零容忍复用检测，并发复用会吊销整族）；刷新失败即结束会话。
- **discovery 定期刷新**：元数据与 JWKS 按 `DiscoveryRefreshInterval`（缺省 5 分钟）过期重取，遇到未知签名密钥另行提前刷新（两次强制刷新至少间隔 30 秒）。
- **下游 S2S 校验**：只信网关签发器的 issuer 与 JWKS，`aud` 必须恰好是本服务（每跳一个受众），载荷按冻结契约（[contracts/v0](../../contracts/v0/README.md) §4）逐条校验；上一跳收到的令牌转发给下一跳、用户访问令牌直接调用下游一律拒绝。Kun 的透传处理器 `PropagateS2sTokenHandler` / `AddKunServiceAuth` 在 BannedSymbols 里禁用。

同一个包进 `platform`、`gateway` 与独立宿主（ADR-019）：独立宿主只装本包即可对接任意 OIDC 发行方，不部署 Qian 平台、不需要 Config 组件（公开客户端）。

## 安装与装配

```bash
dotnet add package Qian.Identity.Client
```

```csharp
builder.Services.AddDataProtection();                       // 多副本须共享受保护的密钥环
builder.Services.AddSingleton<IConnectionMultiplexer>(...); // 会话存储（Redis）；开发可设 UseInMemorySessionStore
builder.Services.AddQianIdentityClient(new IdentityClientOptions
{
    Authority = new Uri("https://id.example.com/"),
    ClientId = "qian-web",
    Scopes = ["openid", "offline_access", "qian.api"],
    Resource = "https://api.example.com",                  // 对接 qian-identity 时配置；任意发行方省略
    RedirectUri = new Uri("https://app.example.com/auth/callback"),
    AllowedReturnPaths = ["/app"]
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseRouting();
app.UseQianIdentityClient();   // 错误详情、端点门控与禁缓存头、Kun Cookie 会话（滑动续期 + 防伪校验）
app.UseAuthentication();
app.UseAuthorization();
app.MapQianIdentityClient();   // /auth/login、回调、/auth/logout、/auth/csrf、/auth/backchannel-logout

// 经 PluginLoader 挂载组件；浏览器流程在挂载后才生效，卸载后端点返回 404、Cookie 会话不再被认可。
await loader.MountAsync([new(new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0, config), new IdentityClientPlugin(), [IdentityDefinition.Key])], ct);
```

组件配置（[plugins/qian.identity.client](../../plugins/qian.identity.client/README.md)）：`issuer`、`clientId` 必须与宿主选项一致；`clientSecret` 只接受 `secretRef://platform/…` 或 `secretRef://dev/…`，第一次换码时经 Config 组件的密钥策略解析（清单声明 `identity/*`，宿主审批），密钥不经代码、选项或配置明文传入。公开客户端省略 `clientSecret`，此时不依赖 Config。轮换密钥 = 重新挂载组件。

下游服务只需：

```csharp
builder.Services.AddQianIdentityS2s(new IdentityS2sOptions
{
    Issuer = "https://gateway.example.com/s2s", Audience = "qian-platform", JwksUri = new Uri("http://gateway.internal/.well-known/jwks.json")
});
app.UseQianErrors(); app.UseAuthentication(); app.UseAuthorization();
```

通过校验的请求，`ICurrentUser.Principal` 是令牌载荷的规范快照（与 AZ-004 的 PEP 交叉比对的字段一致）。两种装配可在同一宿主共存，缺省认证方案取先登记者，端点可显式指定方案。

## 选项

| 选项 | 缺省 | 说明 |
|---|---|---|
| `Authority` | 必填 | 发行方 issuer；discovery 的 issuer 必须与之一致。`RequireHttpsMetadata` 为 true 时必须是 HTTPS。 |
| `ClientId` | 必填 | 客户端标识。 |
| `Scopes` | `["openid"]` | 必须含 `openid`；需要刷新时加 `offline_access`。 |
| `Resource` | 空 | 访问令牌的预期受众（须是发行方为该客户端登记的资源）。配置后会话主体取自访问令牌的规范身份（见下）；授权请求不带 `resource`，由发行方按登记签发。 |
| `RedirectUri` | 空 | 在发行方登记的回调；只来自可信配置，不按请求 Host 推导。为空时不启用浏览器会话（只做 S2S 或只校验令牌的宿主）。 |
| `PathPrefix` | `/auth` | 登录、登出、防伪令牌与后通道登出端点的前缀。 |
| `DefaultReturnPath` / `AllowedReturnPaths` | `/` / 空 | `returnTo` 只接受同源相对路径（解码后不含 `.`、`..` 段，不含反斜杠与控制字符），白名单按路径段前缀匹配；不合法时回缺省落点。 |
| `RequireIssuerParameter` | false | 要求授权响应带 `iss`（RFC 9207）；不论是否要求，带了就与 discovery 的 issuer 比对。qian-identity 总会带。 |
| `DiscoveryRefreshInterval` | 5 分钟 | 5 分钟到 1 天。 |
| `RefreshBeforeExpiry` | 1 分钟 | 访问令牌到期前多久换新，0 到 10 分钟。 |
| `SessionLifetime` | 8 小时 | 会话寿命，经认证的请求滑动续期。 |
| `SessionKeyPrefix` / `UseInMemorySessionStore` | `qian:identity-client:` / false | Redis 会话键前缀（宿主登记 `IConnectionMultiplexer`）；进程内存储只用于单进程开发与测试。 |
| `RequireSecureCookies` | true | 会话、登录事务与防伪 Cookie 只经 HTTPS 发送。Kun 的登录事务 Cookie 恒为 Secure，所以浏览器流程须跑在 HTTPS 上（`localhost` 也一样）。 |
| `RequireHttpsMetadata` | true | 只有本机测试发行方可关闭。 |

发行方访问只经命名 HTTP 客户端 `Qian.Identity.Client`（不跟随跳转、不带 Cookie、超时 10 秒），宿主可为它配置代理或受信根证书。健康检查 `qian.identity.client`（标签 `ready`）在 discovery 与 JWKS 都取得到时才通过。

## 两种会话主体

- **规范模式**（配置了 `Resource`，对接 qian-identity）：缺省认证方案是 `qian.session`。每个请求在 Kun 验证过的 Cookie 会话之上取访问令牌（必要时刷新），按发行方 JWKS 验签（`typ` 必须 `at+jwt`、受众 `Resource`、寿命），投影出规范身份：`sub`、`principal_kind`、恰好一个 `tenant_id`（平台身份为 JSON null）、`membership_id`、`identity_revision`、`session_id`（取发行方的 `sid`；不透明的 `qian.sid` 永不当作会话标识）、`scope`、`amr`、`acr`，并核对与登录时的 id_token 同一主体、同一租户。投影出的主体正好是 Kun 网关签发器 `S2sTokenIssuer` 的输入，网关可直接为下游受众铸 S2S。访问令牌校验失败的请求按匿名处理。
- **任意发行方模式**（不配置 `Resource`）：会话主体只取自 id_token（`ICurrentUser.UserId` 为 `sub`，`ClaimTypes.Email` / `Name` 可读），`ICurrentUser.Principal` 为空——没有 Qian 的规范身份声明，授权 PEP 会一律拒绝。

`IdentitySessionTokens.GetAccessTokenAsync(HttpContext)` 给服务端代码取当前会话的有效访问令牌（必要时刷新）；令牌不得写入日志、响应或前端存储。

## 端点

| 端点 | 说明 |
|---|---|
| `GET {PathPrefix}/login?returnTo=` | 生成 PKCE verifier、state、nonce（存在 Data Protection 保护的一次性 Cookie 里，5 分钟），302 到授权端点。 |
| `GET {RedirectUri 的路径}` | 先消费登录事务（无论成败事务 Cookie 都删除、state 登记为已用），再查 `iss`、`error`，然后带原始 verifier 与 nonce 换码；规范模式下会话建立前先确认访问令牌可投影；成功则建立新会话（旧会话作废），302 回 `returnTo`。 |
| `POST {PathPrefix}/logout` | 删除本地会话与 Cookie，204。有会话时必须带防伪请求头 `X-CSRF-TOKEN`，否则 400。 |
| `GET {PathPrefix}/csrf` | 已认证时返回 `{ headerName, token }`。 |
| `POST {PathPrefix}/backchannel-logout` | OIDC 后通道登出：验签 `logout_token` 后按 issuer + sid 吊销本地会话（发行方须登记该地址）。 |

回调的拒绝都是 ErrorDetail：`AUTH_CONNECTOR_STATE_INVALID`（422，state 缺失 / 不匹配 / 过期 / 重放，或缺 code）、`OIDC_ISSUER_MISMATCH`（400）、`AUTH_CONNECTOR_DENIED`（422，发行方以 OAuth 错误结束授权；`error` 只记进日志，协议定义之外的值记为 `server_error`，不进文案）、`OIDC_TOKEN_INVALID`（401，换码失败或令牌校验未过，含 nonce 不符）、`BAD_GATEWAY`（502，发行方不可达）。state 失效与发行方拒绝和身份服务的连接器回调（S1-IDP-007）共用同一组 `AUTH_CONNECTOR_*` 码，外壳回调页只需识别一套。日志只记拒绝类别、错误类型与 sid 的 SHA-256，不记令牌、授权码、state、口令或原始会话标识；回调地址的查询串会被 ASP.NET Core 的请求日志打印，所以本包把 `Microsoft.AspNetCore.Hosting.Diagnostics` 限到 Warning（与身份服务一致）。

外壳 SPA 直接换码、令牌留在前端内存的方案（`TokenProvider.getAccessToken / refreshAccessToken` 一类的外壳令牌端点）是 auth-pages 规格 D5 的待定选项，本包**不提供**，等 D5 裁定（提案、待 D5）。

## Identity seam

`IdentityClientPlugin` 以 `IdentityDefinition.Key` 提供依赖方的 `IIdentityService`：`CurrentPrincipal` 是当前请求已验证的规范快照；人员、成员、会话管理与连接器注册属于身份服务，调用一律返回 `IDENTITY_OPERATION_UNSUPPORTED`（501），不伪造空结果。只允许 global scope，同一宿主只允许一个 Client。

## 已知限制

- 不做 RP 发起的发行方登出（end_session），也不调用撤销端点：本地登出只结束本地会话，发行方的 SSO 会话仍在；发行方侧的吊销经后通道登出同步到本地。
- 本地登出与正在进行的刷新并发时，刷新的结果只续写仍存活的会话，已登出的会话不会复活；但刷新已经换到的令牌族要等发行方侧过期。
- `prompt`、`login_hint`、租户提示等授权参数尚未透传（外壳接线随 D5 / YUE-002）。
- 清单仍是配置骨架（不声明 `runtime`）：`gateway` profile 开了 AOT，而依赖的 Kun 发布包未带 AOT 兼容标记，见组件文件夹的说明。

## 测试

`tests/Qian.Identity.Client.Tests`：真实 qian-identity（内置账号模型、强制 PKCE）上的服务端换码、篡改 nonce / iss / state 重放、刷新单飞与轮换、登出防伪、卸载、后通道登出，以及会话主体经 Kun 网关签发器铸 S2S 后下游接受、透传被拒；S2S 契约逐条违约。`tests/Qian.Identity.Client.Standalone.Tests`：只引用本包的独立宿主对接 Dex（独立实现的 CNCF OIDC 发行方，见 [tests/README.md](../../tests/README.md) 的 `DexFixture`）完成登录与登出，并断言依赖闭包里没有其他 Qian 组件。选 Dex 而不是第二个 qian-identity，是因为后者证明不了「任意 OIDC 发行方」。
