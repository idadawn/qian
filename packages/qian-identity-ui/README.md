# 认证页族（qian.identity 同源）

qian-identity 宿主同源托管的静态制品：登录（账号密码、连接器按钮）、多租户二次选择、强制改密、连接器落点，以及外壳切换租户用的选择页（S1-YUE-002；界面控件来自 [`@idadawn/yue-shell`](../yue/shell/README.md) 的认证页族）。凭据只与发行方同源：页面直接调同源 `/api/identity/*`（防伪令牌 cookie-to-header，账号 Cookie 由服务端写入、HttpOnly），不持有任何令牌，也没有环境值进入构建产物。

## 路由与入口

构建以 `/login/` 为基路径，宿主把 `dist/` 静态托管在 `/login` 下并对无扩展名的子路径回落到 `index.html`：

| 路径 | 用途 |
|---|---|
| `/login?ReturnUrl=` | 账号 Cookie scheme 的登录页（宿主把 `LoginPath` 指到这里）。只接受同源、落在 `/connect/authorize` 的 ReturnUrl，`client_id` 取自其查询串；其它值一律显示「登录入口无效」，不渲染表单。登录成功（含二次选择、强制改密）后顶层跳回 ReturnUrl 签发授权码 |
| `/login/select-tenant?return_url=` | 外壳切换租户：授权端点遇 `prompt=select_account` 且账号 Cookie 有效时 302 到这里；页面调 `POST /api/identity/session/switch` → `/login/select` 换会话（旧会话吊销 reason=switch），再回到去掉 prompt 的授权请求。Cookie 已失效时回登录页，登录后仍回到同一授权请求 |
| `/login/connector/{connectorId}?flow=` | 连接器统一回调的界面落点：按流程 `next` 驱动等待（轮询）、确认绑定（绑定到已验证账号 / JIT 建号 / 放弃；未验证时先用账号密码验证）、完成（回 `return_url`）与错误（重新发起登录） |

## 本机运行

`scripts/dev-shell.sh up` 会构建本包并由身份宿主托管，见 [联调环境说明](../../tools/Qian.Dev.Shell/README.md)。`pnpm --filter @qian/identity-ui dev` 在 `http://127.0.0.1:5178/login/` 起开发服务器，把 `/api/identity`、`/connect`、`/.well-known` 代理到本机身份宿主（`QIAN_DEV_SHELL_API_URL`，只许回环地址）。

## 验证

入口边界与完整登录 / 选择 / 切换流程在 [apps/qian-shell](../../apps/qian-shell/README.md) 的真实流程验收里覆盖（`pnpm yue:auth`）；控件级用例在 `packages/yue/shell`。lint、typecheck、分层、循环依赖与构建接入 yue 门禁。

## 已知限制

- 连接器落点未在本机联调环境里实跑（没有登记上游连接器），按身份服务 README 的流程契约实现。
- 验证码登录、忘记密码与 Captcha 为后续范围（同 `@idadawn/yue-shell`）。
