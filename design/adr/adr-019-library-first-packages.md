> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# ADR-019 库优先（Library-first）与 Qian 包族

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-019-library-first-packages.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-019-library-first-packages.md)，请修改源文件，不要改本页。

> **2026-10-06 经 ADR-020 修订。** 本 ADR 保留为「代码部分的打包规则」：D19.2 / D19.4 中 `AddQian<Component>()` 与「内核接管」的装配表述改为「`Apply(ctx, config)` 内部的装配细节」，平台模式 = 组件以插件形式挂进 `Qian.Runtime` 的插件树；组件库唯一必需的 Qian 依赖是 `Qian.Runtime`（极小、无业务语义），且不依赖 `qian.registry` / `qian.lifecycle` 等插件（取代「不依赖 `Qian.Kernel` 运行时」的表述）；§7 悬置的组件安装入口与声明格式由 ADR-020 定义（`qian add <component>`、`.qian-plugin/plugin.json`、`.qian/marketplace.json`）。详见 ADR-020 D20.4、D20.10 与 §6。

## 1 背景与问题（Context）

ADR-002 / ADR-003 把 Qian 的一切能力组织为插件，S1 的 18 个系统插件以 `kind: module` 编译期并入 `qian-platform` 等宿主。在此前的表述下，一个外部系统若想复用 Qian 的文件、鉴权、身份等通用能力，只能作为客户端通过 OIDC + HTTP API 调用 Qian 平台，组件本身不能脱离平台单独使用。

产品负责人于 2026-10-06 给出方向修正：更想要的是「别人安装一个 NuGet 包就可以直接使用，尤其是通用的一些组件」。即 Qian 的通用组件应当像 `Microsoft.Extensions.*` 或 Kun 的 `Kun.*` 一样，加入依赖后即可在任意 ASP.NET Core 应用中使用，而不是只能在 Qian 平台内以插件形式存在。

这与既有决策并不冲突，而是对「插件的物理形态」的补充：Kun 本身就是「每个能力一个 NuGet、一个显式注册入口」的框架（ADR-002 §1）；ADR-002 D2.6 已定清单由源生成器从代码派生；ADR-013 D13.12 已定 yue 以 npm 包分发。缺的是把「库可独立使用」写成明确的架构约束与验收条目，并约束组件库对内核的依赖方向——否则各组件库很容易在实现中悄悄依赖 `Qian.Kernel` 运行时，使「独立安装即用」在事实上失效。

同日产品负责人进一步提出，组件模型要参考「声明式组件 + 一键安装 + 目录市场 + 一切皆插件」一类的设计。因此本 ADR 只定**库的形态与依赖规则**；组件的安装入口、声明格式与目录市场由后续 ADR 定义（见 §7）。

## 2 决策（Decision）

- **D19.1 库优先原则**：每个 Qian 系统组件首先是一个可在任意 ASP.NET Core（.NET 10）应用中独立安装使用的 NuGet 包，其次才通过插件清单成为 Qian 插件。**插件 = 库 + 清单**：清单描述库、不替代库。Qian 平台 = `Qian.Kernel` + 这些库的装配。前端 yue 同理：`@yue/*` 以 npm 包分发、可在任意 React 应用中独立安装使用（ADR-013 D13.12、`specs/yue.md`）。
- 【经 ADR-020 修订：`AddQian<Component>()` 为 `Apply` 内部细节；唯一必需依赖改为 `Qian.Runtime`】**D19.2 包命名与分层**：每个组件两层包——`Qian.<Component>.Abstractions`（只含接口 / 契约 / DTO，仅依赖 `Kun.*.Abstractions`）→ `Qian.<Component>`（实现 + .NET 宿主的底层装配入口 `services.AddQian<Component>(options)` + EF 迁移随包）。清单相关的源生成器与生命周期钩子基类放在 `Qian.Plugin.Sdk`【R94：源生成器按 PRD 7.2.3 以 `Qian.Plugin.Sdk.Generators` 独立开发依赖包发布，与 `Qian.Plugin.Sdk` 同版本锁步、消费者同时引用】；组件库本身**不得依赖 `Qian.Kernel` 运行时**。清单由源生成器从代码派生（ADR-002 D2.6），独立使用时清单被忽略。`AddQian<Component>()` 只是 .NET 宿主的底层装配方式，不是面向开发者的安装入口；安装入口见 §7。
- **D19.3 S1 包清单**（与 PRD §07 7.x 一一对应）：`Qian.Runtime`（唯一必需依赖）、`Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities`（只进 `platform` profile 的平台插件，不供消费者独立安装；R34）、`Qian.Plugin.Sdk`、`Qian.Tenancy`、`Qian.Identity.Server`（可嵌入的 OpenIddict IdP，含 Person / Membership 与连接器 SPI）、`Qian.Identity.Client`（RP 接入、Cookie 会话、S2S 校验）、`Qian.Identity.Connector.Oidc` / `Qian.Identity.Connector.WeCom`、`Qian.Authz`、`Qian.Data`（含 `Qian.Data.Dialect.Pg`）、`Qian.Files`（含 `Qian.Files.Oss` / `.S3` / `.Local`）、`Qian.Config.Apollo`、`Qian.Gateway`、`Qian.Events`、`Qian.Audit`、`Qian.Observability`、`Qian.Notify`（含 `Qian.Notify.Channel.Inbox` / `.Email` / `.Sms`）、`Qian.Scheduler`、`Qian.Realtime`、`Qian.Entitlement`、`Qian.ModelGateway`（P1）。每个实现包都配套同名 `.Abstractions` 包。
- 【经 ADR-020 修订：平台模式 = 挂进 `Qian.Runtime` 插件树】**D19.4 两种运行模式，同一份代码**：
  - **独立模式**（宿主应用只装 `Qian.Runtime`、未挂载 `qian.registry` / `qian.lifecycle` 等平台插件）：租户上下文由宿主提供 `ITenantContext` 实现，或使用内置 `SingleTenant` 默认；权限点通过代码特性 `[Permission]` 注册，角色与授权使用库自带的表与 API；身份可用任意 OIDC 发行方（`Qian.Identity.Client`）或嵌入 `Qian.Identity.Server`；配置用标准 `IConfiguration`，Apollo 可选；事件 Outbox 用宿主自己的 DbContext。
  - **平台模式**（宿主装配了内核）：内核接管注册表、两层生命周期、租户级配置、能力目录与权益开关。
  - 模式切换不改业务代码；两种模式由同一份库代码在装配期分叉。
- **D19.5 依赖与隔离规则**：组件库只依赖 `Kun.*` 与 `Qian.*.Abstractions`；组件库彼此之间只通过 Abstractions 引用（例如 `Qian.Files` 依赖 `Qian.Authz.Abstractions` 暴露的 `IPermissionChecker` 端口，不依赖 `Qian.Authz` 实现）；每个库只拥有自己的 PG schema；多租户默认安全装配（fail-closed、RLS 装配，ADR-003 D3.8）在独立模式下同样生效，宿主必须显式选择 `SingleTenant` 才能关闭。
- **D19.6 与 Kun 的分界**：无产品观点的通用基础设施（租户上下文原语、Outbox、RLS 拦截器、存储抽象、网关加固、可观测）上游进 Kun；带多租户 / 业务语义的组件（租户主档、IdP、RBAC 引擎、带配额与策略的文件服务、消息、调度）是 Qian 包。此句待回填为 ADR-001 的补充条目。
- **D19.7 版本与分发**：Qian 包族采用 SemVer，同一版本号锁步发布（类似 `Microsoft.Extensions.*`）；S1 发布到 GitHub Packages（`Qian.*` NuGet 包随公开仓库 `idadawn/qian` 公开可见、安装需 GitHub 令牌，R93；原「私有 feed」口径 2026-10-16 修订）；许可证 Apache-2.0，公开到 nuget.org / npm 的时点见 §7（S2 末或首个外部宿主试用时）。公共 API 变更由 `Microsoft.CodeAnalysis.PublicApiAnalyzers` 守门（覆盖全部 `Qian.*` 包，扩展 PRD §14 C1）；TCK 增加「独立模式」套件。
- **D19.8 S1 验收**：
  - [ ] 一个空白 ASP.NET Core 项目安装 `Qian.Files` + `Qian.Authz` + `Qian.Identity.Client` 三个包、不部署 Qian 平台，照 README 在 30 分钟内跑通文件上传与权限检查；样例 `samples/standalone-minimal` 入 Qian 源码仓。
  - [ ] 同一组包在 `qian-platform` 宿主中以平台模式运行，行为一致（TCK 两种模式都过）。运行时侧的 T23 已由 S1-QA-002 用 CRM 样例组合与 TCK 平台组合包验证；本条点名三包的平台模式对照由 S1-QA-003 / S1-QA-005 承担（S1-DEC-019 R68）。
  - [ ] 任何 `Qian.<Component>` 包的依赖图中不出现 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities`（CI 用 `dotnet list package --include-transitive` 断言）。
  - [ ] 嵌入 `Qian.Identity.Server` 的独立宿主能通过 OpenIddict 一致性测试。
- **D19.9 明确不做**：不提供「拆单个库出来替代 Qian 平台」的支持承诺（独立模式是能力，不是另一个产品）；不为独立模式做低代码运行时（S2 的 metadata / dynamic-data / forms 库可以装，但设计器与运行时需要内核）；非 .NET 消费者沿用 OIDC + HTTP API。

## 3 理由（Rationale）

- 通用组件被别人直接装进自己的代码库才有生态；「只能当客户端调平台 API」把复用门槛抬到了「先部署整套平台」，与产品负责人的方向不符。
- Kun 已经是 NuGet 包族形态（ADR-001 D1.2），Qian 延续同一消费方式，开发者心智一致。
- 「清单从代码派生」（ADR-002 D2.6）天然支持「库先于清单」：清单是库的派生物，去掉清单库仍然完整。
- 组件库不依赖内核运行时是「内核最小集」（ADR-003 D3.1）的另一面：内核只留契约、强制执行点与注册表，本就不该被业务组件反向依赖；这条规则还顺带阻止内核膨胀。
- 独立模式仍强制多租户安全默认值，是因为 Qian 的差异化之一就是「多租户与鉴权不可绕过」（PRD §05）；独立使用者需要显式选择 `SingleTenant`，而不是默认拿到一个 fail-open 的库（ADR-001 D1.5 的强制装配口径）。

## 4 备选方案与放弃原因（Alternatives）

- **只提供平台 API，外部系统作为客户端**：复用门槛 = 部署整套平台；通用组件无法进入他人代码库。放弃（即本 ADR 要修正的方向）。
- **把组件全部上游进 Kun**：Kun 定位是框架，不做 IdP / 权限模型 / 租户目录（ADR-001 §3），且单人维护；带产品语义的组件进 Kun 会模糊两者边界。放弃，只把无产品观点的基础设施上游。
- **一个大包 `Qian.All`**：引入全部依赖、无法按需装配、`Qian.Registry` 等平台插件隐式带入。放弃作为主形态；是否提供元包作为便利见 §7。
- **源码分发 / 模板拷贝**：与 ADR-001 D1.2「禁止 ProjectReference / 源码复制」冲突，升级不可控。放弃。

## 5 后果与代价（Consequences）

- 正面：通用组件可被团队内外任何 .NET 10 应用直接使用；平台本身也是这些库的消费者，「吃自己的狗粮」更彻底；依赖方向约束使内核边界可用 CI 检验。
- 负面：每个组件多一层 Abstractions 包与一份 README / 样例，S1 工作量增加；两种模式的装配分叉需要双倍 TCK 覆盖；SemVer 锁步发布意味着任一包的破坏性变更都会推高整个包族的主版本。
- 配套：`samples/standalone-minimal`；每个包的 README 含独立模式最小装配；CI 依赖图断言；PublicApiAnalyzers 覆盖全部 `Qian.*` 包；GitHub Packages feed 与发布流水线；TCK 独立模式套件。

## 6 对各阶段的影响

- **S1**：D19.3 全部包以两种模式交付；验收 D19.8 四条；发布到 GitHub Packages。
- **S2**：`Qian.Metadata`、`Qian.DynamicData`、`Qian.Forms`、`Qian.Workflow` 等按同一分层发布、可独立安装，但设计器与低代码运行时需要内核（D19.9）。
- **S3**：第三方 `service` 插件与 ISV 开发者只依赖 `Qian.*` 包与 `Qian.Plugin.Sdk`；是否公开 feed 须在 S3 之前拍板。
- **S4**：Agent 门面同样库优先；MCP 门面不属于独立模式。

## 7 待确认事项（2026-10-07 已拍板）

- **安装入口与组件模型**：已由 ADR-020 定义（`qian add`、`.qian-plugin/plugin.json`、`.qian/marketplace.json`）；D19.2 / D19.4 的装配表述以 ADR-020 为准。
- **许可证**：Apache-2.0（Qian 包族、CLI、yue），理由与可回退条件见 ADR-020 §7 第 6 条；S3 商业组件另定。
- **分发渠道**：S1 GitHub Packages（.NET 包公开可见、安装需令牌，R93；TS 包私有，ADR-021 D21.6）；S2 末或首个外部宿主试用时公开到 nuget.org / npm（ADR-020 §7 第 6 条）。
- **「外部应用清单」**：非 .NET 外部应用以「只含声明的组件文件夹」接入（无 `src/`，只有 `metadata/`、`tools/`、`ui/` 等表面），这在 ADR-020 D20.6 下已是常态，不需要单独机制；S1 不专门交付，S3 随 ISV 入驻给出模板。
- **元包**：S1 不提供 `Qian.All` / `Qian.Platform`；profile 的组合包已承担「一组包一起装」的职责。
- **`IPermissionChecker` 端口归属**：`Kun.Authorization` 保持端口定义（ADR-006 D6.1），`Qian.Authz.Abstractions` 只依赖并再导出该端口，不重复定义。
- **包名与插件 ID 的映射**：写在 `.qian-plugin/plugin.json` 的 `runtime.nuget[]` / `runtime.npm[]`（包名 + 版本 + digest），不在 Schema 里加 `assembly` 约束；非同名处以清单为准。
- **本 ADR 状态**：2026-10-07 转为 Accepted，细则已由 ADR-020 与 specs/component-model.md 吸收。

## 8 参考

- 与其他 ADR 的关系：

| ADR                | 关系                                       | 待回填内容                                                                      |
| ------------------ | ---------------------------------------- | -------------------------------------------------------------------------- |
| ADR-001 Qian 与 Kun | D19.6 补 Kun / Qian 包族分界                  | 增补条目：无产品观点的基础设施进 Kun，带多租户 / 业务语义的组件是 `Qian.<Component>` 包                  |
| ADR-002 插件模型       | D19.1 补充插件的物理形态                          | 插件定义处补「插件 = 库 + 清单」                                                        |
| ADR-003 内核边界       | D19.2 / D19.5 约束依赖方向                     | 内核定位处补「内核对组件库是可选的」                                                         |
| ADR-005 身份与 SSO    | D19.3 `Qian.Identity.Server` / `.Client` | 补「`Qian.Identity.Server` 可嵌入任意 ASP.NET Core 宿主；平台部署形态仍是独立 `qian-identity`」 |
| ADR-006 鉴权模型       | D19.4 独立模式权限点注册                          | 补「独立模式下权限点经 `[Permission]` 特性登记，角色与授权用库自带表与 API」                           |
| ADR-013 前端与 yue    | D19.1 前端对应                               | D13.12 处呼应「`@yue/*` 以 npm 独立可用」                                            |

- PRD：`PRD §02`（设计原则，待补「库优先」）、`PRD §03`（待补「插件 = 库 + 清单」与独立 / 平台模式）、`PRD §07`（待补 S1 NuGet 包 ↔ 章节对照与验收）、`PRD §11`、`PRD §12`、`PRD §14`、`PRD §16`；`specs/plugin-manifest.md`、`specs/yue.md`。
- 决策清单：`docs/decisions/key-decisions-v0.md` D19。
