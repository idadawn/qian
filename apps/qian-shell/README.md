# qian-shell（Qian 外壳）

Qian 唯一可部署的外壳入口（S1-YUE-012 把 S1-YUE-001 的 MF2 静态宿主与 S1-YUE-002 的薄入口融合于此，`apps/qian-shell-auth` 已撤销）。通用 UI 来自 [`@idadawn/yue-shell`](../../packages/yue/shell/README.md)，Qian 专有接线来自私有包 [`@qian/shell-kit`](../../packages/qian-shell-kit/README.md)，远程模块的加载、插槽与桥接来自 [前端插件 SDK](../../packages/qian-plugin-sdk/README.md) 的 `ShellHost`。属于 `platform` 的 shell 组合包，由网关进程以产品扩展同源提供（[Qian.Gateway](../../packages/Qian.Gateway/README.md) 的 `UseQianShell` / `MapQianShell`）；不是独立 profile。

## 启动顺序

1. 读同源 `/app-config.json`（`no-store`、同源、不跟随重定向）并校验；不合法只渲染配置错误页，不建客户端、不发任何别的请求。文件不存在（404）即全部缺省值。
2. 建外壳运行时（`src/runtime.ts`，整页只建一次）：一个依赖方会话客户端、一个平台客户端、一个 MF2 宿主；`host.start()` 之后才登记远程。
3. 拉一轮工作区：`GET {apiBase}/shell/bootstrap`（网关聚合端点）一次给出会话快照、上下文、已挂载远程、挂载 / 权益键、已放行权限码与能力导航；远程按差异登记 / 卸载后再一次性通知界面；租户成员再拉一次能力目录条数（辅助接口，失败只在卡片上显示「不可用」，不影响整页）。

## 接线

- **运行时配置**：只有一份 JSON（K8s ConfigMap 挂成同源 `/app-config.json`）：`apiBase`（缺省 `/api`）、`authBase`（`/auth`）、`bootstrapUrl`（`/api/shell/bootstrap`）、`auditUrl`（`/api/shell/audit/remote-rejected`）、可选 `brand` / `locale` / `managed`。四个地址统一做同源路径校验（拒绝绝对源、`//`、反斜杠、查询串、片段、空白与控制字符、点段、结尾斜杠），未知字段拒绝。`locale` 只是部署缺省：用户保存过的语言优先，且不写进偏好存储。改文件后刷新页面即生效，不重编译。
- **规范路径**：契约、远程模块与 `bridge.invoke` 一律写 `/api/...`；外壳平台客户端的中间件把它映射到 `apiBase`，远程模块不感知部署前缀。写请求（非 GET / HEAD / OPTIONS）每次先取一枚新的防伪令牌（`GET {authBase}/csrf`）再带头发出。
- **安全策略**：构建产物 `index.html` 带 meta CSP（`vite.config.ts` 构建期写入）：`script-src 'self' 'unsafe-eval'` 加首帧主题脚本的 sha256，`connect-src` / `img-src` / `font-src` 只许同源（图片与字体另许 `data:`），`object-src 'none'`、`base-uri 'none'`、`frame-src 'none'`、`form-action 'self'`。`frame-ancestors 'none'`、`Referrer-Policy: no-referrer` 与 `nosniff` 由网关静态托管的响应头下发。`scripts/verify-dist.mjs` 在每次构建后断言这些指令、内联脚本哈希一一对应、入口只引用同源资源、正式产物没有验收控制面、首屏 JS gzip ≤ 250 KiB。`'unsafe-eval'` 是已登记的例外：`@idadawn/yue-form` 的 SchemaForm 在运行期用 Ajv 编译表单 Schema（低代码表单的 Schema 来自运行期，无法预编译），共享给远程页面后在外壳源执行；外壳自身与插件 SDK 的启动路径已不在运行期生成代码。它不让配置变成代码：运行时配置是 JSON 数据，只解析与校验，从不求值。
- **会话**：网关 BFF 的 HttpOnly Cookie `qian.sid`，浏览器零令牌（存储、URL、iframe、响应体都没有令牌，`document.cookie` 看不到 `qian.sid`）。首次加载就没有会话时顶层跳 `{authBase}/login?returnTo=当前路径`。
- **刷新合并与粘性结束**：`PlatformClient({ sessionProvider })` 把并发 401 合并成一次会话刷新（先取防伪令牌再 `POST {authBase}/session/refresh`），成功重放并通知外壳重拉 bootstrap（进行中的一轮结束后再完整拉一次）；失败或重放仍 401 只弹一次「登录已过期」对话框。工作台从未就绪（首载、整页重载）时由 `@qian/shell-kit` 的会话过期守卫区分：依赖方说会话已结束才顶层跳登录；依赖方刷新成功、重放仍 401（平台表面拒绝）只弹对话框，不跳转，否则账号 Cookie 仍有效时登录端点静默签码、回调后照样 401，整页无界循环。会话结束对外壳是粘性的：之后刷新通知与排队的重拉都不再发起请求，只有整页重新登录才复位（`packages/yue/sdk/README.md` 的宿主契约）。
- **远程模块**：bootstrap 的 `remotes` 按 `scope|id` 与版本、清单比对差异后登记或卸载；菜单命中的页面才 `loadSlot`（manifest → remoteEntry → expose），未挂载、无权益或无权限的远程完全不请求。已加载的页面只要还在宿主里（同一 bridge）就继续用，重拉 bootstrap 不会让页面重新挂载；加载失败对同一页面是粘性的，显示「插件未能加载」。远程页面收到外壳的平台客户端与宿主闭包绑定的 bridge；`window.__qian.bridge` 只含受控调用入口。
- **菜单与路由**：真实平台给出能力导航（含菜单偏好与权限过滤）时以它为准，否则由宿主按已登记远程的清单、挂载、权益与权限算出。路由是 `@idadawn/yue-shell` 的最小历史路由：按 pathname 精确匹配、再最长前缀；根路径带查询串仍是工作台；未命中走 404 兜底，渲染异常由错误边界兜成 500 页。
- **平台身份**：会话 `tenantId` 为空即平台代管：bootstrap 不调下游、直接回代管上下文，外壳只显示代管横幅与会话卡片，不拉能力目录。
- **切换租户**：用户菜单「切换租户」→ `{authBase}/login?returnTo=/&prompt=select_account` → 身份服务同源选择页 → 新授权码 → 网关回调重建会话 → 整页回到工作台（新文档、新宿主）。
- **偏好**：命名空间 `qian-shell`，只存 theme / locale / sidebarCollapsed；`html[lang]` 与界面文案随语言即时切换；首帧主题脚本在样式之前按已存主题设好 `data-theme`。

## 构建与制品

```bash
pnpm --filter @idadawn/qian-runtime build
pnpm --filter qian-shell build                 # vite build + scripts/verify-dist.mjs
pnpm --filter @qian/tenancy-ui build:remote
node apps/qian-shell/scripts/package-static.mjs
```

外壳产物在 `dist/host/`。`package-static.mjs` 生成 Web 根 `dist/artifact/`（外壳 `index.html / assets`、官方租户远程 `remotes/qian.tenancy/`、MF 清单与类型归档、文件摘要清单）与组件目录 `dist/distribution/plugins/qian.shell/`（原清单、配置 Schema 与真实 `ui/dist/index.html`；`component-artifact.json` 记录逐文件摘要与 Web 根的同字节映射，`installationVerified: false`，保留 `x-skeleton` 安装门）。运行时配置不进制品：部署时由 ConfigMap 挂成 `/app-config.json`，网关每次现读。镜像接线与实测响应头归 S1-DEP-008。

## 本机运行

先按 [联调环境说明](../../tools/Qian.Dev.Shell/README.md) 运行 `scripts/dev-shell.sh up`（它构建认证页族、参考应用远程模块与本外壳，并经网关静态托管 `dist/host/`），浏览器打开 <https://localhost:5443/>。改前端代码后 `pnpm --filter qian-shell build`，刷新即可。`pnpm --filter qian-shell dev` 的开发服务器用缺省配置顶替 `/app-config.json`，并把 `/api`、`/api-alt`、`/auth`、`/__dev`、`/remotes` 代理到本机网关（只许回环地址）；开发服务器不带 meta CSP。

## 验收

两条门禁都对本目录运行：

| 门禁 | 入口 | 内容 |
|---|---|---|
| `pnpm yue:mf`（CI `shell-federation`） | `scripts/e2e.mjs` → `playwright.config.ts` + `e2e/` | 夹具 bootstrap（`scripts/serve-acceptance.mjs`）+ 真实审计宿主：官方租户远程按页面加载；共享 React、PlatformClient、SDK 与 Button 引用一致；scope 遮蔽与卸载恢复；租户 B 未挂载时 manifest 请求为零；`engines.yue` 拒绝并在控制台与真实 PostgreSQL 审计链留痕；window bridge 的 getContext / navigate / invoke 与撤销；运行时配置不合法时只有配置错误页、零请求；全程零 CSP 违规；首屏 JS gzip ≤ 250 KiB（`acceptance-results/first-screen.json`）。验收控制面只由 `--mode acceptance` 引用。 |
| `pnpm yue:auth`（CI `shell-auth`） | `scripts/e2e-auth.mjs` → `playwright.auth.config.ts` + `e2e-auth/` | 对 `tools/Qian.Dev.Shell` 拉起的真实 qian.identity + 平台表面宿主 + 上游 Dex + 网关 BFF 跑完整浏览器流程，见下表。失败时宿主、PostgreSQL 与 Dex 的日志在 `TestResults/shell-auth/logs`。 |

真实流程（S1-YUE-002 验收 1–4）：

| 验收 | 步骤 |
|---|---|
| 1 真实流程 | 匿名 → 身份服务同源登录页 → 账号密码（OIDC code + PKCE，网关换码）→ 多租户选择 → 工作台（会话、菜单、权限来自 bootstrap，浏览器不再分别直调导航与权限）→「客户」页由参考应用远程模块经 MF2 按需加载并真实渲染（导航高亮、`data-tenant` 为甲方），列表经 `bridge.invoke` 带防伪头调 customer.list，平台表面以 403 `AUTHZ_DENIED` 拒绝（见已知限制），页面优雅降级为错误提示；「新建客户」页同样由远程模块渲染 → 404 与带查询串的根路径 → `prompt=select_account` 切换到乙方，菜单 / 权限随之变化 → 服务端令牌到期透明刷新（0 次浏览器刷新、无对话框）→ 会话仍在时强制刷新回快照不回令牌 → 身份修订递增（`/__dev/identity/bump`）后旧令牌 401：1 次刷新、重放后再拉一轮 bootstrap、会话卡片修订 +1、无对话框 → refresh 失效：探测 401、不再 POST、只弹 1 个过期对话框且 Esc 关不掉 → 重新登录回到乙方 |
| 1 SSO 跳转回调 | 登录页填租户代码 →「Dex 本机身份源」→ 上游 Dex 登录 → 落点首次确认绑定 → 工作台；清掉全部 Cookie 再走一次 → 绑定已存在，直接进入 |
| 刷新成功但平台表面持续 401 | `second`（只属于乙方）登录 → `/__dev/identity/stale` 只抬平台表面修订下限 → bootstrap 401 → 恰好 1 次刷新（成功）→ 重放仍 401 → 恰好 1 个过期对话框；静置 3 秒请求数不变（共 2 条 bootstrap，均 401）。随后整页重载：不顶层跳登录，同样 1 次刷新、2 条 401、1 个对话框，静置 3 秒不变；再点「重新登录」：恰好 1 次顶层跳 `/auth/login`，回到工作台后对话框再现、整页稳定 |
| 平台身份 | `operator` 登录 → 代管横幅 + 会话卡片；登录后只有 1 次 bootstrap、0 次刷新、无过期对话框 |
| 2 远程按挂载加载 | 甲方已挂载参考应用：导航有「客户」，工作台不请求远程制品，进入页面才请求 `mf-manifest.json` 与 `remoteEntry.js`；切到丙方（未挂载）：bootstrap 的远程 / 挂载 / 权益 / 导航全空、不含 manifest 地址，导航为空，直达页面路由是 404，丙方会话里远程制品请求为零 |
| 3 代码审查项 | 全程同源 JSON 响应体、存储、URL、iframe 都不含令牌；`document.cookie` 看不到 `qian.sid`；请求不带 `Authorization`；零 CSP 违规；顶栏切换侧栏 / 主题 / 语言即时生效（`html[lang]` 跟随），偏好只存三项 |
| 4 运行时配置 | 改状态目录 `app-config.json` 的 `apiBase` 为 `/api-alt` → 刷新 → 品牌与菜单照常，bootstrap 与能力目录全部走 `/api-alt/**`，没有一条再走 `/api/**`；构建产物不变 |

另一条用例检查认证页族的入口边界：没有 ReturnUrl、跨源、非授权端点或带令牌参数的回跳都不渲染登录表单。

## 已知限制

- CSP 的 `'unsafe-eval'` 要等 `@idadawn/yue-form` 换成解释型校验后才能去掉（见上）。
- 权益（`entitled`）在 S1-ENT-002 前与 `mounted` 等价。
- 用户主体的平台表面在联调栈与集成测试里由参考宿主 `PlatformSurfaceHost` 提供；生产的平台表面宿主（platform profile 镜像）另立 S1-DEP-009。
- 外壳经 `bridge.invoke` 调应用能力时，令牌要带能力声明的 OAuth scope（参考应用是 `crm.read` / `crm.write`）；联调栈的外壳客户端登记并申请了它们，生产外壳客户端的 scope 清单随部署任务交付。
- 参考应用（CRM）的命令运行时只认服务 / 代理主体，用户主体经外壳调 customer.list / customer.create 会被拒绝（403 `AUTHZ_DENIED`），真实流程据此断言拒绝与降级；用户主体执行应用能力属于 S1 边界之外，另立后续任务。
- 站内信只有弹窗壳，消息源待后端接口。
