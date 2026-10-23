# @qian/shell-kit（Qian 外壳专有接线）

私有工作区包，不发包（S1-YUE-012，ADR-019 D19.1）。通用外壳 UI 与契约留在 [`@idadawn/yue-shell`](../yue/shell/README.md)；对接 Qian 平台的部分放在这里，由 [apps/qian-shell](../../apps/qian-shell/README.md) 与认证页族 [packages/qian-identity-ui](../qian-identity-ui/README.md) 引用。

## 导出

| 模块 | 内容 |
|---|---|
| `app-config` | `loadAppConfig()` 以 `no-store`、同源、不跟随重定向读同源 `/app-config.json`（K8s ConfigMap 挂成的文件）；404 即全部缺省值，其余失败、非 JSON 或字段非法都抛 `shell.error.config`。`parseAppConfig()` 校验形状：`apiBase`（缺省 `/api`）、`authBase`（`/auth`）、`bootstrapUrl`（`/api/shell/bootstrap`）、`auditUrl`（`/api/shell/audit/remote-rejected`）统一走 `isSameOriginPath`，另有可选 `brand` / `locale` / `managed`，未知字段拒绝。 |
| `bff-session` | `createBffSessionClient()`：网关 Identity.Client BFF 的会话客户端（`GET {authBase}/session` 快照、`POST {authBase}/session/refresh` 强制刷新，供 `PlatformClient({ sessionProvider })`）、防伪令牌（`GET {authBase}/csrf`）、登出与登录 / 切换租户（`prompt=select_account`）的跳转地址。浏览器零令牌，身份修订按 int64 保留。`createSessionExpiryGuard()` 包住它交给 SDK 作 `sessionProvider`，记下最近一次刷新的结论：工作台从未就绪时收到过期通知，依赖方说会话已结束才跳登录，刷新成功、重放仍 401（平台表面拒绝）只弹过期对话框，刷新出错时先问一次会话快照（第一轮验收复核：否则整页重载会无界跳登录）。 |
| `bootstrap` | `ShellBootstrap` 契约与 `fetchBootstrap()`：经外壳的平台客户端取网关聚合端点（401 随 SDK 合并成一次会话刷新后重放），形状不符按 502。 |
| `middleware` | `apiPrefixMiddleware(apiBase)` 把规范路径 `/api/...` 换到部署前缀；`csrfMiddleware(session)` 给每个写请求先取一枚新的防伪令牌再带头发出。 |
| `navigation` | `toMenu()`：能力导航目录（`GET /api/capabilities/navigation` 或 bootstrap 的 `navigation`）→ `@idadawn/yue-shell` 的菜单项。 |
| `@qian/shell-kit/identity` | `createIdentityAuthProvider()`：认证页族用的身份服务同源 AuthProvider（`/api/identity/*`，防伪令牌 cookie-to-header，选择与改密票据只在内存）。 |

## 同源路径

`isSameOriginPath` 只接受以单个 `/` 开头的路径：拒绝绝对源、协议相对 `//`、反斜杠、查询串、片段、空白与控制字符、`.` / `..` 段与结尾斜杠。BFF Cookie 会话只对同源发送，跨源前缀会把请求引到别处；配置是数据不是脚本，外壳构建产物的 meta CSP 不放行任何来自配置的代码。

## 测试

`src/kit.test.ts`（根 `pnpm yue:test` 的 `qian-shell-kit` 项目）覆盖运行时配置的缺省、合法覆盖与各类非法输入、读取失败，前缀映射与防伪中间件，会话客户端与首载过期的处置（含与 SDK 联动），bootstrap 契约形状，后端菜单与身份服务 AuthProvider。lint、typecheck、分层与循环依赖随 yue 门禁运行；分层规则把本包放在 yue 之上：`@idadawn/yue-*` 与插件 SDK 不得反向引用它，外壳、认证页族与参考应用远程模块可以引（`tests/gates/yue/boundaries.test.ts`）。
