---
title: 组件模型 v2：一切皆插件、声明式组件、一键安装、目录市场
status: Proposed
date: 2026-10-06
decision_id: D20
supersedes: ADR-002 §D2.1/D2.9/D2.10（形态轴与两层生命周期）、ADR-003 §D3.1–D3.4（内核定位与固定可部署单元）；修订 ADR-014、ADR-019
---

# ADR-020 组件模型 v2

## 1 背景与问题（Context）

2026-10-06 产品负责人给出两条连续的方向修正：第一，通用组件（登录、权限、文件上传等）要做到「别人安装一个包就能直接用」；第二，组件化设计要对标当代 Agent 生态里的三类做法，「不要再做传统设计」。所指的三类做法分别是：

- **文件即组件、一条命令安装**：组件目录按领域分文件夹，每个组件是一个带 front matter 的 Markdown 或一份 JSON（agent、command、skill、hook、MCP、settings），用一条 `npx <cli>@latest --agent x --command y --mcp z --yes` 装进项目，另有目录网站、健康检查与分析面板。没有构建步骤，组合就是多个 flag 并列。
- **插件 = 文件夹 + 清单 + 若干声明式表面**：`plugins/<name>/` 下一个 `plugin.json` 清单，旁边是 `skills/`、`.mcp.json`、`agents/`、`commands/`、`hooks.json`、`assets/`；清单描述展示信息（displayName、category、defaultPrompt、品牌色）与各表面的指针；市场是一份 `marketplace.json`，条目只写来源（本地路径、URL、git 子目录）、安装与认证策略、分类。
- **一切皆插件的运行时**：没有需要打补丁的特权内核；插件向共享上下文贡献服务（`ctx.<key>`）、类型化事件和可逆的副作用，用 `inject` 声明依赖而不是手工编排启动顺序；能力按「Service Definition + Provider + Consumer」三角色拆分，换一个 Provider 就改变整个产品；运行中的应用是一棵由 profile、组合包与 patch 层叠出的插件树，一条命令就能打印整棵树并替换任意条目；注册可按 scope 作用域化并遮蔽全局项；文档（配置目录、模块图、事件图）由代码生成并有 CI 新鲜度门禁。

对照现有决策，Qian 的 ADR-002「kind × role × distribution 三轴 + 两层生命周期」与 ADR-003「内核只留契约、强制执行点、注册表；S1 固定 5 个可部署单元 + 18 个系统插件」是典型的传统平台设计：清单是手写的 YAML，顶层按七种物理形态分类，租户开关落在数据库，安装入口是 DI 扩展方法与 Helm，Agent 相关的 skill / agent / hook 要到 S4 才出现。它们与上述三类做法的差距不是细节，而是组织方式。本 ADR 用一份决策把组件模型切到新范式，并给出对既有 ADR 与 PRD 的修订清单。Kun 仍是底座（ADR-001 不变），多租户、SSO、鉴权、K3s、Apollo 的硬要求不变。

## 2 决策（Decision）

- **D20.1 一切皆插件，没有特权内核。** Qian 的运行时只剩一个极小的 `Qian.Runtime`：上下文（`ctx`）、服务注册、类型化事件、可逆副作用、加载器。租户、身份、鉴权、文件、配置、网关、事件、审计、低代码运行时、Agent 门面全部是插件，向共享上下文贡献服务（`ctx.Tenancy`、`ctx.Identity`、`ctx.Authz`、`ctx.Files`、`ctx.Config`、`ctx.Events`…）、事件与可撤销的注册。原 `qian.kernel` 承担的注册表、生命周期控制器、能力目录、契约仓库改为普通插件（`qian.registry`、`qian.lifecycle`、`qian.capabilities`），每一项都能从配置替换。扩展 Qian 的方式是把插件挂到其他插件旁边，而不是修改内核。
- **D20.2 插件的最小接口是 `Inject` + `Apply`。** 插件声明所依赖的服务键（`Inject`），依赖就绪才启动，装载顺序由依赖关系表达；`Apply(ctx, config)` 在上下文里注册服务、监听器与效果。所有注册都是副作用，经 `ctx.Effect()` 返回释放函数，在卸载、重载与 teardown 时按注册的逆序撤销。.NET 落地为 `IQianPlugin`（基于 .NET DI keyed services 与 `Kun.Mediator`），TypeScript 落地为 `@qian/runtime`（yue 外壳也是一棵插件树），两侧语义相同。
- **D20.3 事件即扩展点，五种分发模式。** `emit`（观察）、`waterfall`（环绕中间件，监听器必须调用 `next()` 才委托）、`parallel`（并行扇出）、`serial`（按序）、`bail`（停在首个返回值）。每个事件在定义处标注模式，CI 校验声明与调用点一致，并生成「事件生产方与消费方」映射。拦截与策略优先用事件，直接能力调用优先用服务方法。
- **D20.4 能力按 seam 三角色拆分。** 一项能力 = Service Definition（拥有 `ctx.<key>` 与词汇类型，放在 `Qian.<X>.Abstractions`）+ 一个或多个 Service Provider（`Qian.<X>.<Provider>`）+ 一个或多个 Consumer（含面向模型的工具）。扩展插件只依赖 Definition，绝不依赖具体 Provider。例：`ctx.Files` 的 Provider 从本地盘换成 OSS，所有消费者不变；`ctx.Identity` 的 Provider 从嵌入式 OpenIddict 换成外部 IdP 连接器，登录页与会话中心不变。ADR-019 的 Abstractions / 实现分层据此落位。
- **D20.5 作用域两层扁平。** 任一注册（服务、工具、提示词片段、表单、流程、菜单、监听器）要么是全局的，要么归属恰好一个 scope key；S1 的 scope 种类为租户、应用、Agent 会话。带作用域的注册在该 scope 内遮蔽（shadow）同名全局项；`restrict` 为某个 scope 过滤全局集合，多个 restriction 取交集；被过滤掉的项既不出现在目录与提示词里，也拒绝执行，与不存在无法区分。scope 不继承：子 scope 的行为用 lineage 数据表达，从不用 scope 层级。这取代 ADR-002 D2.9 的租户级生命周期：**租户启用一个插件 = 在租户 scope 挂载它的配置条目；租户定制 = 带作用域的遮蔽注册**。
- **D20.6 组件 = 文件夹 + 清单 + 声明式表面 + 可选代码。** 目录约定如下；无代码的组件（只有 skill、只有配置）是常态而非例外。

  ```text
  <plugin>/
    .qian-plugin/plugin.json      清单：id、version、interface、surfaces 指针、runtime、inject/provides、policy、scopes
    skills/<name>/SKILL.md        技能（Agent 可读，含 front matter：name、description、allowed-tools、metadata）
    agents/<name>.md              子 Agent 定义（front matter + 系统提示）
    commands/<name>.md            人类斜杠命令（命令平面，不进入模型消息；支持 $ARGUMENTS 与内联 shell）
    hooks/hooks.json              钩子：Qian 事件与 Agent 宿主事件（PreToolUse 等）的桥接
    .mcp.json                     MCP 服务器声明
    tools/                        面向模型的工具描述（由代码派生或手写声明）
    ui/                           yue 远程模块、页面、控件、设置卡
    forms/ lists/ flows/ pages/   低代码元数据（S2 起）
    schemas/                      JSON Schema
    config/qian.patch.yml         对其他条目的 patch（组合包用）
    assets/  README.md  AGENTS.md
    src/                          可选：.NET 或 TypeScript 代码 → NuGet / npm
  ```

  `plugin.json` 的字段族：`id`（反向域名，发布后不可改）、`version`（SemVer）、`description`、`author`、`license`、`interface`（`displayName`、`category`、`defaultPrompt[]`、`icon`、`brandColor`、`screenshots`）、各表面指针（`skills`、`agents`、`commands`、`hooks`、`mcpServers`、`tools`、`ui`、`metadata`）、`runtime`（`nuget[]` / `npm[]` 包名与版本、`form: module | service | function | ui | package`）、`inject[]` / `provides[]`（服务键）、`policy`（`tier`、`sandbox`、`authentication: ON_INSTALL | ON_USE`、`profiles[]`）、`scopes[]`（允许挂载的 scope 种类）。**原 ADR-002 的 `kind` 七值退为 `runtime.form` 的实现细节，不再是顶层分类；顶层分类改为「表面（surface）」。** 代码部分的清单字段仍由源生成器从代码派生，但只合并进这一份 `plugin.json`，不再存在手写 YAML 与派生文件并存。
- **D20.7 Profile、组合包与 patch 分层。** 运行中的 Qian 是一棵插件树，由启动时按序叠加的层组成：profile（具名组装；S1 随发行版交付 `platform`、`gateway`、`identity`、`standalone`、`minimal`）→ profile 列出的组合包（配置条目 + 挂载代码的分发格式）→ profile 自己的 `qian.patch.yml` → home 级 patch → 命令行 `--patch` overlay。一条 patch 按 id 定位某个条目并替换其整个 config，或插入新条目。`qian --profile platform --dump-config` 打印整棵树，打印出的任何条目都可被 patch 替换。租户层是租户 scope 的 patch 层：存放在数据库，但与文件 patch 同一格式、同一校验器。**这取代 ADR-003 把「5 个可部署单元 + 18 个系统插件」写死的做法：可部署单元 = profile；系统插件 = `platform` profile 的 base 组合包内容，数量由组合包定义而不是决策。** 热重载：声明式表面（skills、commands、hooks、MCP、元数据、patch）S1 支持运行中重载；代码插件的热装卸（collectible `AssemblyLoadContext`）为 S2 可选，默认走进程重启。
- **D20.8 一键安装与目录市场。** `qian add <component>[@version]`（等价 `npx qian@latest --skill x --agent y --plugin z --yes`）把声明式表面复制进项目的 `.qian/` 目录、把代码部分加为 NuGet / npm 依赖、把配置条目写进当前 profile 的 patch；配套 `qian remove`、`qian list`、`qian doctor`（健康检查）、`qian dump-config`、`qian init --template <stack>`（按技术栈脚手架）。目录市场是 git 仓库里的一份 `.qian/marketplace.json`：`name`、`interface.displayName`、`plugins[]`，每个条目只有 `name`、`source`（`local` 路径 / `url` / `git-subdir`）、`policy`（`installation`、`authentication`、`profiles`）、`category`、`interface`。多市场并存：官方市场仓库、租户私有市场、社区市场（GitHub topic `qian-plugin` 发现）。目录网站由 marketplace.json 生成。**ADR-014 据此修订：应用包格式 = 组件文件夹 + marketplace 条目；OCI 制品只用于代码部分；S3 的商业市场（授权、计费、审核）叠在同一格式之上，不另起格式。**
- **D20.9 Agent 原生表面是一等公民，从 S1 开始。** skill、agent、command、hook、MCP 服务器、tool、提示词片段在 S1 就是受支持的表面，不是 S4 才有。所有模型可见的东西都注册在 `ctx.Tools` 等注册表里并受 scope 过滤；`AGENTS.md` 进每个仓库与每个包；包 README 契约包含「模型体验」节与「已知限制与延期项」节；配置目录、模块图、事件图、能力 seam 图由代码生成并设 CI 新鲜度门禁。S4 的 `extensions` 能力（模型在策略下挂载与卸载插件，运行时自修改）建立在同一套装载器上，S1 只开放给人类 CLI。
- **D20.10 与 Kun、NuGet 的关系。** ADR-019 保留为「代码部分的打包规则」：有代码的组件发 `Qian.<X>.Abstractions` 与 Provider 包，`AddQian<X>()` 只是 `Apply` 内部的装配细节，不是开发者入口；`Qian.Runtime` 是组件库唯一必需的 Qian 依赖（极小、无业务语义），组件库不依赖 `qian.registry` / `qian.lifecycle` 等插件。Kun 的 `AddKunXxx()` 模块在 Qian 里被包装为插件，`Kun.*` 不改。Kun 的严格 AOT 约束与动态插件树矛盾的部分这样处理：`gateway` 等边缘 profile 可 AOT，`platform` profile 不承诺 AOT（与 ADR-001 对 AOT 承诺的修订一致）。
- **D20.11 明确不做。** 不再按物理形态做顶层分类；不把「预装全部 module + 数据库开关」作为租户启用的唯一路径（S1 预装仍是 `platform` profile 的默认组合包，但租户开关改为 scope patch）；不维护第二套清单格式；不把 Cordis 的 TypeScript 源码搬进 .NET（.NET 侧自研同语义运行时；TypeScript 侧是否直接采用 Cordis 见 §7）；不在 S1 做代码插件热装卸；不为第三方代码打开共享进程（ADR-002 D2.4 的信任等级规则保留，映射到 `policy.tier` 与 `policy.sandbox`）。

## 3 理由（Rationale）

- **安装体验决定采用率。** 对标项目的共同点是「一条命令、不需要理解平台」。开发者安装 Qian 的登录或文件组件时，面对的应当是 `qian add identity`，而不是 Helm values、DI 扩展方法与 Apollo namespace。这正是「别人安装一个包就能直接用」的落点，ADR-019 只回答了包怎么分层，没有回答入口。
- **没有特权内核才能被 Agent 时代的生态接纳。** 一旦存在只能打补丁才能改的内核，第三方与 Agent 都只能在内核允许的位置扩展。以服务、事件、可逆副作用为唯一扩展机制，任何一项能力都能被替换、被遮蔽、被按 scope 收窄，这是 S4「Agent 发现、调用、编排乃至生成平台能力」的前提。
- **声明式表面让组件可读、可审、可生成。** skill、agent、command、hook 是 Markdown 与 JSON，Agent 能直接读写；低代码的表单、列表、流程在 S2 也落成同类表面。设计器的产出物因此天然就是组件，可以进 git、进市场、被 patch。
- **profile + patch 把部署从决策变成配置。** 固定 5 个可部署单元会在 S2 增加 JIT 宿主、流程服务时反复改 ADR；用 profile 表达后，新增一个可部署单元只是新增一个 profile 文件。
- **scope 统一了租户开关、租户定制与 Agent 工具集。** 原设计里租户开关走数据库状态机、租户定制走元数据分层、Agent 工具过滤走权限；三件事在新模型里都是「带作用域的注册 + restrict」，少了三套机制。

## 4 备选方案与放弃原因（Alternatives）

- **保留 ADR-002/003，只补一个 CLI。** 安装体验改善，但内核仍是特权层、租户开关仍在数据库、Agent 表面仍在 S4，与方向修正的「不要传统设计」不符。
- **直接采用 Cordis 作为 .NET 侧的运行时（经 Node 侧车或 JS 引擎）。** 与 ADR-002 D2.12「不引入进程共享的服务端 JS 引擎」冲突，也让 Kun 的 .NET 组件要跨进程调用。放弃；TypeScript 侧可单独评估。
- **用 .NET 现有的模块框架（ABP、Orchard Core 模块系统）。** 都是「模块 + 特权内核」的传统形态，没有 scope、可逆副作用与 patch 分层。放弃。
- **声明式表面全部交给 iClaw 托管，Qian 只做数据与权限。** 与 ADR-016 的边界一致，但组件无法在不部署 iClaw 的宿主里独立使用，违反 D19 库优先。放弃；Qian 的 skill / agent / command 表面是组件的一部分，iClaw 仍是运行这些表面的 Agent 运行时。

## 5 后果与代价（Consequences）

- **文档修订面大。** ADR-002 的 D2.1/D2.9/D2.10、ADR-003 的 D3.1–D3.4 被取代；ADR-014 的包格式、ADR-019 的装配表述被修订；PRD §03 概念、§07 的 7.0 与 7.2、§08 设计器产出物、§09 市场形态、§10 Agent 表面、§12 边界、specs/plugin-manifest.md 需按 §6 的清单修订。其余 ADR（身份、鉴权、数据、事件、审计、网关、流程、yue、模型网关、iClaw、部署）的技术结论不变，只是落位从「系统插件」变为「`platform` profile 的 base 组合包里的插件」。
- **S1 新增交付。** `Qian.Runtime`（.NET）与 `@qian/runtime`（TS）、`qian` CLI、官方市场仓库与 marketplace.json、五种声明式表面的解析器与校验器、profile / 组合包 / patch 装载器、`dump-config`、文档生成器与 CI 门禁。这些替换掉原 `qian.kernel` 与 `qian.plugin-sdk` 的大部分工作量，净增工作量集中在 CLI 与装载器。
- **自研运行时的风险。** .NET 没有现成的「可逆副作用 + scope 过滤分发」运行时，需要在 S1 第一波写出并用 TCK 固化语义；语义以本 ADR 与 specs/component-model.md 为准，不以某个实现为准。
- **热重载受限。** 代码插件在 .NET 里无法像 Node 一样轻量重载；S1 明确只对声明式表面承诺热重载，代码部分走滚动重启，避免对开发者许诺做不到的事。
- **租户开关从状态机变为 patch。** 原两层生命周期中的租户级状态（Available → Entitled → Enabled ⇄ Disabled → …）仍需要用于授权与计费（`qian.entitlement`、S3 市场），但「开没开」这一位由 scope patch 表达，状态机只保留「买没买、能不能」。

## 6 对各阶段的影响与修订清单

| 阶段 | 本 ADR 带来的变化 |
|---|---|
| S1 技术底座 | 交付 `Qian.Runtime` / `@qian/runtime`、`qian` CLI（add / remove / list / doctor / dump-config / init）、官方市场仓库与 marketplace.json、skills / agents / commands / hooks / MCP 五种表面、profile `platform` / `gateway` / `identity` / `standalone` / `minimal`、租户 scope patch、文档生成器与 CI 门禁；原 18 个系统插件成为 `platform` profile 的 base 组合包，各自按 seam 三角色拆包；退出标准新增「在空白项目里 `qian add identity authz files` 后 30 分钟内跑通登录、权限检查与上传」 |
| S2 低代码构建 | 表单、列表、流程、页面作为声明式表面进入组件目录；设计器的保存即产出组件文件夹；`qian-lowcode`、`qian-workflow` 以 profile 形式新增；代码插件热装卸可选 |
| S3 面向客户 | 商业市场（授权、计费、审核、ISV 入驻）叠在 marketplace.json 之上；`policy.authentication: ON_USE` 的连接器组件；租户私有市场 |
| S4 AI 原生 Agent | `extensions`：模型在策略下挂载与卸载插件；Agent 生成的组件落为草稿组件文件夹并经同一校验器；每租户 MCP 门面由 `ctx.Tools` 的 scope 视图派生 |

对既有文档的修订清单（由后续修订落实，本 ADR 不逐条改写）：

| 文档 | 修订要点 |
|---|---|
| ADR-002 | D2.1 形态轴退为 `runtime.form`；D2.9 租户级生命周期改为 scope patch；D2.10 预装 + 开关改为 profile 默认组合包 + scope patch；其余（信任等级、清单从代码派生、digest 引用、能力条目 Agent 字段、Wasm 阶段）保留并映射到新字段 |
| ADR-003 | 内核定位改为 `Qian.Runtime`；可部署单元改为 profile；系统插件数量不再是决策；强制执行点（鉴权、租户、审计）保留为 base 组合包里的 waterfall 监听器 |
| ADR-014 | 应用包格式 = 组件文件夹 + marketplace 条目；OCI 仅代码部分；商业市场叠加 |
| ADR-019 | D19.2 / D19.4 的装配表述改为「`Apply` 内部细节」；安装入口指向本 ADR |
| PRD §02 / §03 / §11 / §12 | 新增原则「一切皆插件」「声明式优先」；概念新增表面、scope、seam、profile、组合包、patch、市场条目；边界新增「组件库只依赖 `Qian.Runtime`」「声明式表面可热重载、代码不承诺」 |
| PRD §07 | 7.0 概览与 7.2 重写为运行时 + CLI + 市场 + 表面 + profile；各技术组件小节保留技术结论，落位改为 seam 三角色与 base 组合包 |
| PRD §08 / §09 / §10 | 设计器产出即组件；市场形态；Agent 表面前移到 S1 |
| specs/plugin-manifest.md | 由 specs/component-model.md 取代为权威规格，原文件保留为字段迁移对照（`kind` → `runtime.form`） |
| ROADMAP | S1 核心交付与退出标准按 §6 第一行更新 |

## 7 待确认事项

1. TypeScript 侧（yue 外壳、`qian` CLI）是直接采用 Cordis（MIT）还是自研同语义的 `@qian/runtime`；.NET 侧确定自研。
2. 清单文件名与格式：`.qian-plugin/plugin.json`（对齐对标项目）还是沿用 `qian-plugin.yaml`；本 ADR 默认前者。
3. `qian` CLI 的实现语言：Node（便于 `npx` 一键）并提供 `dotnet qian` 薄包装，还是 .NET global tool 为主；本 ADR 默认前者。
4. 官方市场仓库的名称与可见性（默认与 Qian 同组织下的独立仓库，S1 私有、S3 评估公开）。
5. 代码插件热装卸是否进入 S2 范围，以及 collectible `AssemblyLoadContext` 与 Kun AOT 约束的兼容验证归属。
6. 许可证与公开 feed（沿 ADR-019 §7）。

## 8 参考

- 对标项目的公开仓库与文档（组件目录与 CLI 一键安装；插件清单 `plugin.json` 与 `marketplace.json`；「一切皆插件」运行时、能力 seam、作用域、profile 与 patch 分层、文档生成与 CI 门禁）。
- ADR-001 Qian 与 Kun 的关系；ADR-002 插件模型；ADR-003 内核边界与 S1 服务拆分；ADR-014 应用市场形态；ADR-016 与 iClaw 的边界；ADR-019 库优先与 Qian 包族。
- PRD §03 核心概念模型、§07 S1 技术底座功能规格、specs/plugin-manifest.md（v1，待被 specs/component-model.md 取代）。
