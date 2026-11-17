> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# ADR-023 Qian 并入 Kun 并重新定位 Kun：合并蓝图

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-023-kun-qian-merge-blueprint.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-023-kun-qian-merge-blueprint.md)，请修改源文件，不要改本页。

> 状态：Accepted（2026-11-17）。方向与并入范围由产品负责人裁定（[S1-DEC-036](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-036.md) D1–D14，对应本文 D23.1–D23.5 与 D23.17 至 D23.20），§7 的十条待确认项已逐条确认，其余条目随本文确认。本文按 D23.15 分阶段落地：对应阶段的任务开工之前不动代码与仓库结构，`AGENTS.md` 里的现行规则照旧执行。ADR-001、ADR-021 已标部分取代，ADR-013、ADR-019、ADR-020 已标修订。
>
> 修订（2026-11-17，[S1-DEC-038](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-038.md) D17、D18）：合并后的仓库由 `idadawn/kun` 改为 `CavaiLAB/kun`，改写 D23.1 与 D23.13 里的仓库地址，原 Kun 的历史 PR 号也要改写成带仓库名的形式；迁移现在开始，不再等「初步可用」里程碑收口，改写 D23.20 第 3 点与 D23.15 的时点；前端迁往 `CavaiLAB/qian` 并行进行，不再排在阶段 3 的最后一批，改写 D23.17、D23.18 第 5 点与 D23.15 的排期。包族并成 `Kun.*`、两档分层、归并对照、命名规则、许可、运行期字面不改、租户号用 Guid 都不受影响。正文未改；阶段怎样对应到批次，见 `CavaiLAB/kun` 的 `docs/migration-plan.md`。

## 1 背景与问题（Context）

2026-11-17 产品负责人提问「Kun 和 Qian 是否可以合并为一个框架」。合仓此前被否决过三次：[ADR-001](/design/adr/adr-001-kun-relationship.md) §4、[ADR-021](/design/adr/adr-021-product-family-structure.md) D21.2、[框架细化备忘录](/design/decisions/framework-refinement-2026-10.md) §3.3 (d)。按当日代码重核，当时的理由大多已不成立（对照表见 S1-DEC-036）。产品负责人随后裁定：

1. 「那就 Qian 并入 Kun 这个框架。」
2. 并入范围选「包名也并成 Kun.\*」。
3. 「但是，不是单纯的合并。在合并的过程中，要考虑到一些复用的东西。可以将两个库进行合并，重新定位 Kun 这个框架。」
4. 「租户号统一用 Guid。」

要回答的问题是：合并之后 Kun 是什么；两边重叠的部分怎么归并成一份；包、名字、许可、版本、仓库、门禁各怎么并；分几步做、什么时候做。

### 1.1 两边的现状

| 项       | Kun                                               | Qian                                  |
| ------- | ------------------------------------------------- | ------------------------------------- |
| 仓库      | `idadawn/kun`，公开                                  | `idadawn/qian`，公开                     |
| .NET 规模 | 35 个模块，约 2.4 万行；31 个测试项目                          | 56 个包项目，`packages/` 约 6.4 万行；87 个测试项目 |
| 版本与发布   | 日历版本 `YYYY.M.D`，nuget.org                         | 语义版本锁步，GitHub Packages                |
| 许可      | MIT                                               | Apache-2.0                            |
| 近况      | 2026-10 以来 22 个非合并提交里 18 个带 Qian 任务号；11 月至评估日没有提交 | 近 14 天 168 个提交合入 main                 |
| 其它资产    | 样例、基准、17 份自己的 ADR                                 | 前端 yue、TS 运行时与命令行、文档站、看板、契约、部署清单      |

Qian 的 56 个包项目里 23 个直接引用 Kun 包，全仓库 317 个源文件引用 Kun 命名空间，`QianDbContext` 继承 `KunDbContext`，24 个公共 API 清单文件里有 72 行 Kun 类型。

### 1.2 核实范围

本文的归并对照（§2 D23.9）来自 2026-11-17 对两边源码的分域只读核实：qian `origin/main`（8eeb80fa22）与 kun `origin/main`（abad3cc）。没有跑构建与测试。表里的「关系」一列读了实现；标「推断」的只按项目文件或求值顺序推出，没有实测。下列事项没有核实，归并开工时要补：

- Kun 模块上的 AOT 标记是否生效。标记写在各项目文件里，判断条件却在更早求值的 `Directory.Build.props` 里，很可能不生效（推断；同一问题已由 S1-KUN-015 跟踪）。
- Qian 的 OIDC 连接器与 Kun 的 OIDC 客户端两份校验规则没有逐条比对。
- 身份服务、文件配额、调度工作循环、两边的订阅器只读了结构，没有读完实现。
- Qian 之外的 Kun 使用方对各模块的实际用量。
- 网关上是否存在每请求重复签发（推断，未运行）。

## 2 决策（Decision）

### 已裁（产品负责人，2026-11-17）

- **D23.1 方向**。Qian 并入 Kun。合并后的仓库是 `idadawn/kun`。
- **D23.2 包族**。`Qian.*` 并入 `Kun.*`，一个包族、一条日历版本线。
- **D23.3 合并方式**。不是原样搬运。合并时清点两边重叠与可复用的部分，同一职责合成一份。
- **D23.4 定位**。Kun 重新定位，不再只是 Qian 之下的底座 SDK。
- **D23.5 租户号统一用 Guid**。Kun 现在接受任意 1 到 128 位的标识，并在缺租户时回落到 `default` 租户；合并后只收 Guid，去掉缺省回落，缺租户即拒绝，与 ADR-004 的「租户上下文只来自令牌、缺失即拒绝」一致。租户上下文端口的属性类型由字符串改为 `Guid`（已裁，D23.20），在数据域归并批次里落地。
- **D23.17 前端更名**。前端框架由 yue 更名为 qian，与 `Qian.*` 改名 `Kun.*` 的代码切换放在同一个冻结窗口里做，不单独先做（S1-DEC-036 D6、D7）。本条在初稿之后裁定，编号接在 D23.16 之后，不改动已有编号。它改写了 D23.6 的口径与 D23.10 里的「yue 不动」，排期见 D23.15，带出的 §7 第 2、10 条已裁，见 D23.18。
  涉及的量（qian `origin/main` 9399cf9584，只读盘点）：要改名的路径 517 个，要改内容的文件 517 个、约 4,500 行，不含只出现 `S1-YUE-*` 任务号的 101 个文件。其中 8 个包名 `@idadawn/yue-*` 有 599 处引用，样式变量 `--yue-*` 329 个共 2,289 处，`yue-*` 形式的测试标识等 227 个，根脚本 `yue:*` 16 个，另有 UI 清单的必填键 `engines.yue`。文档里有 285 行同时出现 yue 与 Qian，只能人工改写。
- **D23.18 「Qian」的用法与前端更名的小项**。产品负责人对 §7 第 2、10 条答「都按推荐」（S1-DEC-036 D8、D9）。
  1. 「Qian」只作前端框架的名字。D23.10 表里的第二类标识当作历史字面保留，不再解释为产品名；平台发行版不单独起名。`qian` 命令行与 `@qian/*` 导入名本次不改。总名 Yi 之下怎么写留给 S1-DEC-034。
  2. 8 个前端包直接叫 `@idadawn/qian-<原名>`，不换词：biz、contracts、core、data、form、sdk、shell、tokens。
  3. 清单 `engines` 键：前端用 `qian`，平台运行时版本并入 `kun`。
  4. `S1-YUE-*` 任务号与历史日志不改；本地存储键换名不做迁移。
  5. 排期：阶段 3 的最后一批（D23.15）。
- **D23.19 §7 第 1、3、4、5、6、9 条**。产品负责人答「1、3、4、5、6、9 都按推荐，全部改 MIT」（S1-DEC-036 D10、D11）。许可一条取「全部改 MIT」，不取初稿推荐的 Apache-2.0；其余五条按推荐。
  1. 新定位的表述按 D23.6。
  2. 许可：全部改 MIT，含 .NET 包族、命令行与前端包（D23.11）。
  3. 发布渠道按 D23.12：平台档在对外发布时点之前只发 GitHub Packages，内核档照旧发 nuget.org，到时点后全族上 nuget.org。
  4. 归档 Kun.Saga、Kun.Sandbox、Kun.Dapper、Kun.Infrastructure 与旧线 Kun.Storage（D23.9）。
  5. 命名小项：权限族用 `Kun.Authorization.Rbac`，配置的平台包叫 `Kun.Configuration.Platform`，`Registry`、`Lifecycle` 两个根不加 `Plugins.` 中缀。
  6. Kun 旧 ADR 的编号按 D23.13 第 4 点。
- **D23.20 §7 第 7、8 条**。产品负责人答「第 7 条改为 Guid，第 8 条按推荐」，随后补充「或者使用支持分布式的雪花算法也可以，要考虑分布式」（S1-DEC-036 D12、D13）。
  1. 租户上下文端口的属性类型由字符串改为 `Guid`，在数据域归并批次里落地（D23.5）。Guid 与雪花算法两者里取 Guid：Qian 的租户号现在用 Guid v7 生成，任何副本不需要协调就能生成，并且按时间有序；contracts/v0 已冻结的服务间令牌与事件信封把 `tenant_id` 定为 Uuid，改用雪花算法要发契约的新主版本；雪花算法的 WorkerId 要靠 Redis 租约分配，租约不可用时生成会失败。
  2. 分布式 ID 的现行做法随合并保留（[PRD 7.6](/design/prd/07-s1-foundation.md)）：默认 Guid v7；需要数值主键的地方用雪花算法，WorkerId 经 Redis 租约分配，副本之间互不相同。Kun 一侧现在没有雪花算法的实现，沿用 Qian 的。
  3. 冻结窗口放在「初步可用」里程碑（S1-DEC-033）当前在途任务收口之后（D23.15）。
  4. iClaw 的安排按 D23.16 的推荐：归并开始前先让它钉住合并前的最后一个 Kun 版本。这一项单独确认，产品负责人答「按推荐」（S1-DEC-036 D14）。

### 其余条目（随本文确认）

- **D23.6 新定位**。表述（已确认，D23.19）：

  > Kun 是为 Agent 时代构建的多租户平台框架（.NET 10）。它由三部分组成：一组不带产品语义、可以单独安装的内核包；一组以组件形式装配的平台组件（租户、身份、权限、数据、文件、配置、事件、审计、能力目录）；以及组件运行时、命令行与目录市场。前端使用 qian 组件库（原名 yue，D23.17）。

  三层口径（ADR-021 D21.1）改为：Kun 是框架，内部分内核档与平台档；qian 是前端层（原名 yue，D23.17）。「Qian」不再是平台层的名字，其余用法见 D23.10。

- **D23.7 两档分层**。合并后「Kun 不带产品语义」不再靠仓库边界保证，改由档位与门禁保证。

  | 档位  | 判据                                                                      | 规则                                                        |
  | --- | ----------------------------------------------------------------------- | --------------------------------------------------------- |
  | 内核档 | 通用机制：错误模型、中介、租户上下文、事件总线、EF 约定、令牌校验、网关、存储后端、配置源。不知道租户目录、人员、角色、组件清单这些产品概念 | 可以在任意 .NET 10 应用里单独安装使用；不得引用平台档的包；可以自带组件外壳，前提是外壳只依赖内核档的契约 |
  | 平台档 | 带产品语义：租户主档与开通、身份服务、权限引擎、文件服务、能力目录、注册表、生命周期、调度                           | 以组件形式装配；只依赖内核档与别的平台组件的契约包，不依赖别的平台组件的实现包                   |

  ADR-020 D20.4 的 seam 三角色（契约、提供者、消费者）原样保留，适用于平台档。ADR-019 D19.6「Kun 与 Qian 的分界」由本条取代。AOT 与档位无关，另见 D23.14。

- **D23.8 命名规则**。
  1. 一个职责一个名字根。Kun 已有的根沿用：`Configuration`、`Storage`、`EventBus`、`Authorization`、`Identity`、`Gateway`、`Audit`、`MultiTenancy`、`EntityFrameworkCore`。只在 Qian 的职责沿用 Qian 的根：`Runtime`、`Errors`、`Plugin.Sdk`、`Data`、`Tenancy`、`Files`、`Capabilities`、`Registry`、`Surfaces`、`Profiles`、`Lifecycle`、`Scheduler`、`Docs`。`Config`、`Events`、`Authz` 三个根不再使用。
  2. 后缀：`.Abstractions` 是契约，无后缀是缺省实现，`.AspNetCore` 是 HTTP 适配，按技术命名的提供者用技术名（`.EntityFrameworkCore`、`.Postgres`、`.RabbitMQ`、`.Apollo`）。
  3. 上下两层各用各的根，不合成一个名字：`Kun.MultiTenancy` 回答「当前请求是哪个租户」，`Kun.Tenancy` 回答「有哪些租户、什么状态」；`Kun.Storage.*` 是存储后端，`Kun.Files.*` 是带配额与策略的文件服务。
  4. 类型名里的 `Qian` 与包名同批改，撞名处在各域批次里逐个定。
  5. 组件 id 与包名脱钩。组件 id 保持 `qian.<x>`，不再由包名机械派生，清单的 `runtime.nuget[]` 显式列包名（[specs/component-model.md](/design/prd/specs/component-model.md) §6.1 已允许不同名）。

- **D23.9 逐模块归并对照**。按下表合计 84 个包（内核档 40、平台档 44），现在两边合计 91 个。「归档」指移出解决方案与发版清单，源码留在 git 历史里，nuget.org 上已发布的版本不动。档位在各批次开工时按依赖图复核。

  **运行时基础与错误模型**

  | 合并后的包                     | 来源                             | 档位       | 归并动作                                                                         |
  | ------------------------- | ------------------------------ | -------- | ---------------------------------------------------------------------------- |
  | Kun.Errors                | Qian.Runtime.Errors            | 内核       | 两边各有一套错误模型，留 Qian 的线上模型（错误码、目录、ErrorDetail），做成零依赖包，Kun.Core 引用它              |
  | Kun.Errors.AspNetCore     | Qian.Runtime.Errors.AspNetCore | 内核       | 两边各有一套异常转 HTTP 响应，留 Qian 的；Kun.AspNetCore 里的异常处理器归档                          |
  | Kun.Core                  | Kun.Core                       | 内核       | `Result<T>` 留作进程内返回类型，`Error` 改为携带错误码；两个异常合一，两边之间的桥删除；Kun 内置错误码改成大写下划线并登记进目录 |
  | Kun.Mediator              | Kun.Mediator                   | 内核       | 留 Kun 的；登记改为可叠加，给权限行为开正式扩展点，静态能力目录并入平台档的能力目录                                 |
  | Kun.Validation            | Kun.Validation                 | 内核       | 留 Kun 的；字段错误改用字段级错误码与 JSON Pointer                                           |
  | Kun.AspNetCore            | Kun.AspNetCore                 | 内核，非 AOT | 只留端点映射；权限扩展移到授权域                                                             |
  | Kun.Runtime               | Qian.Runtime                   | 内核       | 留 Qian 的组件运行时；表面键与 scope 种类带产品语义，上移平台档                                       |
  | Kun.Plugin.Sdk            | Qian.Plugin.Sdk 的声明与契约         | 内核       | 从现在的包里拆出                                                                     |
  | Kun.Plugin.Sdk.Commands   | Qian.Plugin.Sdk 的 EF 事务管道      | 平台，非 AOT | 拆出后 EF 不再随 SDK 进网关的原生发布闭包                                                    |
  | Kun.Plugin.Sdk.Generators | Qian.Plugin.Sdk.Generators     | 内核       | 改名；生成器里写死的类型全名同步改                                                            |
  | Kun.Plugin.Sdk.AspNetCore | Qian.Plugin.Sdk.AspNetCore     | 平台       | 改名                                                                           |
  | Kun.Caching               | Kun.Caching                    | 内核       | 原样                                                                           |
  | Kun.Observability         | Kun.Observability              | 内核       | 原样；并入代码的活动源名补进名单                                                             |
  | 归档：Kun.Infrastructure     | —                              | —        | 已标废弃且被 Qian 禁用；前置是数据域先摘掉旧仓储与规约求值器                                            |

  **数据、租户、权限**

  | 合并后的包                                                       | 来源                                                                                          | 档位       | 归并动作                                                                                                                        |
  | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------- | -------- | --------------------------------------------------------------------------------------------------------------------------- |
  | Kun.MultiTenancy                                            | Kun.MultiTenancy，加 Qian.Tenancy 里的上下文传播与严格入口边界                                              | 内核       | 入口处建立租户范围的代码现有三份，并成一份；租户号只收 Guid、去掉缺省回落（D23.5）；隔离策略枚举两份并成一份                                                                 |
  | Kun.EntityFrameworkCore                                     | Kun.EntityFrameworkCore、Kun.MultiTenancy.EntityFrameworkCore、Qian.Data.Abstractions 的 EF 部分 | 内核，非 AOT | 三处并成一个包。基类型留两个：宽松的 `KunDbContext`，以及构造时强制校验隔离装配的严格基类（原 `QianDbContext`，另起名）。Kun 内部重复的租户过滤与缓存键工厂各留一份；`plugin_` schema 前缀改为可配 |
  | Kun.EntityFrameworkCore.PostgreSQL                          | Qian.Data 的行级隔离迁移生成器                                                                        | 内核，非 AOT | 新包                                                                                                                          |
  | Kun.Data.Native.Abstractions、Kun.Data.Native.Postgres       | Qian.Data.Native.\*                                                                         | 内核       | 改名；这是不经 EF 的原生通道，网关的原生发布在用                                                                                                  |
  | Kun.Data.Abstractions                                       | Qian.Data.Abstractions 里不带 EF 的契约                                                           | 平台       | 现在的契约包拆成两半后的一半                                                                                                              |
  | Kun.Data、Kun.Data.AspNetCore、Kun.Data.Dialect.Pg            | 同名 Qian 包                                                                                   | 平台       | 改名                                                                                                                          |
  | Kun.Tenancy.Abstractions、Kun.Tenancy、Kun.Tenancy.AspNetCore | 同名 Qian 包                                                                                   | 平台       | 改名；租户开通的步骤表留在这里                                                                                                             |
  | Kun.Tenancy.Signup.Abstractions、Kun.Tenancy.Signup          | 同名 Qian 包                                                                                   | 平台       | 改名                                                                                                                          |
  | Kun.Authorization                                           | Kun.Authorization                                                                           | 内核       | 端口原样                                                                                                                        |
  | Kun.Authorization.Rbac.Abstractions、Kun.Authorization.Rbac  | Qian.Authz.Abstractions、Qian.Authz                                                          | 平台       | 改名；过滤表达式与判定主体两边各有一份类型，合一；引擎不再按类型名字符串挂接中介的内部类                                                                                |
  | Kun.Authorization.AspNetCore                                | Qian.Authz.AspNetCore                                                                       | 平台       | HTTP 与 gRPC 的强制执行点两边各有一套，留 Qian 的；`RequireKunPermission` 与 `RequireQianPermission` 并成一个                                     |
  | 归档：Kun.Dapper                                               | —                                                                                           | —        | Qian 没有使用方，职责与原生通道重叠；分页类型如仍需要，并入原生通道                                                                                        |

  **身份、安全、网关**

  | 合并后的包                                | 来源                                                  | 档位       | 归并动作                                                            |
  | ------------------------------------ | --------------------------------------------------- | -------- | --------------------------------------------------------------- |
  | Kun.Security                         | Kun.Security，加 Qian 的 Principal、claim 词汇表与服务间令牌契约校验 | 内核       | 服务间令牌的契约规则现在在签发、校验、投影三处各写一遍，并成一份；「当前用户」的多个实现并成一个；已废弃的旧签发与校验目录归档 |
  | Kun.Identity                         | Kun.Identity                                        | 内核       | 留 Kun 的协议与会话存储；已废弃的签发类型删除；依赖方类型迁到根命名空间，给平台契约让出名字                |
  | Kun.Identity.AspNetCore              | Kun.Identity.AspNetCore、Qian.Identity.Client 的逻辑    | 内核       | 现在是三层，并成两层，以 Qian.Identity.Client 的逻辑为主                         |
  | Kun.Identity.Abstractions            | Qian.Identity.Abstractions 里的人员、成员、连接器契约            | 平台       | Principal 一组下沉到 Kun.Security 后的剩余部分                             |
  | Kun.Identity.Client                  | Qian.Identity.Client 的组件外壳                          | 平台       | 只剩薄外壳；若外壳不再依赖平台契约，则并入 Kun.Identity.AspNetCore                   |
  | Kun.Identity.Server                  | Qian.Identity.Server                                | 平台，非 AOT | 改名；迁移快照里写死的实体全名同步改，库表结构不变                                       |
  | Kun.Identity.Connector.Oidc          | Qian.Identity.Connector.Oidc                        | 平台       | 改名；它与 Kun 的 OIDC 客户端是同一协议的两份实现，先逐条比对校验规则再合一                     |
  | Kun.Gateway、Kun.Gateway.Abstractions | Kun.Gateway、Qian.Gateway、Qian.Gateway.Abstractions  | 内核       | Kun.Gateway 由可执行程序改为纯库并吸收 Qian.Gateway；两个选项类型合一，以声明式的那份为准       |
  | Kun.Gateway.Shell                    | Qian.Gateway 里的外壳接口与静态托管                            | 平台       | 拆出                                                              |
  | Kun.RateLimit                        | Kun.RateLimit、Kun.Gateway 的限流、Qian.Gateway 的限流      | 内核       | 限流现有三套，留一套：端口取 Kun.Gateway 的，Redis 实现取 Qian 的                   |
  | Kun.Grpc                             | Kun.Grpc                                            | 内核       | 留结果映射与追踪；认证与权限拦截器以 Qian 的为准，并支持租户为空的平台主体                        |

  **事件、审计、编排与调度**

  | 合并后的包                                                             | 来源                                                 | 档位       | 归并动作                                                                     |
  | ----------------------------------------------------------------- | -------------------------------------------------- | -------- | ------------------------------------------------------------------------ |
  | Kun.EventBus.Abstractions                                         | Kun.EventBus.Abstractions、Qian.Events.Abstractions | 内核       | Qian.Events 是套在 Kun.EventBus 上的一层，不是第二套总线；信封、来源作用域、拓扑命名并入                |
  | Kun.EventBus                                                      | Kun.EventBus、Qian.Events 的信封写入端与事件目录               | 内核       | 信封写入端成为缺省写入端；`OutboxMessage` 的命名空间不动，它已写进 EF 快照                          |
  | Kun.EventBus.EntityFrameworkCore                                  | Qian.Events 的 Outbox 投递循环                          | 内核，非 AOT | 投递循环两边各有一套，留 Qian 的（领取即提交、按租约逐条标记、停机交还）；Kun 的那套归档                        |
  | Kun.EventBus.RabbitMQ                                             | Qian.Events 的发布连接与订阅器                              | 内核       | 发布与订阅两边各有一套，Kun 的那套 Qian 没有引用；包名留给 Qian 的实现，Kun 现有的发布器与订阅器归档             |
  | Kun.Audit.Abstractions                                            | Qian.Audit.Abstractions                            | 内核       | 命令级哈希链两边各有一套，留 Qian 的（与业务同事务，有平台链与验链，两个宿主在用）；`Kun.Audit` 这个包名停发          |
  | Kun.Audit.EntityFrameworkCore                                     | Qian.Audit，加 Kun.Auditing 的事务拦截器                   | 平台，非 AOT | Kun 的链原语归档；实体级审计 Qian 还没做，以 Kun.Auditing 的事务拦截器为起点；`Kun.Auditing` 这个包名取消 |
  | Kun.Audit.Postgres                                                | Qian.Audit.Native                                  | 内核       | 改名；不引用 EF，网关的原生发布在用                                                      |
  | Kun.Lifecycle.Abstractions、Kun.Lifecycle、Kun.Lifecycle.AspNetCore | 同名 Qian 包                                          | 平台       | 只改名                                                                      |
  | Kun.Scheduler.Abstractions、Kun.Scheduler                          | 同名 Qian 包                                          | 平台       | 只改名；Kun 没有对应模块                                                           |
  | 归档：Kun.Saga                                                       | —                                                  | —        | 带补偿的编排器，Qian 的租户开通是只进不退的幂等步骤，不迁上去；没有使用方，需要时再恢复                           |

  **配置、存储与文件、测试支持**

  | 合并后的包                                                                                  | 来源                                                                       | 档位                | 归并动作                                                  |
  | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ | ----------------- | ----------------------------------------------------- |
  | Kun.Configuration                                                                      | Kun.Configuration、Qian 的强类型配置入口                                          | 内核                | 强类型配置两边各有一套；以 Qian 的委托绑定为主入口，Kun 的反射重载保留并标注；两个配置监视器合一 |
  | Kun.Configuration.Apollo                                                               | Kun.Configuration.Apollo，加 Qian.Config.Apollo 的源顺序、引导校验、密钥引用、快照扫描        | 内核                | 协议客户端留 Kun 的；Qian 的装饰层下沉后删除                           |
  | Kun.Configuration.Secrets                                                              | Qian.Config.Secrets                                                      | 内核                | 改名                                                    |
  | Kun.Configuration.Abstractions、Kun.Configuration.Platform、Kun.Configuration.AspNetCore | Qian.Config.Abstractions、Qian.Config.Apollo 的平台部分、Qian.Config.AspNetCore | 平台                | 配置服务与开关的端口、发布记录入审计链、草稿管理、三个组件                         |
  | Kun.Storage.Abstractions、Kun.Storage.Local、Kun.Storage.S3、Kun.Storage.Oss              | Kun 存储新线                                                                 | 内核，S3 与 Oss 非 AOT | 留 Kun 的；本地后端的签名与校验从 Qian.Files 下沉                     |
  | Kun.Files.Abstractions、Kun.Files、Kun.Files.AspNetCore                                  | 同名 Qian 包                                                                | 平台，非 AOT          | 改名；Qian 自立的后端端口收敛到 Kun 的存储接口上                         |
  | Kun.Testing、Kun.Testing.EntityFrameworkCore                                            | 同名 Kun 包                                                                 | 内核                | 原样；它们是断言扩展                                            |
  | Kun.Testing.Containers                                                                 | tests/Qian.Testing.Containers，加 Kun 测试目录里的对象存储与连接池夹具                     | 内核                | 容器夹具两边各有一套，留 Qian 的并作为新包发布；带产品语义的夹具留在仓库内部             |
  | 归档：Kun.Storage（旧线）、Kun.Sandbox                                                         | —                                                                        | —                 | 旧线已标废弃；沙箱没有使用方                                        |

  **只在 Qian 的平台组件**

  | 合并后的包                                                                      | 来源        | 档位       | 归并动作                         |
  | -------------------------------------------------------------------------- | --------- | -------- | ---------------------------- |
  | Kun.Capabilities.Abstractions、Kun.Capabilities、Kun.Capabilities.AspNetCore | 同名 Qian 包 | 平台，非 AOT | 改名；Kun.Mediator 里已有的静态能力目录并入 |
  | Kun.Registry.Abstractions、Kun.Registry、Kun.Registry.AspNetCore             | 同名 Qian 包 | 平台       | 改名                           |
  | Kun.Surfaces.Abstractions、Kun.Surfaces、Kun.Surfaces.Hosting                | 同名 Qian 包 | 平台       | 改名                           |
  | Kun.Profiles.Abstractions、Kun.Profiles                                     | 同名 Qian 包 | 平台       | 改名                           |
  | Kun.Docs.Abstractions、Kun.Docs                                             | 同名 Qian 包 | 平台       | 改名；构建期工具，不进宿主                |

  宿主、工具、样例随所在域的批次改名：`Qian.Gateway.Host`、`Qian.Platform.Host`、`tools/` 下 13 个项目、`Qian.Sample.Crm`；Kun 的样例、基准与事件 Schema 导出工具留下。

  停发的 Kun 包名共 8 个：Kun.Infrastructure、Kun.Dapper、Kun.Saga、Kun.Sandbox、Kun.Storage（旧线）、Kun.Auditing、Kun.Audit、Kun.MultiTenancy.EntityFrameworkCore。

- **D23.10 名字面：哪些跟着改，哪些不改**。「qian」在仓库里共出现在 1,903 个文件里，包名只是其中一小部分。改名的代价主要在已经落库、上线、冻结的字面上。推荐按两类处理。

  | 类别         | 内容                                                                                                                                                                                                                                                                                     | 处理               |
  | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------- |
  | 代码层        | .NET 包名、命名空间（873 处声明、2,142 处 using）、类型名里的 `Qian`（117 个公共标识符）、分析器诊断号、MSBuild 属性、文档站站名与三层口径的叙述                                                                                                                                                                                           | 跟着改              |
  | 组件生态与运行期标识 | 组件 id 前缀 `qian.`（约 5,600 处，派生出库 schema 名、令牌 scope、配置命名空间、审计链）；库 schema `plugin_qian_*`（37 个迁移文件全部涉及）；Cookie 名、交换机名、Redis 键前缀；契约标识 `https://qian.dev/…` 与 `x-qian-*` 扩展键（contracts/v0 已冻结）；清单与配置文件名 `.qian-plugin/plugin.json`、`qian.profile.yml`、`qian.patch.yml`、`.qian/`；部署命名空间与镜像标签 | 不改，与包名脱钩         |
  | 前端         | yue 全部包名、目录、样式变量前缀、测试标识、根脚本与工作流名、UI 清单键 `engines.yue`                                                                                                                                                                                                                                  | 改名为 qian（D23.17） |
  | 不涉及        | 不带 qian 的标识（租户 GUC、claim 名、内置错误码、队列名）                                                                                                                                                                                                                                                  | 不动               |

  由此推荐：「Qian」不再是框架或包族的名字，留作组件生态与官方平台发行版的名字，上表第二类标识因此仍有意义。`qian` 命令行与 `@qian/*` 导入名属于组件生态，本次不改。另一个选择是彻底不用这个名字，代价是第二类全部迁移：改库 schema 要迁移数据，改 Cookie 名在线会话全部失效，改契约标识等于发 contracts 的新主版本。这一条与 S1-DEC-034 的总名 Yi 一并由产品负责人定（§7 第 2 条）。

  D23.17 之后 qian 是前端框架的名字，上面这条推荐要重看。第二类标识不改的理由是迁移代价，这一点不变；变的是「官方平台发行版叫 Qian」会让一个名字既指前端框架又指平台发行版。更名评估的推荐是：Qian 只作前端框架的名字；第二类标识当作历史字面保留，不再解释为产品名；平台发行版不单独起名。`plugin.schema.json` 的 `engines.qian` 现指平台运行时版本，仓库里没有清单在用，推荐改指前端，平台运行时版本并入 `engines.kun`。产品负责人已裁定按这一条办（D23.18），初稿「留作组件生态与官方平台发行版的名字」不再采用。

- **D23.11 许可**。一个包族一个许可。推荐 Apache-2.0：ADR-020 §7 第 6 条为平台、命令行与 yue 选它的理由（含专利授权）仍然成立，合并后的代码大部分来自 Qian。Kun 已按 MIT 发布的旧版本不受影响，仓库保留 Kun 原 MIT 声明。另一个选择是全部改 MIT。产品负责人裁定全部改 MIT（D23.19）：.NET 包族、命令行与前端包都用 MIT。已按 Apache-2.0 发布的 `Qian.*` 0.2.0 与 npm 包的旧版本不受影响。MIT 不带明文专利授权，ADR-020 当时选 Apache-2.0 的这条理由不再适用。ADR-019 的许可证条目与 ADR-020 §7 第 6 条已标注改为 MIT；S3 商业市场与计费组件的许可证仍到 S3 再定。

- **D23.12 版本与发布渠道**。
  1. 版本线用 Kun 现行的日历版本 `YYYY.M.D` 与同日修复版规则。`Kun.*` 在 nuget.org 已发到 2026.10.25，不能回到 0.x。`Qian.*` 的语义版本锁步、`KunVersion` 单点钉版与 `Kun.*` 包源映射（ADR-021 D21.3）随合并作废。
  2. 已发布的 `Qian.*` 0.2.0 包停止更新，在 GitHub Packages 上标弃用并指向新包名，不发转发包。
  3. 发布渠道分两步。nuget.org 上的包发了不能删，所以平台档在对外发布时点（ADR-019 §7：S2 末或首个外部宿主试用时）之前只发 GitHub Packages，内核档照旧发 nuget.org；到时点后全族上 nuget.org。包源映射按包名单生成。
  4. 发版流水线取 Qian 的做法：手动触发、要求同一提交的 CI 已通过、干净消费者验证、从包源真实还原运行；版本号规则与 nuget.org 推送取 Kun 的。锁步的包从 35 个增加到 84 个，推送不是原子的，先推 GitHub Packages 再推 nuget.org。

- **D23.13 仓库布局与历史**。
  1. 目标布局：`src/` 放全部 .NET 包，一个包一个目录；`tests/` 放测试、TCK 与门禁自测；`hosts/`、`samples/`、`tools/`、`benchmarks/`；`packages/` 只留前端工作区（前端 qian（原名 yue）、TS 运行时、插件 SDK、命令行）；`apps/`、`website/`、`contracts/`、`schemas/`、`plugins/`、`profiles/`、`tck/`、`deploy/`、`docs/`、`pm/`、`scripts/`。
  2. 两边顶层有 14 个同名项，不能直接叠在一起。分两步：先保留历史把 Qian 整棵树导入 kun 仓库的一个子目录；再随各域批次把项目挪到目标位置，全部挪完后子目录消失。
  3. 历史 PR 号：任务文件里有 721 处落在 #62–#321，kun 仓库今后会把这些号发给别的 PR。导入时把任务文件与文档里的历史 `#N` 改写为 `idadawn/qian#N`，`idadawn/qian` 归档为只读。
  4. ADR：Qian 的 ADR-001 至 ADR-023 保留编号，它们被 PRD 与任务大量引用。Kun 的 17 份 ADR 有 15 个编号与之重叠，移入 `docs/adr/kun/`，引用时写「Kun ADR-0xx」。此后新 ADR 接着 ADR-024 编。
  5. 看板整体搬入，任务 id 不变；看板、文档站、发版工作流与部署清单里 92 个文件写着的 `idadawn/qian` 改指 kun 仓库。文档站「Kun 框架」镜像小节（ADR-021 D21.4）撤销，改为同仓来源；Kun 文档里写给特定下游的内容改写后再进站。
  6. Kun 仓库里的自管任务队列与第三方生成的代理配置删除，任务管理统一到 `pm/`。

- **D23.14 门禁与构建基线并轨**。
  1. 依赖门禁改为显式的「包 → 档位与角色」表，写在项目属性里。现在的两道门禁按 `Qian.` 前缀和 `.Abstractions` 后缀判定，包名一改就会静默失效，所以这一步排在任何改名之前，并带自测。规则是 D23.7 的两条，加 Kun 现有的「Kun.Core 零依赖」检查；Kun 文档里的四层方向没有机器检查，由档位规则取代。
  2. 公共 API 门禁取 Qian 的做法：可打包的项目必须带两份清单，缺文件即报错。分析器升到 5.6.0。
  3. AOT：缺省不声明，逐项目显式打开；开关移到晚于项目文件求值的位置；是否兼容以宿主原生发布并运行的冒烟为准，不以标记为准。Kun 现有四份互不一致的 AOT 模块清单并成一份。
  4. 禁用符号与数据层分析器现在对全部项目生效，直接套到 Kun 源码上会拦住被禁类型自己的定义。被禁的类型大多在归档名单里；其余的把规则作用域收到平台档与宿主。
  5. 中央包版本：两边同名不同版的 19 个包全部取高的一边。三处跨主版本（Microsoft.OpenApi、JsonSchema.Net、xunit.v3）要实测。测试运行器统一到 Microsoft.Testing.Platform，Kun 工作流里的过滤与覆盖率参数重写。
  6. CI 全部跑在 GitHub 托管 runner 上，自托管 runner 只留基准与对象存储实测。Kun 的格式门禁先不对并入的 Qian 代码打开。

- **D23.15 分步与时点**。

  1. **阶段 0，蓝图确认**。本文转 Accepted，回写 ADR-001、ADR-019、ADR-020、ADR-021 与两个仓库的 `AGENTS.md`。本仓库的部分已于 2026-11-17 做完；kun 仓库的 `AGENTS.md` 由 S1-KUN-018 在该仓库回写。阶段 1 至阶段 4 的任务是 S1-KUN-019、S1-KUN-020、S1-KUN-021、S1-YUE-022、S1-KUN-022。
  2. **阶段 1，冻结与导入**。前提：开着的 PR 全部合入或关闭，各会话分支收口。保留历史导入；看板、文档站、发版工作流改指 kun 仓库；`idadawn/qian` 归档只读。这一步不改任何包名。
  3. **阶段 2，构建并轨**。一个解决方案；Qian 对 Kun 由包引用改为项目引用；中央包版本取高；门禁改成显式档位表；CI 并轨。仍不改包名。退出标准：两边全部测试与门禁在一条流水线上通过。
  4. **阶段 3，按域归并与改名**。一域一批，自底向上：错误模型与运行时基础 → 租户上下文与数据 → 事件与审计 → 安全、身份、网关 → 权限 → 配置、存储与文件、测试支持 → 只在 Qian 的平台组件及宿主、工具、样例。每批的退出标准：该域每个职责只剩一套公共 API，归档的类型移出解决方案，公共 API 清单更新，全量测试通过。同一时间一个域只有一个归并批次，功能任务避开正在归并的域。
  5. **阶段 4，首个合并版**。按日历版本发版；`Qian.*` 0.2.0 标弃用；文档站改写。

  前端更名（D23.17）排在阶段 3 的最后一批（已裁，D23.18）：后端各域的 `Qian.*` 都改完之后做，赶在阶段 4 之前，文档站只改写一遍。提前做的话，前端的 qian 会与尚未改名的 `Qian.*` 并存。这一批要同时做完的事：8 个包以新名首发并删除已发的 3 个 `@idadawn/yue-*` 0.1.0 私有包；外壳与远程模块同批重建；UI 清单键与 TCK 用例一起换；页头带 yue 字样的整页视觉基线重拍。

  阶段 1 起需要冻结窗口，放在「初步可用」里程碑（S1-DEC-033）当前在途任务收口之后（已裁，D23.20）。工作量是数周量级，没有拆解估算；阶段 2 结束后按域建任务再估。

- **D23.16 对 Kun 现有使用方的影响**。首个合并版对 nuget.org 上 `Kun.*` 的现有使用方是破坏性版本：内置错误码改名、`Error` 形状变；租户号只收 Guid、无缺省回落，租户上下文端口的属性类型改为 `Guid`；Kun.Gateway 由可执行程序变为库；RabbitMQ 的交换机、路由键与消息体改为 Qian 现值；Kun.Audit.EntityFrameworkCore 与 Kun.EventBus.RabbitMQ 两个包名换了实现；8 个包名停发。旧版本留在 nuget.org 不动。iClaw 以项目引用消费 Kun 源码（ADR-001 §1），用到的模块见备忘录 §3.1，归并会动到其中多个；其现状属私有仓库，不写进本文。归并开始前先让它钉住合并前的最后一个 Kun 版本（已裁，D23.20）。

## 3 理由（Rationale）

- **重叠是实际存在的，不只是名字相近。** 分域核实确认两边各有一套的地方有：错误模型、异常转 HTTP 响应、强类型配置、Outbox 投递循环、RabbitMQ 发布与订阅、命令级审计链、权限强制执行点、OIDC 协议客户端、测试容器夹具；另有限流三套、入口处建立租户范围的代码三份、服务间令牌的契约规则三处。原样搬运会把这些原封不动地放进同一个包族。
- **机械改名会直接撞名。** `Kun.Config.Apollo` 会与 `Kun.Configuration.Apollo` 并存，`Kun.Audit`、`Kun.Gateway` 两个包名各有两份实现，`Kun.Identity.Abstractions` 命名空间里有两个同名类型。所以命名规则与归并必须一起定。
- **留哪一套看使用与测试。** 两边各有一套时，留有宿主在用、有测试覆盖的一边。Kun 的协议客户端、存储后端、令牌签发与密钥环、中介、缓存留 Kun 的；错误模型、审计链、投递循环、消息发布订阅、强制执行点留 Qian 的。
- **档位取代仓库边界。** 「框架不带产品语义」原来靠两个仓库隔开。合并后只用内核的使用方与原生发布的网关仍然需要这条边界，所以用档位写下来、用门禁守。
- **已落库与已冻结的标识不值得改。** 组件 id 派生出库 schema、令牌 scope 与审计链；改它们需要数据迁移与契约升版，却不带来功能。把它们与包名脱钩，改名就只剩机械工作。
- **分步是为了每一步都能验证。** 先导入、再并轨、最后按域归并，每一步结束时两边的测试都在同一条流水线上通过，出问题时知道是哪一步引入的。

## 4 备选方案与放弃原因（Alternatives）

- **Kun 并入 qian 仓库，包名与分层不动**（主会话最初的推荐）：搬动量小、不牵动在途工作。产品负责人未采纳。
- **只合仓，不改包名**：产品负责人选了包名并成 `Kun.*`。
- **机械改名，不归并**：违背 D23.3，并且会出现上面说的撞名与两套并存。
- **用名字前缀分档**（平台档统一叫 `Kun.Platform.*`）：门禁与包源映射会简单一些，但等于给重叠的两份实现各留一个名字，鼓励并存而不是归并。档位写在项目属性里已经够用。
- **一次改完全部包名**：一个提交改两千多处引用，出错时无法定位，也没法与功能开发并行。按域分批。
- **彻底去掉「Qian」这个名字**：见 D23.10，代价是数据迁移、会话失效与契约升版，产品负责人未采用（D23.18）。

## 5 后果与代价（Consequences）

- 正面：一套词汇、一次发版；跨层修复在一个 PR 里完成，不再走 Kun 发版与版本升级两轮；文档单一来源；上面列的重复实现各收成一份。
- 负面：阶段 1 需要冻结窗口，阶段 3 期间功能开发要避开正在归并的域；已发布的 `Qian.*` 0.2.0 包作废；Kun 现有使用方面对一次破坏性升级；锁步发版的包数翻了一倍多；「内核不带产品语义」从此只靠门禁保证。
- 未核实事项（§1.2）可能改变个别模块的归并动作与档位，各批次开工时先补核实。

## 6 对各阶段的影响

- **S1**：「初步可用」里程碑先行；阶段 0 可并行。阶段 1 至阶段 3 占用 S1 的一段时间，S1 剩余任务的排期要重排。
- **S2**：元数据与动态数据层直接建在合并后的包族上，不再有跨仓库的版本钉。
- **S3**：第三方只面对一个包族、一个版本号；对外发布时平台档上 nuget.org。
- **S4**：Agent 表面与能力目录在平台档，内核档不加 AI 语义。

## 7 待确认事项

十条已于 2026-11-17 全部确认，逐条的结论如下。

1. 新定位的表述：已裁（2026-11-17，D23.19），按 D23.6。
2. 「Qian」这个名字：已裁（2026-11-17，D23.18）。只作前端框架的名字，第二类标识当作历史字面保留，平台发行版不单独起名，`qian` 命令行与 `@qian/*` 本次不改。未裁的只剩 S1-DEC-034 的总名 Yi 之下怎么写。
3. 许可：已裁（2026-11-17，D23.19），全部改 MIT。
4. 发布渠道：已裁（2026-11-17，D23.19），平台档在对外发布时点之前只发 GitHub Packages。
5. 归档名单：已裁（2026-11-17，D23.19），归档 Kun.Saga、Kun.Sandbox、Kun.Dapper、Kun.Infrastructure、旧线 Kun.Storage。
6. 命名小项：已裁（2026-11-17，D23.19），权限族用 `Kun.Authorization.Rbac`；配置的平台包叫 `Kun.Configuration.Platform`；`Registry`、`Lifecycle` 两个根不加 `Plugins.` 中缀。
7. 租户上下文端口的属性类型：已裁（2026-11-17，D23.20），由字符串改为 `Guid`。
8. 冻结窗口的时点：已裁（2026-11-17，D23.20），放在「初步可用」里程碑当前在途任务收口之后。iClaw 的安排：已裁（2026-11-17，D23.20），归并开始前先钉住合并前的最后一个 Kun 版本。
9. Kun 旧 ADR 的编号处理：已裁（2026-11-17，D23.19），按 D23.13 第 4 点。
10. 前端更名的小项：已裁（2026-11-17，D23.18），包名、`engines` 键、历史与兼容、排期全部按推荐。

## 8 参考

- 裁决任务：[S1-DEC-036](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-036.md)；相关：[S1-DEC-033](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-033.md)（里程碑顺序）、[S1-DEC-034](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-034.md)（总名 Yi）、[S1-DEC-035](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-035.md)（备忘录待拍板项，其中 Q4 的 Kun 路线由本文取代）。
- 相关 ADR：[ADR-001](/design/adr/adr-001-kun-relationship.md)、[ADR-004](/design/adr/adr-004-database-tenant-isolation.md)、[ADR-013](/design/adr/adr-013-frontend-yue.md)、[ADR-019](/design/adr/adr-019-library-first-packages.md)、[ADR-020](/design/adr/adr-020-component-model-v2.md)、[ADR-021](/design/adr/adr-021-product-family-structure.md)。
- 规格：[specs/component-model.md](/design/prd/specs/component-model.md) §6（seam 三角色与包命名）。
