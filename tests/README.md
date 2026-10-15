# 测试与门禁

> 对应任务 S1-QA-001。依据：[PRD §14 质量与评估](../docs/prd/14-quality.md)（测试金字塔、C1 PublicAPI、C10 测试门禁、C19 依赖图断言）、[ADR-019](../docs/adr/ADR-019-library-first-packages.md) D19.7 / D19.8、[组件模型规格](../docs/prd/specs/component-model.md) §6.1 与 §10.3。

## 1 目录

```text
tests/
  Directory.Build.props                       测试项目公共设置：不打包、不要求 XML 注释
  Qian.Testing.Containers/                    集成测试共用的 Testcontainers fixture（不打包）
    ApolloMock/apollo_mock.py                 Apollo 协议替身，在容器里运行
  Qian.Testing.Containers.IntegrationTests/   fixture 自身的集成测试与单元测试
  Qian.Authz.Tests/                           授权：权限目录与默认角色、RBAC 判定与快照缓存（真实 PostgreSQL + Redis，两宿主授予 / 撤销传播）、平台超管旁路（专用平台登录 + 真实 Audit 插件）
  Qian.Data.AspNetCore.Tests/                 数据端点：基准类型目录（无数据库）与数据连接档案（真实 PostgreSQL、挂载式密钥、连通测试）
  Qian.Lifecycle.Tests/                       生命周期：验签与信任根轮换（真实 PostgreSQL）、GitOps 执行器（本机裸仓库 + K3s 单节点集群）、Local 执行器
  Qian.Runtime.Errors.Tests/                  contracts/v0 契约测试（Schema、fixture、语义规则、内置错误码目录）与 ErrorDetail 内存宿主测试
  Qian.Runtime.Tests/                         最小运行时单元测试与 tck/runtime/*.scenario.yml 的 .NET harness
  Qian.Common.Tests/                          packages/Common 共享内部源文件的测试，直接编译源文件；并生成 CLI 移植比对用的 tck/profiles/key-vectors.json 与等价类表
  gates/                                      门禁自测：样例项目 + Python 测试，不在 Qian.slnx 里
    depgraph/                                 依赖图门禁样例（good.slnx 合规、bad.slnx 违规）
    publicapi/                                PublicAPI 门禁样例
    yue/                                      yue 前端门禁自测（分层边界、循环依赖样例、包形状），由 pnpm yue:test 跑
```

## 2 本地运行

```bash
dotnet build Qian.slnx -c Release
python3 scripts/check_deps.py --no-restore
python3 -m unittest discover -s tests/gates -v
dotnet test --solution Qian.slnx -c Release --no-build
```

- 本机跑全量 `dotnet test` 前先补齐 CI 的两个前置，否则 `Qian.Capabilities.Tests` 与 `Qian.Sample.Crm.Tests` 会出现 19 个环境性失败（S1-DEC-019）：
  - `pnpm install --frozen-lockfile && pnpm --filter "qian..." build`：doctor 用例要调用 `packages/qian-cli/dist/main.js`；
  - `python3 scripts/prepare_contract_tools.py --destination <目录>`，再导出 `QIAN_BUF_PATH=<目录>/buf` 与 `QIAN_OASDIFF_PATH=<目录>/oasdiff`：能力契约的真实兼容检查器，缺失时相关用例按设计拒绝跳过。
- `dotnet test` 走 Microsoft Testing Platform（[global.json](../global.json) 的 `test.runner`）；xUnit v3 测试项目是可执行程序，csproj 里写 `<OutputType>Exe</OutputType>`。
- 单跑一个测试项目用 `dotnet run --project tests/<X>.Tests -c Release`；`dotnet test <项目路径>` 或 `dotnet test --project …` 在部分机器上会报「运行了零个测试、退出码 5」，以 `--solution` 或 `dotnet run` 的结果为准。
- CLI 的私有源 E2E（`packages/qian-cli/test/private-feed.e2e.mjs`）不随 `pnpm --filter qian test` 运行，单独用 `QIAN_FEED_TOKEN=<读包令牌> pnpm --filter qian test:feed`；本机可取 `gh auth token`（需 `read:packages`）。令牌只放环境变量，缺失即失败、不跳过。网络很慢时可另设 `QIAN_PACKAGE_TIMEOUT`（整数秒，缺省 600）放宽 CLI 包管理器操作的超时。
- 集成测试需要 Docker。本机没有 Docker 时集成测试**跳过**；在 CI（`CI=true`）或设置了 `QIAN_REQUIRE_DOCKER=1` 时**直接失败**，不会悄悄跳过（C10：集成测试必须跑）。
- 首次运行要拉镜像，之后整套测试十秒左右。

## 3 写一个集成测试

整组测试共享一套容器：在测试项目里声明一次 collection，测试类挂上它。

```csharp
[CollectionDefinition(Name)]
public sealed class SharedInfrastructure : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "shared-infrastructure";
}

[Collection(SharedInfrastructure.Name)]
public sealed class OrderVisibilityTests(InfrastructureFixture infra)
{
    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b()
    {
        // 建表、迁移、造数用超级用户；被测代码一律用受 RLS 约束的应用角色。
        var admin = infra.Postgres.ConnectionString;
        var app = infra.Postgres.AppConnectionString;
        // ...
    }
}
```

只用一个依赖时，也可以把单个 fixture 直接当 class fixture 用。

| fixture | 默认镜像（覆盖用的环境变量） | 提供什么 |
|---|---|---|
| `PostgresFixture` | `postgres:16-alpine`（`QIAN_TEST_IMAGE_POSTGRES`） | `ConnectionString`：超级用户，做 DDL 与造数；`AppConnectionString`：应用角色 `qian_app`（NOSUPERUSER NOBYPASSRLS，只有 DML 权限）。验证 RLS 必须用应用角色——超级用户会绕过 `FORCE ROW LEVEL SECURITY` |
| `RedisFixture` | `redis:7-alpine`（`QIAN_TEST_IMAGE_REDIS`） | `ConnectionString` |
| `RabbitMqFixture` | `rabbitmq:4.3-alpine`（`QIAN_TEST_IMAGE_RABBITMQ`） | `ConnectionString`（AMQP URI）；`Container` 可停启 broker，做「重启后消息不丢」这类韧性测试 |
| `ApolloFixture` | `python:3.13-alpine` 运行 `apollo_mock.py`（`QIAN_TEST_IMAGE_APOLLO_MOCK`） | `ConfigServiceUri`（即客户端的 `Apollo__ConfigServer__0`）；`PublishAsync`、`DeleteAsync`、`SetAccessKeyAsync`、`SetHoldAsync`、`ResetAsync` |
| `InfrastructureFixture` | 以上四个并行启动 | `Postgres`、`Redis`、`RabbitMq`、`Apollo` |
| `K3sFixture` | `rancher/k3s:v1.31.5-k3s1`（`QIAN_TEST_IMAGE_K3S`），特权容器 | `Kubeconfig`（API Server 已映射到宿主端口）；`KubectlAsync` 在容器内执行 kubectl（含 kustomize），`CopyAsync` 写入待 apply 的清单。K3s 启动时会短暂占满 CPU，容器在就绪前自行退出时重建，最多三次（只重建容器，不重试断言）；用例里的工作负载镜像由集群自行拉取，需要出网 |

- 版本：PostgreSQL 16、Redis 7 与部署基线一致（ADR-017 D17.2）；RabbitMQ 取当前 4.x 稳定线。环境变量用来与 staging 对齐或换内网镜像源。
- 容器口令每次运行随机生成，仓库里没有写死的凭据。
- PRD §14.2.1 的约定照旧：多副本用同一套容器起 2–3 个宿主实例，不用「单实例 + 顺序调用」冒充；集成测试默认在两个租户下运行（租户夹具随租户组件补上）；安全类用例不允许重试通过。

### Apollo 协议替身

PRD §14.2 规定集成测试层的 Apollo 用 mock server。[apollo_mock.py](Qian.Testing.Containers/ApolloMock/apollo_mock.py) 只依赖 Python 标准库，实现 Apollo「其它语言客户端接入」HTTP 协议里客户端会用到的部分：

| 接口 | 行为 |
|---|---|
| `GET /configs/{appId}/{cluster}/{namespace}` | 返回 `configurations` 与 `releaseKey`；带相同 `releaseKey` 返回 304；未发布或已撤下返回 404 |
| `GET /configfiles/json/{appId}/{cluster}/{namespace}` | 配置的 JSON 视图 |
| `GET /notifications/v2` | 长轮询：客户端落后时立即返回新通知号；否则挂起（默认 60 秒，与 Apollo 服务端相同）直到有发布，超时返回 304 |
| `GET /services/config` | Meta Server，返回本服务地址 |
| 访问密钥 | `SetAccessKeyAsync` 之后，请求必须带 `Timestamp` 与 `Authorization: Apollo {appId}:{签名}` 头；签名 = Base64(HMAC-SHA1(密钥, 时间戳 + 换行 + 路径与查询串))，时间差超过 60 秒或签名不符返回 401 |

不覆盖：灰度发布与 label 规则、Portal / Admin Service 接口、多实例 Meta Server、增量同步。自研 Apollo provider（S1-CFG-001）若要对真实服务端做契约测试，需另起 Apollo；官方 quick-start 镜像默认要配一个 MySQL 容器（只切换到 H2 配置组仍会去连 MySQL，已实测）。

## 4 门禁

### 4.1 PublicAPI（C1 / G4）

- **Qian 包** = 项目里写了 `<IsPackable>true</IsPackable>` 且包名以 `Qian.` 开头。根目录 [Directory.Build.props](../Directory.Build.props) 默认不打包，宿主、样例、测试都不算 Qian 包。
- Qian 包自动接入 `Microsoft.CodeAnalysis.PublicApiAnalyzers`（[Directory.Build.targets](../Directory.Build.targets)）。项目目录必须有 `PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt`，首行写 `#nullable enable`，缺了报 QIAN0001。
- 新增公共 API 没登记报 RS0016，登记了但代码里已删除报 RS0017，都是编译错误。新 API 先登记进 Unshipped，发布时并入 Shipped；破坏性变更要引用 ADR（C1）。IDE 对 RS0016 的代码修复「添加到公共 API」会生成登记行。

一个 Qian 包目录至少有：

```text
<包目录>/
  Qian.Foo.Abstractions.csproj    含 <IsPackable>true</IsPackable>
  PublicAPI.Shipped.txt           #nullable enable
  PublicAPI.Unshipped.txt         #nullable enable，之后逐行登记新 API
```

### 4.2 依赖图（C19 / G3）

[scripts/check_deps.py](../scripts/check_deps.py) 检查解决方案里的每个 Qian 包：

- 允许的 Qian 依赖只有 `Qian.Runtime`（及 `Qian.Runtime.*`）、`Qian.Plugin.Sdk`（及 `Qian.Plugin.Sdk.*`）和任何 `*.Abstractions`。平台插件包 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities` 与所有 Provider 包，出现在传递依赖里即违规。Definition 的 Kun 直接依赖另受下述规则约束；第三方包与 Kun 传递依赖不在该附加规则范围内。
- 组合包 `Qian.Bundle.*` 不检查：它的职责就是把一组 Provider 一起装（组件模型规格 §7.3）。
- 数据来自各项目的 `obj/project.assets.json`。`dotnet list package --include-transitive` 只列 NuGet 包，看不到 ProjectReference 带进来的项目，而仓库内组件之间多是项目引用，所以直接读 NuGet 还原产物，包与项目两类依赖一并检查。
- 失败时打印从被检查方到违规依赖的完整路径，例如 `Qian.GateSample.Reports → Qian.GateSample.Files.Abstractions → Qian.GateSample.Authz`；加 `--json` 输出机器可读结果。修法：改为依赖对方的 `.Abstractions` 包。

### Definition 的 Kun 直接依赖（S1-KUN-010）

Definition 只允许直接引用 `Kun.*.Abstractions`，`PackageReference` 与 `ProjectReference` 均检查。Provider 使用 Kun 实现包不受此附加规则影响。现有公开契约的以下例外按完整「消费包 → 依赖包」登记在 `DEFINITION_KUN_EXCEPTIONS`，每条有理由，不能扩大为包名前缀豁免：

| 消费包 | 允许的 Kun 直接依赖 | 保留理由 |
|---|---|---|
| `Qian.Tenancy.Abstractions` | `Kun.Core` | 复用零外部依赖的租户契约 |
| `Qian.Authz.Abstractions` | `Kun.Authorization` | 复用框架权限、数据与字段判定端口，避免复制接口 |
| `Qian.Data.Abstractions` | `Kun.EntityFrameworkCore` | 已发布的 QianDbContext 基类与同上下文工作单元契约 |
| `Qian.Data.Abstractions` | `Kun.MultiTenancy.EntityFrameworkCore` | 启动期校验 RLS 双拦截器与模型缓存键，维持隔离安全 |

Data 的两项依赖保留现有实现与公开 API；本任务不通过删除启动校验或复制基类来消除依赖。后续若移动数据基类，应先提供兼容迁移再删除对应白名单。身份事件入口已改为 `Kun.EventBus.Abstractions`，不在例外内。

### 4.3 门禁自测

`tests/gates` 下的样例项目专门证明门禁拦得住违规，CI 每次都跑（`python3 -m unittest discover -s tests/gates -v`）：

| 样例 | 期望 |
|---|---|
| `depgraph/kun.slnx` | 真实 Kun 包：Storage 抽象可引用，EventBus 实现被 Definition 规则拒绝 |
| `depgraph/good.slnx` | 通过；其中组合包与不打包的宿主引用 Provider 不算违规 |
| `depgraph/bad.slnx` | 4 处违规：Definition 依赖 Provider、Provider 依赖平台插件、Provider 依赖另一个 Provider、经 Definition 传递依赖 Provider |
| `publicapi/Qian.GateSample.Declared` | 构建通过 |
| `publicapi/Qian.GateSample.Undeclared` | RS0016，构建失败 |
| `publicapi/Qian.GateSample.Stale` | RS0017，构建失败 |
| `publicapi/Qian.GateSample.NoApiFiles` | QIAN0001，构建失败 |
| `publicapi/GateSample.NotPackable` | 构建通过：不打包的项目不受约束 |
| `test_check_deps.py` 的 `SdkBoundaryTests` / `RealSdkClosureTests` | SDK 经 Definition、第三方包或另一个 SDK 包间接带入 Provider 与 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities` 都报出完整路径；仓库里真实的 `Qian.Plugin.Sdk`、`.AspNetCore`、`.Generators` 在 `Qian.slnx` 的门禁范围内，其完整传递依赖只有 Runtime / SDK / Definition 与第三方包（S1-RT-014） |
| `test_dotnet_release.py`（合成 `.nupkg`，不需要 dotnet 与网络） | 包族锁步发布的制品核对：缺包、多包、版本不一致、包族内依赖下限未钉同一版本或指向包族外、源生成器包带 `lib/` 或不是开发依赖、许可证与仓库地址缺失都失败；版本号须为不带前缀与构建元数据的 SemVer 2.0；推送后的包源核对缺包、仓库关联不符、可见性与仓库不一致或缺版本都失败（S1-RT-014） |
| `test_check_docs.py`（不用样例项目，临时目录里搭最小仓库） | 文档门禁对文档站内容生效：站内路由、`/design/` 镜像路由与生成页路由的解析，生成目录跳过，禁用词在 `.mdx` 与站点源码里同样拦截（S1-DOCS-001）；Kun 小节的路由在生成前交给构建时的断链检查，显式给出的生成目录照查，生成目录里的文件进 Git 即失败，禁用词整词匹配（S1-DOCS-005） |

## 5 CI

[.github/workflows/dotnet.yml](../.github/workflows/dotnet.yml)：还原 → 构建（PublicAPI 门禁）→ 依赖图门禁 → 门禁自测 → 单元与集成测试，测试结果（TRX）作为构件上传，整体限时 20 分钟（PRD §14.2.1）。仓库暂无自托管 runner，先用 GitHub 托管的 ubuntu-latest（自带 Docker）；自托管 runner 就位后只需改 `runs-on`。

包族发布不在 PR 门禁里：[.github/workflows/dotnet-release.yml](../.github/workflows/dotnet-release.yml) 只从 main 手动触发，把全部 Qian 包以同一版本发到 GitHub Packages，核对包源里的版本、仓库关联与可见性，并在干净消费者项目里从该源真实还原、构建、运行（S1-RT-014，ADR-019 D19.7）；做法与失败处理见 [SDK 文档「包族发布」](../packages/Qian.Plugin.Sdk/README.md#包族发布)。


## 旧 Kun API 门禁（S1-KUN-006、S1-KUN-004）

根目录 `BannedSymbols.txt` 与 `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 对实际 API 使用产生编译错误 RS0030，应用于包、宿主和测试。`tests/gates/bannedapi` 的负例引用已发布 Kun 2026.10.9 的旧仓储／存储接口，正例引用新存储包，避免凭虚构类型验证门禁。Kun 2026.10.9 起旧接口自带 `KUNLEGACY001` Obsolete，负例会同时报 KUNLEGACY001 与 RS0030 / QIAN0002，门禁只断言后两者，叠加后判定不变（S1-KUN-009）。

命名空间以源码为准：旧存储 `Kun.Storage` 包的接口是 `Kun.Storage.Abstractions.IFileStorage`；新 `Kun.Storage.Abstractions` 包的接口是 `Kun.Storage.IFileStorage`。标准分析器覆盖符号使用（例如 typeof、调用、构造）；`tools/Qian.Build.Analyzers` 使用同一清单补齐未使用的参数、字段、返回类型等声明，并发出 QIAN0002。负例包含纯参数声明，不依赖上游新包的 Obsolete 标记。

执行 `python3 -m unittest discover -s tests/gates`，同时验证依赖图、PublicAPI 与旧 API 的正反例。后续发布 Kun 新包时仍只通过 `Directory.Packages.props` 的 KunVersion 升级；此任务不伪装成已经消费上游新实现。

S1-KUN-004 补充旧 Identity / Security S2S 签发器、旧注册方法及旧校验/JWKS 子模块的禁用清单。新增正反例使用当前 KunVersion 锁定的真实发布包：两套旧签发器和旧工厂方法产生 RS0030，网关 S2sTokenIssuer 正常编译。负例主动关闭 TreatWarningsAsErrors 仍无法绕过；上游后续版本附带的 KUNS2S001 也被单列为错误。这里验证的是编译边界，不代表已发布或消费尚未发布的上游改动。

## 数据安全编译门禁（S1-DATA-001）

`DataBoundaryAnalyzer` 对非测试项目中直接或间接继承 EF `DbContext`、却不继承 `QianDbContext` 的上下文报 QIAN0003。测试项目可定义验证用的上下文；编译样例显式设置 `IsTestProject=false` 模拟在线代码，不因位于 tests 目录而豁免。

全仓库对 EF `IgnoreQueryFilters` 的调用和方法委托引用报 QIAN0004，按语义符号识别，其他类的同名方法不误报。`test_data_boundary.py` 验证五组真实 EF 正反例，负例即使关闭 TreatWarningsAsErrors 也必须失败。真库隔离测试使用受限应用角色的原生 SQL，不调用过滤器旁路。

### 受保护命令注册门禁（S1-AZ-001）

QIAN0005 在全部代码树检查真正的 Kun `AddRequestHandler`：受保护请求或未确定请求类型的泛型封装必须改用 `Qian.Authz.Abstractions.AddAuthorizedHandler`，同一入口将处理器与权限行为登记到同一个 builder。两个重载、方法组引用、生成代码均受检查；关闭警告即错误不会降低规则。

`python3 -m unittest discover -s tests/gates -p test_authorized_handler.py` 编译八组真实样例：直接注册、能力注册、委托引用、泛型包装、应用伪造桥接类必须失败；安全入口、普通请求及无关同名方法必须通过。运行时拒绝/放行顺序由 Qian.Authz.Abstractions.Tests 验证。

### 集成事件发布位置门禁（S1-EVT-001）

QIAN0006 落实「集成事件只能在命令 handler 同 scope 发出」：对 Kun `IIntegrationEventBus.Publish` 与 `IOutboxWriter.Enqueue` 的调用和方法组引用，只允许出现在写命令 handler（处理 Kun `ICommand`，或处理 `[Capability]` 且 SideEffect 不为 read 的请求）与领域事件通知 handler 内；直接向 Kun 缓冲器追加消息（`IntegrationEventBuffer.Add`、`IIntegrationEventOutbox.Add`）会绕过 v1 信封，只允许出现在 Outbox 写入端（实现 `IOutboxWriter` 或 `IIntegrationEventBus` 的类型）里。测试项目（引用了 xUnit 的编译）不受检查。运行时由 SDK 命令管道开启的事件来源作用域兜底，作用域外发布被拒绝。

`python3 -m unittest discover -s tests/gates -p test_event_publish_scope.py` 编译 `tests/gates/events` 下八组真实样例：命令 handler、领域事件 handler、写能力和无关同名方法必须通过；查询 handler、只读能力、后台服务和直接写缓冲器必须以 QIAN0006 失败。

### 能力契约的真实兼容检查

运行 `Qian.Capabilities.Tests` 和客户目录契约挂载用例前，先执行 `python3 scripts/prepare_contract_tools.py --destination /tmp/qian-contract-tools`，再设置 `QIAN_OASDIFF_PATH=/tmp/qian-contract-tools/oasdiff`（Windows 使用对应 exe）。下载版本与 SHA-256 固定在 `scripts/contract-tools.lock.json`，不会运行远程安装脚本。全部 .NET 测试也需要此环境变量；工具缺失按失败处理。CI 已包含安装步骤。

proto 与反射用例还需设置 `QIAN_BUF_PATH=/tmp/qian-contract-tools/buf`（Windows 使用 buf.exe）；同一安装脚本会下载并核验该二进制。运行全部 .NET 测试时同时提供 QIAN_OASDIFF_PATH 与 QIAN_BUF_PATH。
