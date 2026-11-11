# staging 首次部署：平台与身份同进程（S1-DEP-011）

「初步可用」里程碑期间（S1-DEC-033 D2，R143），staging 按过渡形状部署：

- 平台宿主 `hosts/Qian.Platform.Host` 以 `Identity:InProcess=true` 并入身份服务与开通申请组件；
- 网关宿主 `hosts/Qian.Gateway.Host` 独立成进程，并托管外壳；
- 清单在部署仓库手写（ADR-017 §10）。

本手册覆盖构建、部署、回退与首个平台运营账号的引导，并列出相对 PRD 7.17 的已知差异。

- 装配方式与取值的裁决：[关键决策](../decisions/key-decisions-v0.md) 的 R150（扩展登记与 overlay、主机名与证书、引导 Job、D4 只开运营开户）与 R151（系统组件修订的签名、授权与免批）。
- 配置键：以 [平台宿主](../../hosts/Qian.Platform.Host/README.md) 与 [网关宿主](../../hosts/Qian.Gateway.Host/README.md) 的文档为准。
- 清单与 Secret 引导脚本：在部署仓库（`idadawn/iclaw-deploy`）的 `2-qian/` 与 `env-ds/qian-staging/` 下，对象名以那里为准。

凡是改动集群状态的操作（apply 或同步、建角色、建 Secret、改网络策略、跑 Job），都要先经产品负责人确认；只读查询不需要。密钥只经 Secret 注入，不进 git。

## 1 形状

| 工作负载 | 镜像 | 说明 |
|---|---|---|
| `qian-platform`（Deployment，1 副本） | `192.168.1.201/qian/qian-platform@sha256:…`（JIT） | 平台插件树加身份分支。平台侧：平台表面与运营接口。身份侧：OIDC 协议面、账号接口、认证页族 `/login`、申请人接口与公开页 `/signup`。前端在镜像的 `/app/web/identity`、`/app/web/signup` |
| `qian-gateway`（Deployment，1 副本） | `192.168.1.201/qian/qian-gateway@sha256:…`（NativeAOT） | BFF 会话、S2S 签发与路由：平台簇与运营簇都指向 `http://qian-platform:8080/`。另有原生审计、外壳与远程托管（`/app/web/shell`、`/app/web/remotes/<组件>`）。配置源 `Config:Source=host` |
| 迁移、系统组件修订、首个运营引导（Job） | 与 `qian-platform` 同镜像、同配置 | `--migrate-only`、`--install-system-revisions`、`--bootstrap-operator`，见第 4 节 |
| 建角色（Job） | PostgreSQL 客户端 | 用基线的管理员 Secret 建受限登录与 NOLOGIN 函数属主 |
| `qian-postgres`、`qian-redis` | 基线（S1-DEP-001） | 已在 `qian-staging` 运行，钉在 worker01 |

主机名（R150 ②）：

- 外壳与网关：`https://qian-staging.deepsingularity.cn`
- 身份发行方：`https://qian-staging-id.deepsingularity.cn/`

公网链路与现有站点相同：浏览器 → hub nginx（43.180.203.135，TLS 在此终止，Let's Encrypt 证书）→ 隧道 → MetalLB `192.168.1.200` → Traefik（按 Host 分流）→ 两个 Service。

**转发头。** Traefik 对 web / websecure 入口信任节点网段与 Pod 网段的转发头（部署仓库 `0-cluster/traefik-helm-config.yaml`，2026-11-12 只读核对过运行参数），hub 设的 `X-Forwarded-Proto: https` 会原样到 Pod。

- 平台宿主：身份服务只收 HTTPS，所以身份分支要从可信代理的转发头恢复原始 scheme 和客户端地址。staging 配 `Identity:TrustedProxyNetworks` = `10.42.0.0/16`（直连的 Traefik Pod）与 `192.168.1.0/24`（Traefik 看到的隧道一跳），`Identity:ForwardLimit=2`。
- 网关：配 `Gateway:TrustedProxyNetworks=["10.42.0.0/16"]`。

**回环与依赖。**

- 网关取发现文档、换码、刷新都走身份发行方的公网地址，要经 hub 绕一圈回来：网关的出口策略要放行 hub 公网地址的 443。
- 平台取网关的 JWKS 走集群内的 `http://qian-gateway:8080/.well-known/jwks.json`（`S2S:AllowHttpJwks=true`）。
- 平台不连 Redis：环境 overlay 把 events 条目的配置置空，Outbox 用 skip-locked、不带 broker。网关连 Redis（会话、限流）与 PostgreSQL（原生审计）。

## 2 一次性前置（产品负责人）

1. DNS：两个主机名的 A 记录指向 43.180.203.135。
2. Harbor：
   - 建私有项目 `qian`；
   - 给 qd 一个推送账号（qd 上执行 `docker login 192.168.1.201`）；
   - 建一个只读 `qian` 项目的机器人账号，凭据建成 `qian-staging` 里的 `kubernetes.io/dockerconfigjson` Secret（名称见部署仓库清单）。项目是私有的，而节点的 `registries.yaml` 只给了 HTTP 端点、没有凭据，这一点与公开的 `iclaw` 项目不同。
3. hub 证书：DNS 生效后，按部署仓库 `env-ds/edge/qdal/` 的流程签发两个主机名的证书，再部署两个 vhost。hub 上的部署脚本先 `nginx -t`、自带备份，失败会回滚。
4. 系统组件修订的签名钥（R151 ①）。在 qd 上生成一次，私钥只留在 qd，不进集群、不进 git：
   ```bash
   install -d -m 700 ~/.qian
   openssl genpkey -algorithm ed25519 -out ~/.qian/staging-system-revisions.pem
   chmod 600 ~/.qian/staging-system-revisions.pem
   openssl pkey -in ~/.qian/staging-system-revisions.pem -pubout -out ~/.qian/staging-system-revisions.pub.pem
   ```
   公钥建进 Lifecycle 信任根的 Secret（第 4 节第 1 步）。平台宿主固定把 `secretRef://platform/trust/platform` 解析到 `<Secrets:MountRoot>/qian/trust-platform/public-key`。

## 3 构建镜像与签名清单

镜像在 qd 上构建：qd 是 amd64、有 Docker、到得了 Harbor，而 Mac 到不了 192.168.1.x。qd 没有 Node，前端在开发机上构建好再拷过去。构建入口是 [scripts/images.py](../../scripts/images.py) 的 `build`，模板与构建上下文见 [deploy/images](../../deploy/images/README.md)。

1. 前端（开发机，Qian 仓库根目录，检出要构建的提交）：
   ```bash
   pnpm install --frozen-lockfile
   ```
   ```bash
   pnpm exec turbo run build --filter=qian-shell --filter=@qian/identity-ui --filter=@qian/tenancy-ui --filter=@qian/tenancy-signup-ui
   ```
   ```bash
   pnpm --filter @qian/tenancy-ui run build:remote
   ```
   ```bash
   pnpm --filter @qian/tenancy-signup-ui run build:remote
   ```
   组装两个目录，分别作为两个镜像的 `--web`：

   | 目录 | 内容 | 来源 |
   |---|---|---|
   | `web-platform/identity` | 认证页族（base `/login/`） | `packages/qian-identity-ui/dist` |
   | `web-platform/signup` | 开通申请公开页（base `/signup/`） | `packages/qian-tenancy-signup-ui/dist/public` |
   | `web-gateway/shell` | 外壳 | `apps/qian-shell/dist/host` |
   | `web-gateway/remotes/qian.tenancy` | 租户运营远程 | `packages/qian-tenancy-ui/dist/remote` |
   | `web-gateway/remotes/qian.tenancy.signup` | 开通申请远程 | `packages/qian-tenancy-signup-ui/dist/remote` |

2. 把源码与前端制品送到 qd。用 `git bundle` 送源码，是为了保留提交历史：构建证据要记源码 SHA。
   ```bash
   git bundle create qian.bundle <分支>
   ```
   ```bash
   rsync -a qian.bundle web-platform web-gateway qd:qian-build/
   ```
   ```bash
   ssh qd 'cd qian-build && git clone -b <分支> qian.bundle qian'
   ```
3. 构建与推送（qd 的 `~/qian-build/qian`）。`build` 构建完即推送到标签所在的仓库，再从推送结果取 digest：
   ```bash
   python3 scripts/images.py build --kind jit --project hosts/Qian.Platform.Host/Qian.Platform.Host.csproj --assembly Qian.Platform.Host --tag 192.168.1.201/qian/qian-platform:<短 SHA> --web ../web-platform
   ```
   ```bash
   python3 scripts/images.py build --kind aot --project hosts/Qian.Gateway.Host/Qian.Gateway.Host.csproj --assembly Qian.Gateway.Host --tag 192.168.1.201/qian/qian-gateway:<短 SHA> --web ../web-gateway
   ```
   - 每条命令打印一段 JSON：`ref` 是按 digest 的引用，`build` 是构建证据（源码 SHA、KunVersion、profile 与组件目录修订、`webDigest`）。两段都记进任务进度日志。
   - 推送失败时命令以非零退出、不打印 JSON，镜像留在本机；修好仓库权限后重跑，BuildKit 缓存让重跑很快。
   - 清单只按 `ref` 里的 digest 引用镜像，标签只用来推送。部署仓库里 digest 只写在 `env-ds/qian-staging/kustomization.yaml` 的 `images` 段这一处。
4. 签名清单（qd）。对推送后的同一个 digest 生成签名清单并自查，结果放进部署仓库的签名清单 ConfigMap：
   ```bash
   python3 scripts/sign-system-revisions.py sign --key ~/.qian/staging-system-revisions.pem --image 192.168.1.201/qian/qian-platform@sha256:<digest> --out system-revisions.json
   ```
   ```bash
   python3 scripts/sign-system-revisions.py verify --public-key ~/.qian/staging-system-revisions.pub.pem --signatures system-revisions.json --image 192.168.1.201/qian/qian-platform@sha256:<digest>
   ```
   签名清单是公开数据（组件标识、版本、目录摘要与签名），可以进 git。平台镜像里的组件文件夹一变，摘要就变，所以每换一次平台镜像都要重签。

## 4 部署顺序

部署仓库的 PR 由产品负责人合并，之后逐步执行；每一步都先确认再执行。每步做完用只读命令检查：
```bash
kubectl -n qian-staging get jobs,pods -o wide
```
```bash
kubectl -n qian-staging logs job/<Job 名>
```

1. **Secret。** 部署仓库的 `scripts/bootstrap-qian-secrets.py --context <上下文> --namespace qian-staging --create-missing` 只建缺失项，不覆盖、不打印值。它生成：
   - 新登录的口令与连接串文件；
   - 网关 S2S 的私钥与公钥；
   - 官方客户端（网关 BFF）的密钥；
   - 身份服务的签名、加密证书与私钥（RSA）；
   - 首个平台运营的初始密码。

   Lifecycle 信任根的公钥由运维从 qd 导出后传给脚本，脚本不生成私钥。Harbor 拉取 Secret 另建（第 2 节）。
2. **建角色 Job。** 用基线的管理员 Secret 建以下角色，口令取自第 1 步：
   - 迁移属主：可在库上建 schema；
   - 两个平台登录：`qian_platform_authz`、`qian_platform_audit`；
   - 网关原生审计的两个登录；
   - 两个 NOLOGIN 的审计函数属主。

   应用登录 `qian_app` 由基线初始化。受限登录都是 `NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION`，函数属主没有任何成员关系。可重复执行：已存在的角色只校正属性与口令。
3. **迁移 Job（`--migrate-only`）。** 三步，都可重复执行：
   1. 管理员连接做离线 DDL 与授权，包括网关原生审计的离线迁移；
   2. 迁移属主执行包内迁移；
   3. 管理员把业务 schema 授给应用登录。

   在线副本不挂迁移属主与管理员的凭据。离线 DDL 不做跨版本的表结构升级，详见[平台宿主的 Job](../../hosts/Qian.Platform.Host/README.md#job)。
4. **系统组件修订 Job（`--install-system-revisions`）。** 每次换平台镜像都要跑，而且在迁移之后。
   - 它按签名清单把镜像预装的 system 级组件装成修订并激活：同摘要已激活则不动，新摘要安装后激活。
   - 签名清单缺项、验签失败或摘要对不上，都会列出组件并以退出码 1 失败。此时先核对 ConfigMap 里的清单是否对应当前镜像 digest。
5. **首个运营引导 Job（`--bootstrap-operator`，只在首次）。** 见第 5 节。
6. **在线副本。** 依次是 `qian-platform`、`qian-gateway` 两个 Deployment，都用 Recreate、钉在 worker01，`/readyz` 返回 `ready` 后才就绪。
   - 平台在线副本启动时只校验（受限角色、模型、没有待执行的迁移），不建表；迁移没跑或没跑完就拒绝启动。
   - 网关在登录时才取身份发行方的发现文档（之后定期刷新），不阻塞就绪；平台就绪、Ingress 与 hub vhost 生效之前，登录会失败。
7. **入口。** 两个主机名的 Traefik Ingress 随清单下发；hub vhost 按第 2 节第 3 步部署。

## 5 首个平台运营账号

引导 Job 只在库里还没有平台超管时执行（R150 ③）：

1. 经身份账号端口建平台身份，用户名与显示名来自清单，初始密码来自引导 Secret，并标记首登必须改密；
2. 在平台角色锁里授平台超管；
3. 写一条平台审计链记录（操作者 `platform-bootstrap`，通道 `job`）。

库里已有平台超管时 Job 拒绝执行（退出码 1），不能拿它二次提权。上次在授权之前失败时，同名身份沿用，不重置密码。

引导后：

1. 平台运营打开 `https://qian-staging.deepsingularity.cn`，跳到身份发行方的 `/login`，用初始密码登录，按提示改密；
2. 回到外壳，进平台控制台（租户运营、开通申请）；
3. 确认能登录后删除初始密码的 Secret 与引导 Job。之后的平台超管授予与撤销走平台运营流程。

## 6 部署后检查

逐条对应任务验收，结果记进 `pm/tasks/S1-DEP-011.md` 的进度日志。

E2E 部分由 [scripts/staging-e2e.py](../../scripts/staging-e2e.py) 经公网主机名、照外壳的真实路径跑一遍，在 qd 上执行。证据 JSON 进进度日志，测试账号与租户留在 staging，代码带运行后缀：
```bash
python3 scripts/staging-e2e.py --shell https://qian-staging.deepsingularity.cn --issuer https://qian-staging-id.deepsingularity.cn --operator <平台运营用户名> --operator-password-file <当前密码文件> --operator-new-password-file ~/.qian/staging-operator-password --out staging-e2e.json
```
- 平台运营若还没首登，脚本按要求改密：新密码生成后写进 `--operator-new-password-file`（0600，不打印），之后用这个文件登录。
- 首登完成后，删除初始密码的 Secret（第 5 节）。

- **就绪与镜像。** 只读确认以下几项，并核对 Deployment 和 Job 里没有 `:latest`：
  - 两个 Pod Ready；
  - 镜像按 digest 引用；
  - `runAsNonRoot` 且 UID 非 0；
  - `readOnlyRootFilesystem: true`。
- **登录、开户与租户隔离（E2E）。**
  1. 平台运营经外壳登录进平台控制台；
  2. 新建管理员账号，开户并激活一个租户；
  3. 该租户管理员登录、选租户后进工作台；
  4. 另开一个租户，它的管理员读第一个租户的摘要得到 403。

  同一流程在集成测试 `PlatformIdentityHostTests` 里对生产入口跑通。
- **自助申请（D4，R150 ④）。** `POST /api/signup/verifications` 返回 503 `SIGNUP_UNAVAILABLE`，运营开户照常。staging 没有发件实现，申请到审核通过这一段由集成测试覆盖。
- **没有开发期内容。**
  - `/__dev/` 下的路由都不是 200；
  - 平台 profile 没有开发种子，也没有开发期发件箱；
  - 运行期连接串只用受限登录（`rolsuper`、`rolbypassrls` 都为假）；
  - 密钥只以 Secret 卷挂载，ConfigMap 与环境变量里没有凭据。
- **迁移、审计链与超管旁路。**
  - Job 完成时间早于在线副本启动；
  - 平台宿主日志里审计与授权条目已就绪；
  - 运营接口的超管旁路放行后，平台审计链出现对应记录（`qian.authz` 的平台链）。

## 7 回退

- **只换回应用版本。** 在部署仓库把 `images` 段的 digest 改回上一版（git revert 那次写回），合并后同步；然后对上一版平台镜像重跑系统组件修订 Job。旧修订仍在 Lifecycle 里，Job 会把它切回激活。
  - 迁移只向前：上一版能跑在新表结构上时才能直接回退。
  - 离线 DDL 不做跨版本升级，所以首个版本之后凡是改表结构的部署，都要在迁移 Job 前先备份：在 `qian-postgres` Pod 里用 `pg_dump`，备份落在 qd 之外。
- **撤掉整套应用。** 停止同步，再 revert 部署仓库里本任务新增的应用清单：两个 Deployment、Job、ConfigMap、Ingress、应用的网络策略。按基线手册，Namespace、PVC 与基线 Secret 一律不删。hub 上撤掉两个 vhost 时，用部署脚本的备份回滚。
- **数据恢复。** 用迁移前的 `pg_dump` 恢复；没有备份时只能清库重来（staging 可以接受，但要先经产品负责人确认）。
- **签名钥泄露或丢失。** 在 qd 上重新生成钥对，换掉信任根公钥的 Secret，对当前镜像重签并重跑系统组件修订 Job。旧修订的签名不会被重新核验；正式的信任根轮换归 S1-DEP-005。

## 8 相对 PRD 7.17 的已知差异

这些差异都是过渡形状的取舍，不计入 7.17 的验收，由 S1-DEP-005 / S1-DEP-008 收口。

| 方面 | PRD 7.17 | staging 首次部署 |
|---|---|---|
| 镜像形状 | 三个 profile 镜像：`qian-platform`（JIT，含 `shell` 与 `model-gateway` 组合包）、`qian-identity`（JIT）、`qian-gateway`（AOT distroless） | 两个镜像：`qian-platform`（JIT，平台加身份分支，认证页族与公开页在 `/app/web`）与 `qian-gateway`（NativeAOT）；没有 `qian-identity` 镜像 |
| 外壳制品 | 随 `qian-platform` 镜像发布、经网关提供 | 直接打进 `qian-gateway` 镜像（`/app/web/shell` 与远程），由网关托管 |
| 供应链 | SBOM（Syft）、漏洞扫描、Cosign 签名，镜像与插件包推 Harbor | 只构建并推送 Harbor，按 digest 引用；没有 SBOM、扫描与 Cosign 签名（S1-DEP-008） |
| 发布链 | 不可变发布事件 → 验签 → 构建推送 → 迁移 Job 先行 → `kustomize edit set image` 写回 → Argo 收敛 → smoke，中间无人工步骤 | qd 上手工构建；digest 经部署仓库 PR 手工写回；Job 与同步逐步人工确认执行；smoke 人工跑 |
| 部署物料 | `qian pack` 从清单与 profile 生成 Kustomize 片段 | 部署仓库手写（ADR-017 §10） |
| 配置 | Apollo（引导项由 Kustomize 注入），环境 overlay 由 ConfigMap 挂载 | 不连 Apollo：网关 `Config:Source=host`，平台读宿主配置根（`qian.config`）；环境 overlay 照旧由 ConfigMap 挂载 |
| 消息与依赖 | RabbitMQ（quorum queue）、Apollo、MySQL；四依赖从真实宿主验收 | 只有 PG 与 Redis；events 不带 broker，Outbox 用 skip-locked |
| 迁移 | 迁移 Job 先行，取 advisory lock | 迁移 Job 先行，由单实例 Job 保证串行；Tenancy、Authz、Audit、Registry 由离线 DDL 首装，不做跨版本升级 |
| 系统组件信任根 | 发布链签名 | staging 专用 Ed25519 钥，私钥只在 qd（R151 ①）；签名清单经 ConfigMap 交给安装 Job |
| 身份部署 | 独立 `identity` profile 与进程 | 与平台同进程（R143）；授权变更经进程内端口递增身份修订（S1-DEC-027） |
| 高可用 | `qian-platform` 至少两副本、PDB、kill 一个副本不漏 5xx | 每个 Deployment 一个副本，Recreate；数据保护密钥在临时卷，重启要重新登录 |
| 自助申请 | 发件实现可用 | D4：只开运营开户，发验证码 503（R150 ④） |
| 配置漂移门禁 | 镜像内 `qian --dump-config` 与 git 合成结果一致 | 本次不跑（随 S1-DEP-008） |

## 9 已知限制

- 平台与网关的数据保护密钥都放在临时卷：Pod 重建后，账号 Cookie 与网关会话解不开，用户要重新登录。多副本之前要改成共享目录。
- 网关的转发头只回溯一跳（Kun 网关 `ForwardLimit` 固定为 1），按 IP 的限流把全部外部请求算在隧道那一跳的地址上。staging 用户少，暂不处理。
- LAN 内（192.168.1.x）的主机也在 Traefik 的可信网段里，能伪造转发头直连 Traefik；staging 接受这个暴露面。
- K3s 的 Secret 静态加密（`--secrets-encryption`）状态本次没有核对（需要 root）。
- 没有装配 OpenAPI / proto 兼容检查器，组件跨版本升级会拒绝执行（fail-closed）；换镜像只能是同版本组件的重签与重装。
