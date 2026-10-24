# qian.identity.client

身份依赖方（RP）组件。Provider 已在 [Qian.Identity.Client](../../packages/Qian.Identity.Client/README.md) 中实现：服务端授权码 + PKCE 换取、`nonce` 与 RFC 9207 `iss` 校验、`qian.sid` Cookie 会话与刷新、discovery 定期刷新，以及下游的 S2S 令牌校验（`aud` 只认本服务、禁止透传上一跳令牌）。本文件夹已完成私有源安装验收并移除骨架标记（S1-IDP-015）。

## 组件四问

1. **文件夹里有哪些表面**：只有清单与配置 Schema（`schemas/config.schema.json`），没有 skills、commands、hooks 或工具表面。浏览器端点（`/auth/login`、回调、`/auth/logout`、`/auth/csrf`、`/auth/session`、`/auth/session/refresh`、`/auth/backchannel-logout`）由代码包映射，不是声明式表面。
2. **装进哪个 profile**：`platform`（base 组合包的 `identity` 条目）、`gateway`（网关组合包的 `identity` 条目）；`standalone` 宿主按需自行加入。`identity` profile 的同名条目装的是 `qian.identity.server`，两者不会同时出现在一个 profile 里。
3. **属于哪个 seam 的哪个角色**：Identity seam 的依赖方 Provider，只回答当前主体；人员、成员与会话管理属于身份服务，依赖方上调用一律返回 `IDENTITY_OPERATION_UNSUPPORTED`。只允许 global scope。
4. **能否 `qian add`**：代码包可直接作为 NuGet 库独立引用。唯一清单已由 SDK 派生 `IdentityClientComponent` 入口、`Identity` 提供项与 `runtime.configSchema`，配置 Schema 的代码真相源为 `ComponentConfiguration.Schema`。已通过真实 `qian add identity` 安装验收（S1-IDP-015）：`runtime.nuget` 钉 `Qian.Identity.Client` 0.3.0 与发布归档的 SHA-256，代码包从 GitHub Packages 还原；表面进入 `.qian/plugins/qian.identity.client`，lock 记录组件与包摘要。包族由 main 的 `dotnet-release` 锁步发布，宿主按清单入口挂载到 Ready，卸载撤销 Identity 服务。

   清单声明了 `x-aot: true`，依据是 CI 的 NativeAOT 冒烟（S1-IDP-014，`tools/Qian.Identity.Client.AotSmoke`）：本组件连同依赖的 Kun 包以 ILC 发布 linux-x64，0 条 IL 警告；原生二进制挂载组件后通过端点冒烟。依赖的 Kun 发布包缺 AOT 兼容元数据，是 Kun 的构建缺陷（S1-KUN-015），判断兼容性不看它。按 S1-DEC-029，开发期 `gateway` 暂用 JIT；gateway 整体的 NativeAOT 验收（配置、审计、数据组件与真实网关宿主）仍归 GW-001 / DEP-008。完整组合（`qian add identity authz files`）的验收归 S1-QA-005（R84）。

## 安装

宿主配置正式私有 NuGet 源及签名市场后运行 `qian add identity --yes`。市场中的 `identity` 条目指向本组件；E2E 沿用已裁决的签名本地市场（R98），官方市场目录由 S1-RT-017 交付。

安装器不会猜测 OIDC 发行方：CLI 新建常规条目的 ID 取组件 ID 的最后一段，因此 `qian add identity` 的市场简写是 `identity`，条目 ID 是 `client`。在空白宿主的目标 profile patch 预置 disabled 的 `client` 条目（`plugin: qian.identity.client`），填写 `issuer` 和 `clientId`，安装时沿用配置并启用；宿主 `AddQianIdentityClient` 的值必须相同。公开客户端省略 `clientSecret`，可独立装配而不启动 Config 或 Data Provider。`qian remove identity --purge` 撤销包引用、组件文件和 patch 条目；完整登录、刷新、登出验收见包 README，安装门验证实际已发布 Provider 的挂载、匿名会话端点返回 401、卸载后端点返回 404，以及旧上下文失效。

## 配置

| 字段 | 必填 | 说明 |
|---|---|---|
| `issuer` | 是 | 发行方的规范 issuer，必须与宿主 `IdentityClientOptions.Authority` 一致（末尾斜杠不计）。只用于核对，发行方地址只来自宿主的可信装配。 |
| `clientId` | 是 | 在发行方登记的客户端标识，必须与 `IdentityClientOptions.ClientId` 一致。 |
| `clientSecret` | 否 | 机密客户端的密钥，只接受 `secretRef://platform/…` 或 `secretRef://dev/…`；第一次换码时经 Config 的密钥策略解析（清单声明 `identity/*`，宿主审批后才可读）。公开客户端省略它，此时不依赖 Config 组件。 |

明文密钥、`tenant` 作用域的引用、未知字段或与宿主不一致的 issuer / clientId 都会让挂载失败（`INPUT_INVALID`），错误里不回显配置值。
