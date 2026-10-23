# 外壳认证联调环境

一条命令在本机拉起真实的 qian-identity、业务运行时与网关 BFF，再用浏览器走完整的外壳认证流程：OIDC code + PKCE 登录、多租户选择、切换租户、令牌到期与刷新、运行时配置注入。任务见 [S1-YUE-002](../../pm/tasks/S1-YUE-002.md)；真实流程验收（Playwright）的入口在 [apps/qian-shell-auth](../../apps/qian-shell-auth/README.md)。

## 这是什么，不是什么

这是开发机上的联调环境，装配照搬租户运营手测宿主（[Qian.Dev.TenantOps](../Qian.Dev.TenantOps/README.md)，S1-QA-012）与集成测试已验收的形状，并复用它的开发库、平台插件树与运营前置：

- **身份 / 业务宿主**（缺省 `http://127.0.0.1:5090`）：CRM 参考命令宿主充当运行时容器，同一宿主上挂真实 `qian.identity.server`（内置账号模型、OpenIddict 协议面、账号 HTTP 接口），身份请求走自己的分支；同源静态托管认证页族 [packages/qian-identity-ui](../../packages/qian-identity-ui/README.md) 的构建产物（`/login`、`/login/select-tenant`、`/login/connector/{connectorId}`），账号 Cookie 的登录页指向 `/login`。它就是 OIDC 发行方（协议库只在开发环境允许回环明文 HTTP）。
- **平台插件树 + 参考应用**：Data / Tenancy / Authz / Audit / Config / Registry / Capabilities / Lifecycle 都是实际 Provider；参考应用（qian.sample.crm）作为 system / required 组件制品一起经 Lifecycle 签名安装、激活，租户开通第 03 步经 Lifecycle 为每个租户开通它（开通只登记安装并执行 tenant-provision 钩子，不挂载），种子随后把条目 `crm` 挂进租户 scope（与运营「挂载组件」等价），能力导航目录因此出现它 `ui/manifest.json` 声明的「客户」「新建客户」两页（分别要求 `customer:read` / `customer:write`）。
- **种子**：按 [seed/accounts.json](seed/accounts.json) 建自然人（用户名 + 初始密码，不要求首登改密），经真实开通编排建租户（第 05 步经身份的成员开户端口建管理员成员，第 07 步授予 tenant.owner），随后激活并挂载参考应用；第二租户的额外成员与 tenant.user 授权由种子直接补齐。缺省：`demo` 是甲方科技（acme）的管理员、乙方贸易（beta）的普通成员，`second` 是乙方管理员，`operator` 是平台身份。
- **平台表面宿主**（随机回环端口）：照参考宿主的 TenantOperationsHost，复用同一运行时，只认网关 S2S（受众 `qian-platform`），接受用户主体并按令牌 `tenant_id` 进入租户 scope，提供能力目录 / 导航（`/api/capabilities/**`）与有效权限（`/api/authz/**`）。参考应用的命令宿主只认机器身份（service / agent），用户主体的这两类表面因此由平台宿主提供——与网关 S2S 的受众分簇一致。
- **网关 BFF**（缺省 `https://localhost:5443`，自签名证书）：同一份 Identity.Client / Qian.Gateway 生产入口——`/auth/login` 发起 code + PKCE、服务端换码、`qian.sid` HttpOnly Cookie、refresh 单飞；`/api/**` 经会话换 60 秒 S2S 按前缀分簇转发（能力 / 授权表面 → 平台宿主，其余 → 业务宿主）；静态托管外壳 [apps/qian-shell-auth](../../apps/qian-shell-auth/README.md) 的构建产物；`/app-conf.js` 取自状态目录（K8s ConfigMap 的本机替身）。浏览器只持有 Cookie，不接触任何令牌。

它不是：`qian dev`（S1-RT-013）、staging / K3s 部署（S1-DEP-002 / 005）、产品契约（下文 `/__dev/` 路由只在本环境存在）。

## 前置

- Docker 引擎在运行；.NET 10 SDK（版本见 `global.json`）；Node 24 与 pnpm（`package.json` 的 `packageManager`）；curl 与 python3；
- 本机端口：PostgreSQL `127.0.0.1:55433`，身份 / 业务宿主 `127.0.0.1:5090`，网关 `localhost:5443`。被占用时用下文环境变量改。

## 三步用起来

1. 起环境（先构建两个前端制品与宿主，再重建开发库），看到「外壳认证联调环境已就绪」即可：

   ```bash
   scripts/dev-shell.sh up
   ```

2. 浏览器打开 <https://localhost:5443/>（接受自签名证书，或导入状态目录里的 `gateway/tls.crt`）。外壳发现没有会话就跳到身份服务的同源登录页；用 `demo` 与种子文件里的密码登录，选择租户后回到工作台；用户菜单里可以切换租户、退出登录。

3. 用完 Ctrl+C 停宿主，再删容器：

   ```bash
   scripts/dev-shell.sh down
   ```

真实流程验收（自起一套独立环境，结束后清理）：

```bash
scripts/dev-shell.sh e2e
```

## 运行时配置

外壳的 `index.html` 在模块脚本之前以普通脚本加载同源的 `/app-conf.js`，它定义 `window.__APP_CONF__`（`apiBase`、`authBase`、`brand`、`locale`、`managed`，形状与校验见 `@idadawn/yue-shell` 的 `readAppConf`）。本环境把它映射到状态目录的 `app-conf.js`（首次启动写缺省值，之后不覆盖），响应 `no-store`。把 `apiBase` 改成 `/api-alt` 再刷新页面，请求就全部走第二个 API 前缀（网关把 `/api-alt/**` 改写回 `/api/**` 转发），构建产物不变——这就是验收 4 的本机形态。

## 开发路由

产品契约里没有这两个端点，只对已认证的本会话生效，且仍要带防伪令牌（`GET /auth/csrf` 取头名与值）：

| 方法 | 路径 | 用途 |
|---|---|---|
| POST | `/__dev/session/expire` | 把当前会话的服务端访问令牌改成已到期：下一批请求由依赖方透明刷新（单飞），页面无感 |
| POST | `/__dev/session/break` | 同时毁掉 refresh token：依赖方刷新必败 → 会话结束 → 并发 401 被 SDK 合并成一次刷新尝试（会话已不在，取防伪令牌即 401、不再 POST）→ 只弹一次过期对话框 |

## up 做了什么

1. 起容器 `qian-dev-shell-pg`（`postgres:16-alpine`），只绑 `127.0.0.1`；管理员口令随机生成，存在 `.qian-dev/shell/pg-admin-password`，仅本人可读。
2. 构建 `packages/qian-identity-ui` 与 `apps/qian-shell-auth`（`QIAN_DEV_SHELL_SKIP_UI_BUILD=1` 跳过），构建本工程。
3. 重建开发库 `qian_dev` 与受限角色（同 Qian.Dev.TenantOps），用生产同一套迁移建身份库并授权给应用登录；Capabilities / Lifecycle 由迁移属主执行包内迁移。
4. 启动身份 / 业务宿主，装平台插件树与参考应用制品，挂载身份组件，引导平台超管并经 Lifecycle 安装、激活全部 system 组件，按种子建账号与租户。
5. 启动平台表面宿主；生成网关材料（S2S 签名钥、外壳客户端密钥、TLS 证书，都在 `.qian-dev/shell/gateway/`，仅本人可读）并启动网关 BFF。

每次 `up` 都从空库开始，数据不跨次保留；会话存储是进程内的，重启后要重新登录。

## 环境变量

| 变量 | 默认值 | 作用 |
|---|---|---|
| `QIAN_DEV_SHELL_API_URL` | `http://127.0.0.1:5090` | 身份 / 业务宿主地址，即 OIDC 发行方；只许回环 |
| `QIAN_DEV_SHELL_URL` | `https://localhost:5443` | 网关 BFF 地址（外壳地址）；只许回环 |
| `QIAN_DEV_PG_PORT` | `55433` | PostgreSQL 映射到本机的端口 |
| `QIAN_DEV_PG_CONTAINER` | `qian-dev-shell-pg` | 容器名 |
| `QIAN_DEV_PG_IMAGE` | `postgres:16-alpine`，未设时沿用 `QIAN_TEST_IMAGE_POSTGRES` | PostgreSQL 镜像 |
| `QIAN_DEV_STATE` | `.qian-dev/shell` | 本机状态目录（口令、网关密钥、`app-conf.js`） |
| `QIAN_DEV_SHELL_SKIP_UI_BUILD` | 未设 | 设为 `1` 时 `up` 不重复构建前端制品 |
| `QIAN_OASDIFF_PATH` | 未设 | 设了就接入契约检查器（只在组件升级准入时调用） |

## 常见问题

- **浏览器提示证书不受信任**：网关是自签名 HTTPS，接受即可；Playwright 以 `ignoreHTTPSErrors` 运行。
- **登录后菜单为空**：看身份 / 业务宿主终端里的授权拒绝日志；平台身份（`operator`）没有租户，外壳显示代管横幅且不拉菜单。
- **改了 `app-conf.js` 没生效**：确认改的是 `QIAN_DEV_STATE` 指向的那个文件，再强制刷新；响应是 `no-store`，不会被缓存。
- **端口被占用**：设 `QIAN_DEV_SHELL_API_URL` / `QIAN_DEV_SHELL_URL` / `QIAN_DEV_PG_PORT`；各个子命令要用同一组变量。
