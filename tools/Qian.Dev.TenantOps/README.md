# 本机租户运营手测环境

一条命令在本机起 PostgreSQL 与租户运营宿主，再用开发令牌按顺序调用 S1-TEN-006 / 002 / 004 交付的运营 HTTP。不用写代码，也不用等页面、staging 或真实登录。任务见 [S1-QA-012](../../pm/tasks/S1-QA-012.md)。

## 这是什么，不是什么

这是开发机上的手测环境。宿主装配照搬集成测试已验收的形状：

- 真实 Kestrel，JwtBearer 只收 RS256；
- 运行期连接全部是受限 PostgreSQL 角色（NOSUPERUSER、NOBYPASSRLS）；
- Authz 用专用平台登录，Audit 用平台链；
- 平台组件制品经 Lifecycle 签名校验后安装、激活；
- 调用的是与产品相同的 [TenantOperationsHost](../../samples/Qian.Sample.Crm/TenantOperationsHost.cs) 与 `MapQianTenancy`。

它不是：

- `qian dev`：正式的本地编排归 S1-RT-013，那是 Aspire AppHost，会同时拉起 PG / Redis / RabbitMQ / Apollo；
- 部署：staging 与 K3s 归 S1-DEP-002 / 005；
- 登录：开发令牌由本机私钥签发，不代表 Identity 已签发或登录（S1-IDP-002 / 003）；
- 页面：运营后台页面归 S1-TEN-005；
- 产品契约：下文两条 `/__dev/` 开发路由只存在于本手测宿主。

## 前置

- Docker 引擎在运行；
- .NET 10 SDK，版本见仓库根目录 `global.json`；
- curl 与 python3（macOS 自带 `/usr/bin/python3` 即可）；
- 两个本机端口：PostgreSQL 用 `127.0.0.1:55432`，运营宿主用 `127.0.0.1:5080`。被占用时用下文环境变量改。

## 三步用起来

1. 起环境，终端保持运行，看到「租户运营手测宿主已就绪」即可：

   ```bash
   scripts/dev-tenant-ops.sh up
   ```

2. 另开一个终端，按顺序走一遍。每步先打印等价的 curl 命令，再打印响应并核对状态码，回车进入下一步：

   ```bash
   scripts/dev-tenant-ops.sh walkthrough
   ```

   顺序是：建租户 → 开通步骤 → 激活 → 写设置 / 读配额 → 冻结 / 恢复 → 扫描到期 → 权限反例。加 `--yes` 一口气跑完；任一检查不符时退出码为 1。

3. 用完在第一个终端按 Ctrl+C 停宿主，再删容器：

   ```bash
   scripts/dev-tenant-ops.sh down
   ```

## 单独调用某个接口

以平台超管身份调用一次，响应体自动缩进：

```bash
scripts/dev-tenant-ops.sh api GET /api/tenancy/tenants
```

带请求体时把 JSON 作为第三个参数，路径里的冒号要连同路径一起加引号：

```bash
scripts/dev-tenant-ops.sh api POST '/api/tenancy/tenants/<租户 id>:activate' '{"validUntil":"2026-12-31T00:00:00Z"}'
```

想用自己的 curl 或 HTTP 客户端，先取令牌：

```bash
export TOKEN=$(scripts/dev-tenant-ops.sh token)
```

令牌默认 8 小时有效。`token` 还能签别的身份，用来看 403：

- `--sub intruder`：不是平台超管的用户；
- `--tenant <租户 id>`：带 `tenant_id` 的租户主体；
- `--kind service`：服务主体；
- `--ttl 60`：有效期，单位分钟。

## 接口一览

运营契约 [http-v1.openapi.json](../../plugins/qian.tenancy/contracts/http-v1.openapi.json) 的 8 个路径、11 个操作都在走查里，另有两条开发路由：

| 方法 | 路径 | 用途 | 走查步骤 |
|---|---|---|---|
| GET | `/api/tenancy/tenants?skip=&take=` | 租户列表（take 1–100，默认 50） | 1.1、7.x |
| POST | `/api/tenancy/tenants` | 新建，同步跑开通编排，返回 201 与 pending | 1.2 |
| GET | `/api/tenancy/tenants/{id}` | 详情 | 1.3、6.2 |
| PUT | `/api/tenancy/tenants/{id}` | 改名、改配额，需带 `expectedRevision` | 1.4、1.5 |
| POST | `/api/tenancy/tenants/{id}:activate` | 激活（`trial`、`validUntil`） | 3.2、6.3 |
| POST | `/api/tenancy/tenants/{id}:suspend` | 冻结 | 5.1 |
| POST | `/api/tenancy/tenants/{id}:resume` | 恢复到冻结前的状态 | 5.2 |
| GET | `/api/tenancy/meta/state-machine` | 状态与合法迁移 | 3.1 |
| GET | `/api/tenancy/tenants/{id}/settings/{plugin}` | 读租户设置 | 4.1、4.4 |
| PUT | `/api/tenancy/tenants/{id}/settings/{plugin}` | 写租户设置，按组件 configSchema 校验 | 4.2、4.3 |
| GET | `/api/tenancy/tenants/{id}/quota` | 配额与用量 | 4.5 |
| GET | `/__dev/tenancy/tenants/{id}/provision-steps` | 开发路由：开通步骤表 | 2.1 |
| POST | `/__dev/tenancy/scan-expired?limit=` | 开发路由：代调度器触发到期扫描 | 6.1 |

## 两条开发路由

产品契约里没有这两个端点：

- 开通步骤表：读 `ITenantProvisioning.GetStepsAsync`。这个内部端口只接受无租户上下文，不对外映射；
- 到期扫描：调用运行时能力 `qian.tenancy/tenant.scan-expired`。它只收 global `qian.tenancy` 或 `qian.scheduler` 的可信调用，准点触发归调度器（S1-SCH-002）。手测宿主让超管手动代调度器以 global `qian.tenancy` 身份调用一次。

授权与运营端点相同：已验签的平台用户主体（无 `tenant_id`、无委托），再经平台超管显式旁路，写一条 `bypass=superadmin` 的平台审计。读步骤用权限点 `qian.tenancy:tenant:read`，扫描用 `qian.tenancy:tenant:scan`。

## up 做了什么

1. 起容器 `qian-dev-tenant-ops-pg`（`postgres:16-alpine`，与集成测试同一镜像），只绑 `127.0.0.1`。管理员口令在本机随机生成，存在 `.qian-dev/tenant-ops/pg-admin-password`，仅本人可读。
2. 构建本工程，删掉并重建开发库 `qian_dev` 与六个 `qian_dev_*` 角色：
   - 通用应用登录与 CRM 业务登录；
   - 审计函数属主与迁移属主；
   - Authz、Audit 各一个专用平台登录。

   运行期登录的口令每次随机生成，只在宿主进程内存里。
3. 建表：没有包内迁移的上下文用 EF 建表脚本；Capabilities 与 Lifecycle 由迁移属主在 advisory lock 内执行包内迁移；再写入审计受控入口与平台登录的授权。
4. 装平台插件树：Data、Tenancy、Authz、Audit、Config、Registry、Capabilities、Lifecycle。组件文件夹写进临时 platform profile，制品摘要用本次启动生成的 Ed25519 平台钥签名；公钥按生产形状放进 K8s Secret 挂载目录，经 `secretRef` 引用。
5. 引导平台超管 `root`，经 Lifecycle 安装并激活全部 system 组件制品，然后启动运营宿主。

CRM 命令宿主只充当运行时容器，监听随机本机端口，手测流程不用它。

## 数据与密钥

- 每次 `up` 都从空库开始，数据不跨次保留；同一次 `up` 里可以反复跑走查，每次新建一个租户。
- 开发签名私钥在 `.qian-dev/tenant-ops/ops-signing-key.pem`，首次使用时生成，仅本人可读，不打印。整个 `.qian-dev/` 被 git 忽略。
- 令牌只被本机运营宿主接受：`iss=qian-dev-local`，`aud=qian-dev-tenant-ops`。
- `down --purge` 连同私钥与口令一起删除，下次 `up` 重新生成。

## 常见问题

- **403 看不出原因**：按设计，拒绝原因只进宿主日志，响应统一是 `AUTHZ_DENIED`。看 `up` 那个终端，例如 `not_superadmin`。
- **激活返回 400**：`validUntil` 必须晚于当前时间。
- **想让到期来得更快或更慢**：走查默认激活 30 秒后到期，用 `QIAN_DEV_VALID_SECONDS` 调整。
- **端口被占用**：设 `QIAN_DEV_OPS_URL` 或 `QIAN_DEV_PG_PORT`。`up`、`walkthrough`、`api` 要用同一组变量。
- **系统代理**：脚本里的 curl 都带 `--noproxy '*'`，只访问本机。

## 环境变量

| 变量 | 默认值 | 作用 |
|---|---|---|
| `QIAN_DEV_OPS_URL` | `http://127.0.0.1:5080` | 运营宿主监听地址 |
| `QIAN_DEV_PG_PORT` | `55432` | PostgreSQL 映射到本机的端口 |
| `QIAN_DEV_PG_CONTAINER` | `qian-dev-tenant-ops-pg` | 容器名 |
| `QIAN_DEV_PG_IMAGE` | `postgres:16-alpine`，未设时沿用 `QIAN_TEST_IMAGE_POSTGRES` | PostgreSQL 镜像 |
| `QIAN_DEV_STATE` | `.qian-dev/tenant-ops` | 本机状态目录（私钥、口令） |
| `QIAN_DEV_VALID_SECONDS` | `30` | 走查里激活后多少秒到期 |
| `QIAN_OASDIFF_PATH` | 未设 | 设了就接入契约检查器；它只在组件升级准入时调用，手测流程用不到 |
