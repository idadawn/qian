# qian-shell（外壳薄入口）

Qian 外壳的可部署入口：运行时配置注入、网关 BFF 会话、能力导航 → 路由、租户切换、站内信弹窗、主题 / 语言偏好与 403 / 404 / 500 / 离线兜底（S1-YUE-002）。全部能力以 [`@idadawn/yue-shell`](../../packages/yue/shell/README.md) 的库模块实现，本目录只做组合；MF2 宿主、插槽、`loadRemote` 与参考应用远程模块由 S1-YUE-001 在此之上接线（本任务验收 2 留待其合入后补验）。

## 接线

- **运行时配置**：`index.html` 在模块脚本之前加载同源 `/app-conf.js`（K8s ConfigMap 挂载），`readAppConf()` 校验 `window.__APP_CONF__`（`apiBase` 缺省 `/api`、`authBase` 缺省 `/auth`、可选 `brand` / `locale` / `managed`）；非法时只渲染配置错误页，不发请求。构建产物不含任何环境值。
- **会话**：`createBffSessionClient({ authBase })` 对接依赖方（Identity.Client / 网关 BFF）的 `GET /auth/session`、`POST /auth/session/refresh`、`POST /auth/logout` 与 `/auth/login` 跳转地址；浏览器只持有 HttpOnly Cookie `qian.sid`。没有会话就顶层跳到 `/auth/login?returnTo=当前路径`。
- **刷新合并**：`PlatformClient({ sessionProvider: bff })`——并发 401 合并成一次刷新尝试：先取防伪令牌再 `POST /auth/session/refresh`，成功重放一次；会话已被依赖方结束时取防伪令牌这一步即 401、不再 POST；失败只触发一次 `onSessionExpired`，弹 `SessionExpiredDialog`。服务端访问令牌到期前的刷新由依赖方单飞完成，页面无感。
- **菜单与权限**：`GET {apiBase}/capabilities/navigation`（能力导航目录，服务端按调用者权限与租户 scope 过滤）经 `toMenu` + `menuTypeRegistry` 映射为页面路由；`GET {apiBase}/authz/me/permissions` 给出有效权限；`GET {apiBase}/capabilities` 作为第三路请求。登录、切换租户（整页回调）与手动刷新时一起重拉。
- **切换租户**：用户菜单 →「切换租户」→ `/auth/login?returnTo=/&prompt=select_account`；身份服务在账号 Cookie 仍有效时 302 到同源选择页换会话，回到授权请求签新授权码，网关回调重建会话（旧会话删除），工作台整页回来后菜单与权限随之刷新。
- **路由**：`createBrowserRouter` + `matchMenuItem`（精确 → 最长前缀）；命中的页面显示页头与远程模块占位，未命中走 404 兜底；渲染异常由 `ShellErrorBoundary` 兜成 500 页；离线横幅由浏览器事件驱动。
- **代管横幅**：`managed` 缺省按会话判断——平台身份（`tenantId` 为空）即代管。

## 本机运行

先按 [联调环境说明](../../tools/Qian.Dev.Shell/README.md) 运行 `scripts/dev-shell.sh up`（它会构建本目录并静态托管 `dist/`），浏览器打开 <https://localhost:5443/>。改前端代码后 `pnpm --filter qian-shell build`，刷新即可（宿主直接读 `dist/`）。`pnpm --filter qian-shell dev` 的开发服务器用缺省配置顶替 `/app-conf.js`，并把 `/api`、`/auth`、`/__dev` 代理到本机网关。

## 验收

`pnpm --filter qian-shell e2e:auth`（根脚本 `pnpm yue:auth`，CI 工作流 `shell-auth`）：`scripts/e2e-auth.mjs` 用独立临时状态目录、随机回环端口与自有 PostgreSQL 容器拉起真实宿主，等就绪后跑 `e2e/auth.spec.ts`，最后停宿主、删容器；失败时保留状态目录里的宿主日志，Playwright 产物在 `TestResults/shell-auth/`。

规格里的三项验收在同一条用例里按步骤落实：

| 验收 | 步骤 |
|---|---|
| 1 真实流程 | 匿名 → 身份服务同源登录页 → 账号密码（OIDC code + PKCE，网关换码）→ 多租户选择 → 工作台（会话、菜单、权限来自真实接口）→ 菜单路由与 404 → `prompt=select_account` 切换到乙方 → 新授权码、回调重建会话、菜单 / 权限随之变化 → 服务端令牌到期透明刷新（0 次浏览器刷新、无对话框）→ 会话仍在时 `POST /auth/session/refresh` 经真实身份服务换新令牌、回快照不回令牌 → refresh 失效时依赖方结束会话，三路并发 401 合并成 1 次刷新尝试（1 次探测 401、0 次 POST）、只弹 1 个过期对话框、Esc 关不掉 → 重新登录回到乙方 |
| 3 代码审查项 | 全程所有同源 JSON 响应体、`localStorage` / `sessionStorage`、URL、iframe 都不含令牌；`document.cookie` 看不到 `qian.sid`；请求不带 `Authorization` 头；顶栏切换侧栏 / 主题 / 语言即时生效，偏好只存 theme / locale / sidebarCollapsed |
| 4 运行时配置 | 改状态目录 `app-conf.js` 的 `apiBase` 为 `/api-alt` → 刷新 → 品牌与菜单照常渲染，三路请求全部走 `/api-alt/**`，没有一条再走 `/api/**`；构建产物不变 |

另一条用例检查认证页族的入口边界：没有 ReturnUrl、跨源、非授权端点或带令牌参数的回跳都不渲染登录表单。

## 已知限制

- 页面内容是远程模块占位：MF2 宿主、插槽与 `loadRemote`（含租户 scope 未挂载时不请求 `mf-manifest.json`）归 S1-YUE-001；权益接线归 S1-ENT-002。
- 站内信只有弹窗壳，消息源待后端接口；SSO 连接器按钮由认证页族渲染，本环境没有登记上游连接器。
