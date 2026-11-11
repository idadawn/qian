# platform 生产宿主

入口是本目录 `Program.cs`，可执行项目为 `Qian.Platform.Host.csproj`，是 platform profile 的生产启动项目（S1-DEP-009），随 qian-platform 镜像发布。它按仓库交付的 platform profile 装配平台插件树，只认网关签发的 S2S，承载面向用户的平台表面（能力目录、导航、外壳目录、平台控制台目录、有效权限）与平台运营接口（租户运营）。组件的契约与安全行为以各组件的主文档为准，本宿主只做组合。staging 首次部署（S1-DEP-011，R143 / R150）用同一宿主并入身份分支：配置 `Identity:InProcess=true` 后，身份服务与开通申请组件进同一插件树，见下文「同进程身份分支」；不配置时宿主保持 S1-DEP-009 的形状。

## 装配

`Program.cs` 只调用 `PlatformHost.RunAsync`。装配拆成可组合的四步，后续宿主任务经扩展登记复用，不改 `Program.cs`：

1. `CreateBuilder`：配置源依次是 `appsettings.json`（及环境名文件）、`QIAN_` 前缀环境变量、命令行；内容根是程序目录。
2. `AddQianPlatform`：读设置、收扩展登记、合成 profile、登记宿主 DI 与插件树运行时。
3. `UseQianPlatform`：ErrorDetail → 网关 S2S 认证 → 按令牌 `tenant_id` 进入租户 scope（平台身份不进任何租户）→ 授权 → 插件端点可见性 → 授权 PEP（生产模式）。
4. `MapQianPlatform`：健康端点、平台表面、运营接口与扩展端点。

profile 的合成顺序是：platform profile 的组合包（base / shell / model-gateway）→ 宿主 overlay [`platform.overlay.yml`](platform.overlay.yml) → 扩展 overlay → 环境 overlay（`--patch <文件>`，可重复，或 `Profile:Patches`）。宿主 overlay 只做两件与环境无关的事：关掉本宿主没有 Provider 的条目，以及让 data 条目不带 `connection`（本宿主按登录分别绑定数据上下文）。config 条目暂用读宿主配置根的 `qian.config`，Apollo 联调归 S1-DEP-005。

合成后的每个启用条目都必须在宿主目录里有 Provider（内置目录见 `PlatformComponents.BuiltIn`，扩展经 `AddComponent` 登记），且组件清单的 `runtime.entry` 与目录项一致；没有 Provider 的启用条目让宿主拒绝启动，不会静默跳过。每个条目的组件文件夹就是它的预装制品：

- 第一遍按清单声明校验组件文件夹，用 metadata 编译权限目录，交给 Authz 的 Provider。
- 第二遍用实际 Provider 再校验一次（清单 `inject` 必须等于 Provider 的依赖，清单 `provides` 必须都在定义里），得到交给 Registry 的预装绑定。命令型工具的绑定只认组件代码派生的能力（入口程序集里 SDK 生成的元数据），不在其中即拒绝。
- 声明了 UI 清单的条目包上导航包装：清单 `scopes` 只含 `global` 的平台组件（如 `qian.tenancy`）进平台控制台目录，其余进租户的外壳目录与菜单。

全部条目 Ready、Registry 启动监听之后 `/readyz` 才返回 `ready`。停机先停 HTTP，再逆序卸载插件树。

### 扩展登记

`PlatformHost.RunAsync(args, extend, configure)` 的 `extend` 拿到 `PlatformHostComposition`，可以：

| 方法 | 用途 |
|---|---|
| `AddComponent` | 登记目录项（module 应用或扩展组件）：组件 ID、清单 `runtime.entry`、实例工厂与宿主 DI 登记（数据上下文用 `AddAppData` / `AddData`）。 |
| `AddComponentDirectory` / `AddPatch` | 额外的组件根目录与扩展 overlay（例如把 module 应用的条目加进全局 scope）。 |
| `AddEvents` / `AddMediator` | 并进同一次 `AddKunEventBus` / `AddKunMediator`（Kun 的事件类型表只认第一次、中介者只认最后一次）。module 应用的能力处理器经 `PlatformRuntime.Tenant(id)` 取租户视角的上下文。 |
| `AddLifecycleHooks` | Lifecycle 的 module 钩子。 |
| `AddOperations` | 运营接口：`map` 映射到运营端点组，组里的端点只收受众 `qian-operations`；`prefix` 声明这组端点所在的路径范围（与网关路由表里指向运营簇的前缀对应，不以斜杠结尾）。S1-DEP-011 用它挂开通申请的运营接口。 |
| `MapEndpoints` | 其它端点（走平台受众），不能放在运营前缀下。 |

运营端点组与声明的运营前缀在启动时核对：组里的端点必须在某个运营前缀下，运营前缀下的端点必须都在组里（与路由一样不区分大小写），不一致即拒绝启动。

集成测试 `PlatformHostTests` 就是这样把参考应用作为 module 应用并进插件树的。

### 同进程身份分支（S1-DEP-011）

`Program.cs` 把 [`PlatformIdentity`](PlatformIdentity.cs) 的 `Extend` 与 `Configure` 交给 `RunAsync`。两者只在 `Identity:InProcess=true` 时生效（R150 ①）：

- `Extend`：经 `AddComponent` 登记 `qian.identity.server` 与 `qian.tenancy.signup` 的目录项与数据上下文；内置扩展 overlay [`identity.overlay.yml`](identity.overlay.yml) 把 `identity` 条目由 `qian.identity.client` 换成 `qian.identity.server`、启用它，并加 `tenancy.signup` 条目，issuer 由环境 overlay 给；身份事件并进同一次 `AddEvents` / `AddMediator`；开通申请的运营接口经 `AddOperations` 挂在 `/api/signup/applications`、`/api/signup/meta` 两个运营前缀下（受众 `qian-operations`）。
- `Configure`：身份分支以 `IStartupFilter` 排在平台管线之前，按路径接手 `/api/identity/**`、`/connect/**`、`/.well-known/**`、认证页族 `/login`、申请人接口（`/api/signup/verifications`、`/session`、`/code-availability`、`/me/**`），给出公开页构建产物时还有 `/signup/**`。这些请求不经平台管线（网关 S2S、租户 scope、授权 PEP），各自用身份服务与开通申请组件的认证。目录接口 `/api/directory/**` 不在分支里，由网关以 S2S 转来、走平台管线。

同进程后，经本宿主做的授权变更由 Authz 经进程内的身份账号端口递增该成员在租户内的身份修订，并抬高 PEP 的修订下限（S1-DEC-027 / S1-AZ-009）：旧令牌经网关调平台表面一律 401，网关换令牌后恢复。本宿主不登记身份侧的授权事件订阅，避免双递增。

身份分支要求 HTTPS issuer 与 RSA 签名、加密证书（含私钥）。`Identity:AllowLoopbackHttp` 只在 Development 且 issuer 是回环地址时生效，只给集成测试用。TLS 在反向代理上终止时（staging 是边缘 hub → WireGuard → Traefik），配置 `Identity:TrustedProxyNetworks` 与 `Identity:ForwardLimit`，分支从可信代理的 `X-Forwarded-Proto` 恢复 HTTPS、从 `X-Forwarded-For` 恢复客户端地址；不配置时不采信任何转发头。转发头只在身份分支里处理，平台管线只认网关 S2S，不需要它们。

## 认证

只装网关 S2S 的两个 JWT 方案，共用网关的签发方与 JWKS（`S2S:JwksUri`，网关的 `/.well-known/jwks.json`，未知 kid 时刷新）：

- 受众按路由实际匹配到的端点选，不按路径写法（R144）：运营端点组（租户运营接口，以及经 `AddOperations` 挂上的端点）只收受众 `qian-operations`，其余端点只收 `qian-platform`。端点路由不区分大小写、忽略结尾斜杠，所以 `/api/Tenancy/tenants/` 与 `/api/tenancy/tenants` 是同一个端点、受众相同；认证排在路由之后。受众错配、多受众、签发方不符、过期或未生效、非 RS256、签名不符都是 401。
- 没有匹配到端点的请求（404，方法不符的 405）不执行任何处理器，按声明的运营前缀（`/api/tenancy` 与经 `AddOperations` 声明的前缀，不区分大小写）选运营受众，其余选平台受众：网关签运营受众转来的这类请求得到 404 / 405，不会被外壳当成会话失效的 401。
- 兜底授权策略要求已认证：没有声明授权要求的扩展端点也拒绝匿名；带组件端点绑定却没有声明权限点的端点由 PEP 按生产模式缺省拒绝（已认证也 403）。
- 不认浏览器 Cookie，也不认身份服务签发的外部访问令牌；载荷即主体快照，接受 user / service / agent 主体。
- 带 `act` 的委托主体由能力目录、执行入口、有效权限、运营接口与命令管道各自拒绝（执行入口与不存在一样 404），与参考宿主相同；S1 不开放委托。
- 平台身份（没有租户）访问租户表面 401；平台控制台目录只给平台身份，带租户的请求 404。

宿主根容器里只有一个身份适配器，平台表面、运营接口与同进程 module 应用的命令管道读同一份已验证快照（S1-YUE-015 裁决 ④）。

## 启动输入

`appsettings.json` 给出不含凭据的配置结构，部署前把示例地址换成当前环境的值。

| 键 | 说明 |
|---|---|
| `Root`（环境变量 `QIAN_Root`，镜像里是 `/app/qian`） | profile 与组件目录的根，必须是绝对路径。 |
| `S2S:Issuer` / `S2S:JwksUri` | 网关的 S2S 签发方与 JWKS 地址。JWKS 必须是 https；集群内走网关的 HTTP 端口时显式设 `S2S:AllowHttpJwks=true`。`S2S:ClockSkewSeconds` 0–60，缺省 0。 |
| `ConnectionFiles:App` | 应用登录（受限角色，按租户 RLS）的连接串 Secret 文件；Tenancy、Authz、Audit、Registry、Capabilities、Lifecycle 与 module 应用都用它。 |
| `ConnectionFiles:PlatformAuthz` / `ConnectionFiles:PlatformAudit` | authz 条目配置了 `platformRole`、audit 条目配置了 `chain.platformRole` 时，两个平台登录的连接串文件。 |
| `ConnectionFiles:Migrations` | 迁移属主的连接串文件，只在 `--migrate-only` 时读。 |
| `ConnectionFiles:Redis` / `ConnectionFiles:EventsBroker` | events 条目用 Redis 领取 Outbox、配置了 broker 时才读。platform profile 缺省两者都要：base 组合包给 events 配了 `outbox.lock: redis` 与 `broker`，环境 overlay 改成 `skip-locked`、去掉 broker 时才不需要。 |
| `Secrets:MountRoot`、`Trust:PlatformKeys` | 平台信任根公钥的 Secret 挂载点与引用（Lifecycle 核验系统制品签名时解析，每次解析写平台审计链）。宿主固定把 `secretRef://platform/trust/platform` 绑到 `<MountRoot>/qian/trust-platform/public-key`，配置写 `Trust:PlatformKeys:0:PublicKey: secretRef://platform/trust/platform`。 |
| `Profile:Patches`、`Profile:ComponentDirectories`、`Profile:HostOverlay` | 环境 overlay、额外组件目录、宿主 overlay 路径（缺省为程序目录下的 `platform.overlay.yml`）。 |
| `ConnectionFiles:MigrationAdmin`、`Migrations:AppRole`、`Migrations:AuditWriterRole`、`Migrations:GatewayAudit:{TenantRole,PlatformRole,FunctionOwner}` | 只在 `--migrate-only` 时读：数据库管理员连接（离线 DDL 与授权，见下文「Job」）、应用登录的角色名、审计同事务函数属主（独立 NOLOGIN 角色）、网关原生审计的三个角色。不给 `MigrationAdmin` 时只执行包内迁移。 |
| `Identity:InProcess` | 为 `true` 时并入身份分支（S1-DEP-011）。 |
| `Identity:PagesRoot`、`Identity:SignupPagesRoot` | 认证页族与开通申请公开页的构建产物目录（必须带 `index.html`；镜像里在 `/app/web` 下）。认证页族必填，公开页可省。 |
| `Identity:SigningCertificate` / `Identity:SigningKey`、`Identity:EncryptionCertificate` / `Identity:EncryptionKey` | 身份服务签名与加密证书、私钥的 PEM Secret 文件（RSA，绝对路径）。 |
| `Identity:Clients:N:{ClientId,SecretFile,RedirectUri,Resource,DisplayName,Scopes}` | 官方客户端（网关 BFF）：机密客户端，授权码 + PKCE 与 refresh，密钥只从 Secret 文件读，回调与 resource 精确登记。 |
| `Identity:DataProtectionKeysDirectory` | 账号 Cookie 与防伪令牌的数据保护密钥目录。只读根文件系统下必须给出可写目录；多副本时须共享。 |
| `Identity:TrustedProxyNetworks`、`Identity:ForwardLimit` | 身份分支信任的反向代理网段（CIDR 数组，前缀长度不能为 0）与从右往左回溯的跳数上限（1–5，缺省 1；每回溯一跳，上一跳都必须在这些网段里）。恢复出的 scheme 与客户端地址供 OIDC 的 HTTPS 要求、Secure Cookie、登录审计、会话与申请人限流使用。staging 的取值与理由见[运维手册](../../docs/ops/staging-first-deploy.md)。 |
| `Identity:Overlay` | 替换内置扩展 overlay 的路径（缺省为程序目录下的 `identity.overlay.yml`）。 |
| `Bootstrap:Username`、`Bootstrap:DisplayName`、`Bootstrap:PasswordFile` | 只在 `--bootstrap-operator` 时读：首个平台运营账号的用户名、显示名与初始密码 Secret 文件。 |
| `SystemRevisions:SignaturesFile` | 只在 `--install-system-revisions` 时读：系统组件修订的签名清单（ConfigMap 挂载）。 |

连接串只从 Secret 文件读，每次建上下文时现读，便于轮换。角色名（authz 的 `platformRole`、audit 的 `chain.platformRole`）与 events 的 broker、Outbox 领取方式写在环境 overlay，例如：

```yaml
- id: authz
  config: { advancedMode: false, platformRole: qian_platform_authz }
- id: audit
  config: { chain: { store: pg, platformRole: qian_platform_audit } }
```

```bash
dotnet run --project hosts/Qian.Platform.Host -c Release -- --urls http://0.0.0.0:8080 --patch /etc/qian/platform/environment.yml
```

### Job

三个 Job 与在线副本用同一镜像、同一份配置与 Secret，只多一个开关；跑完退出（成功 0，失败 1），不监听 HTTP。部署顺序是迁移 → 系统组件修订 → （首次）引导首个平台运营账号 → 在线副本。

```bash
dotnet run --project hosts/Qian.Platform.Host -c Release -- --migrate-only --patch /etc/qian/platform/environment.yml
dotnet run --project hosts/Qian.Platform.Host -c Release -- --install-system-revisions --patch /etc/qian/platform/environment.yml
dotnet run --project hosts/Qian.Platform.Host -c Release -- --bootstrap-operator --patch /etc/qian/platform/environment.yml
```

- `--migrate-only`（[`PlatformMigrations`](PlatformMigrations.cs)）：三步，都可重复执行。① 配置了 `ConnectionFiles:MigrationAdmin` 时用管理员连接做离线 DDL：没有包内迁移的上下文（Tenancy、Authz、Audit、Registry）按 EF 模型建 schema 与表（含 RLS），schema 已在时只核对表都在；再执行两个平台登录与审计同事务函数的授权脚本，配置了网关审计角色时执行网关原生审计的离线迁移（`NativeAuditStorage`）。审计函数的属主必须是没有任何成员关系的 NOLOGIN 角色，改函数属主只能由管理员做，所以这一步不交给迁移属主。审计租户守卫带原生审计标记后不再重放 Audit 的平台访问脚本（否则原生迁移判为漂移）。② 迁移属主执行包内迁移（Capabilities、Lifecycle、身份、开通申请等）。③ 管理员把全部业务 schema 的表与序列授给应用登录，迁移历史表只读。离线 DDL 不做跨版本的表结构升级：schema 已在而缺表时拒绝执行，正式迁移链归 S1-DEP-005。
- `--install-system-revisions`（[`PlatformSystemRevisions`](PlatformSystemRevisions.cs)，R151 ①）：开户编排第 03 步把宿主预装的 system 级组件逐个开通进租户，Lifecycle 要求每个组件有已激活、经信任根验签、摘要等于预装组件文件夹的修订。这个 Job 按签名清单（[`scripts/sign-system-revisions.py`](../../scripts/sign-system-revisions.py) 在构建机上对同一镜像生成，私钥不进集群）幂等地安装并激活这些修订：同摘要已激活则不动，新摘要安装后激活；清单缺任何一项即失败并列出缺项。安装与激活写平台审计链。
- `--bootstrap-operator`（[`PlatformOperatorBootstrap`](PlatformOperatorBootstrap.cs)，R150 ③）：要求身份分支在进程内。库里已有平台超管时拒绝执行（退出码 1），不能拿它二次提权；否则经身份账号端口建平台身份（要求首登改密），在平台角色锁里授平台超管，写一条平台审计链记录。同名平台身份已存在（上次在授权之前失败）时沿用、不重置密码。跑完删除初始密码的 Secret。

三个开关都不带值，宿主把它们从命令行配置里去掉再交给配置源，紧跟其后的参数（如 `--environment=…`）不会被吞掉。

装配、迁移或启动失败时，异常写到标准错误，进程以退出码 1 结束。入口不让异常逃出 Main：容器里宿主是 PID 1，未处理异常走 abort 时内核忽略这个信号，进程不退出、占满一个 CPU。

在线副本启动时只校验（受限角色、模型、没有待执行的迁移），不建表、不迁移。

## 端点

| 路径 | 受众 | 说明 |
|---|---|---|
| `GET /healthz`、`GET /readyz` | 匿名 | 存活与就绪探针，不回显组件细节。 |
| `/api/capabilities/**` | `qian-platform` | 能力目录、通用执行入口、导航、外壳目录（`MapQianCapabilities`）。 |
| `GET /api/capabilities/console` | `qian-platform` | 平台控制台目录（`MapQianPlatformConsole`），只给平台身份。 |
| `/api/authz/**` | `qian-platform` | 有效权限（`MapQianAuthz`）。 |
| `/api/tenancy/**` | `qian-operations` | 租户运营接口（`MapQianTenancy`，在运营端点组里），平台超管显式旁路，放行写平台审计链。 |
| `/api/signup/applications/**`、`/api/signup/meta/**` | `qian-operations` | 开通申请的运营接口（身份分支并入时，`MapQianTenancySignupOperations`），同样经超管旁路、写平台审计链。 |
| `/api/identity/**`、`/connect/**`、`/.well-known/**`、`/login`、申请人接口、`/signup/**` | 身份分支自己的认证 | 身份分支并入时：OIDC 协议面、账号接口、认证页族、申请人接口与公开页（见上文）。 |

表里的受众是对端点说的：路径的大小写与结尾斜杠变体路由到同一个端点，受众也相同。

网关宿主的路由表（[`hosts/Qian.Gateway.Host/appsettings.json`](../Qian.Gateway.Host/appsettings.json)）按簇转发：能力与有效权限进 `qian-platform`，租户运营进 `qian-operations`，两个簇指向本宿主的同一个服务地址；以后要拆只改簇地址。

## 与参考宿主的差异

联调环境（[tools/Qian.Dev.Shell](../../tools/Qian.Dev.Shell/README.md)）与大部分集成测试仍用参考宿主：平台表面参考宿主 `PlatformSurfaceHost` 与运营参考宿主 `TenantOperationsHost`（[samples/Qian.Sample.Crm](../../samples/Qian.Sample.Crm/README.md)）。本宿主与它们的差异：

| 方面 | 参考宿主 | 本宿主 |
|---|---|---|
| 插件树 | 借用 CRM 参考宿主已装配的运行时（测试里手工挂载的 Provider 与临时组件文件夹） | 按仓库交付的 platform profile 与组件文件夹自行装配，条目缺 Provider 即拒绝启动 |
| 进程与受众 | 平台表面与运营各成一个宿主，各认一个受众 | 同一进程、同一插件树、同一端口，按匹配到的端点分两个受众（R144） |
| 验签 | 测试直接给签名公钥 | 取网关公开的 JWKS，钥轮换时按 kid 刷新 |
| 身份适配器 | 平台表面验签后把快照交给参考应用的适配器（依赖 HttpContextAccessor 的静态 AsyncLocal） | 根容器里只有一个适配器，平台表面、运营接口与 module 应用共用 |
| 数据库登录 | 测试里的应用角色 | 应用登录与两个平台登录分别绑定，连接串只从 Secret 文件读，迁移属主只给迁移 Job |
| 身份服务 | 同进程（内置账号模型） | `Identity:InProcess=true` 时同进程（S1-DEP-011），否则不在进程内 |
| 开发期内容 | 开发路由、种子与开发期发件箱 | 没有 |

## 已知限制与接续

- 不并入身份分支时，经本宿主自己的接口做的授权变更不会递增身份修订（身份端口只有同进程实现），旧令牌在会话刷新前仍有效，开通申请的运营接口也不挂。同进程形状（S1-DEP-011）的集成测试 `PlatformIdentityHostTests` 覆盖递增与 401。
- Tenancy、Authz、Audit、Registry 没有随包迁移，由迁移 Job 的离线 DDL 首装；跨版本的表结构升级归 S1-DEP-005 的正式迁移链。
- 系统组件修订的签名用 staging 专用钥（R151 ①），正式的发布链签名与信任根轮换归 S1-DEP-005。
- 数据保护密钥在 staging 放在 Pod 的临时卷：重启即需重新登录；多副本前须改为共享目录。
- 镜像上的实测与供应链检查归 S1-DEP-008。
- 没有装配 OpenAPI 与 proto 的兼容检查器：组件升级时的契约兼容判定会拒绝执行（fail-closed），在镜像带上检查器之前不能在本宿主上做跨版本升级。
- 平台组件以平台镜像预装为安装路径（R144 ③）；`qian add` 独立安装 tenancy / authz / audit 另见 S1-RT-040。
