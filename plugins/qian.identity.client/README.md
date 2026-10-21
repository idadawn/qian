# qian.identity.client

身份依赖方（RP）组件。Provider 已在 [Qian.Identity.Client](../../packages/Qian.Identity.Client/README.md) 中实现：服务端授权码 + PKCE 换取、`nonce` 与 RFC 9207 `iss` 校验、`qian.sid` Cookie 会话与刷新、discovery 定期刷新，以及下游的 S2S 令牌校验（`aud` 只认本服务、禁止透传上一跳令牌）。本文件夹仍标记 x-skeleton，原因见下文第四问。

## 组件四问

1. **文件夹里有哪些表面**：只有清单与配置 Schema（`schemas/config.schema.json`），没有 skills、commands、hooks 或工具表面。浏览器端点（`/auth/login`、回调、`/auth/logout`、`/auth/csrf`、`/auth/backchannel-logout`）由代码包映射，不是声明式表面。
2. **装进哪个 profile**：`platform`（base 组合包的 `identity` 条目）、`gateway`（网关组合包的 `identity` 条目）；`standalone` 宿主按需自行加入。`identity` profile 的同名条目装的是 `qian.identity.server`，两者不会同时出现在一个 profile 里。
3. **属于哪个 seam 的哪个角色**：Identity seam 的依赖方 Provider，只回答当前主体；人员、成员与会话管理属于身份服务，依赖方上调用一律返回 `IDENTITY_OPERATION_UNSUPPORTED`。只允许 global scope。
4. **能否 `qian add`**：代码包可以直接作为 NuGet 库引用并独立使用（ADR-019）。`qian add identity` 的完整安装路径进展如下（S1-IDP-014）：
   - **已完成：`runtime` 与 `x-aot`。** `IdentityClientPlugin` 带 `[QianPlugin]`，`inject`、`provides`、`runtime.form`、`runtime.entry` 由源生成器派生，经 `Qian.Plugin.Tool sync` 写入，CI 检查漂移。`gateway` profile 开了 AOT（`build.aot: true`），装载器要求其中的代码组件声明 `x-aot`。本组件的声明以 CI 的 NativeAOT 冒烟为据（`tools/Qian.Identity.Client.AotSmoke`：ILC 0 条 IL 警告，原生二进制挂载组件并通过端点冒烟）。依赖的 Kun 发布包缺 AOT 兼容元数据，是 Kun 的构建缺陷（S1-KUN-015），判断兼容性不看它。
   - **还差：私有源发布与钉版。** 私有源发布 `Qian.Identity.Client`，在 `runtime.nuget` 钉版本与归档摘要（同 S1-RT-011 对 Data / Events / Config 的做法），然后去掉 x-skeleton。发版工作流只能从 main 触发，所以这一步在本组件的代码合入 main 之后做。

   完整组合（`qian add identity authz files`）的验收归 S1-QA-005（R84）。网关整体能否保持 AOT 归 S1-GW-001：同一 profile 里的配置组件和审计组件还没有 AOT 依据。

## 配置

| 字段 | 必填 | 说明 |
|---|---|---|
| `issuer` | 是 | 发行方的规范 issuer，必须与宿主 `IdentityClientOptions.Authority` 一致（末尾斜杠不计）。只用于核对，发行方地址只来自宿主的可信装配。 |
| `clientId` | 是 | 在发行方登记的客户端标识，必须与 `IdentityClientOptions.ClientId` 一致。 |
| `clientSecret` | 否 | 机密客户端的密钥，只接受 `secretRef://platform/…` 或 `secretRef://dev/…`；第一次换码时经 Config 的密钥策略解析（清单声明 `identity/*`，宿主审批后才可读）。公开客户端省略它，此时不依赖 Config 组件。 |

明文密钥、`tenant` 作用域的引用、未知字段或与宿主不一致的 issuer / clientId 都会让挂载失败（`INPUT_INVALID`），错误里不回显配置值。
