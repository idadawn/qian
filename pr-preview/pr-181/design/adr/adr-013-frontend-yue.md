> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# ADR-013 前端与 yue

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-013-frontend-yue.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-013-frontend-yue.md)，请修改源文件，不要改本页。

> 2026-10-07 经 [ADR-021](/pr-preview/pr-181/design/adr/adr-021-product-family-structure.md) 修订：D13.18 文档站的覆盖范围从「Qian 平台与 yue 组件」扩为整个产品族，底座框架 Kun 以镜像小节进入第 3 部分（规则见 specs/docs-site.md §2b）。

## 1 背景与问题（Context）

参考平台前端是「两套并存」的反面教材：Vue 3 + 开源后台模板内核之上叠加另一套 UI 库封装与第三方表格，弹窗、表单、表格、Tree、Loading 都有两到三套实现；去重后组件与设计器全集约 251 条目，其中真正的设计器 8 个（基线 U §0 第 1 条）。控件三态（编辑 / 只读 / 单元格）各自硬编码，新增控件改三处；列表 PC / 移动两份完整配置（基线 F §6 第 7/9 条）。

iClaw 前端有好的工程规范与令牌思路（React 19 + TypeScript + Vite + Tailwind v4 + shadcn，console 版令牌架构，工程门禁），但没有组件库：`@iclaw/ui` 是 29 行占位，产品里真正在用的 shadcn 组件只有 `Toaster`，数据密集型控件全部缺失，Base UI 只用了 Dialog 一个原语（基线 U §0 第 2 条、§7.2）。shadcn 自 2026-07 起新项目默认 Base UI（基线 U §4）。前端插件化方面，Module Federation 2 的 `registerRemotes / loadRemote` 允许加载启动后才发现的远程，但 MF 零隔离，只能给可信插件（基线 P §9.1）。三条安全硬线：不用 `eval` 执行租户脚本；令牌不进 URL / iframe；前端权限裁剪只是体验层（基线 U §0 第 6 条）。

## 2 决策（Decision）

- **D13.1 技术栈**【文档工具部分已由 ADR-022 修订为统一 Rspress；下文保留原决策】（路线 A，沿用 iClaw 已验证组合）：React 19 + TypeScript + Vite + Tailwind v4 + Base UI + shadcn 源码自有化；Zustand + TanStack Query；react-router 7；i18next；Vitest + Playwright + axe；Lighthouse 预算；Changesets 发包；Storybook 10；API client 从 Kun OpenAPI 生成（orval / openapi-typescript）+ 契约快照测试。
- **D13.2 yue 六层 + 横切**：`@yue/tokens`（primitive → semantic → component 三层令牌、明暗两态）→ `@yue/core`（约 30 个基础组件，Base UI + shadcn 自有化）→ 后台通用层（`@yue/shell` AppShell、`@yue/data` DataTable 基于 TanStack Table v9 + Virtual、`@yue/form` Field + SchemaForm 基于 react-hook-form + Ajv、`@yue/charts` ECharts 6、`@yue/code-editor` Monaco + CodeMirror 6；Upload 分片 + MD5；Tree / Cascader / 日期）→ `@yue/biz` 业务选择器（组织 / 用户 / 角色 / 岗位 / 字典 / 接口选择）→ `@yue/runtime` + `@yue/widgets` 低代码运行时与物料 → `@yue/designer-*`；横切 `@yue/sdk`（注册表 API、PlatformClient、拦截器链、i18n、mock）、`@yue/agent`（S4）、`@yue/mobile`（S2 可选 / S3）。依赖方向严格自下而上，用 eslint-plugin-boundaries / dependency-cruiser 写死进 CI。
- **D13.3 只保留一套表单 / 表格 / 弹层引擎**贯穿 S1 后台 CRUD 与 S2 低代码运行时；业务组件只做叶子，不得新建引擎。
- **D13.4 外壳**：`@yue/shell` + Module Federation 2 运行时加载（S1 必做）+ 带版本号的插槽注册表（`qian.shell.nav/v1` 等，破坏性变更开 v2 并给弃用窗口）；共享依赖 `react / react-dom / @qian/yue / @qian/plugin-sdk` 设 singleton；清单声明 `engines.yue`；yue 严格 SemVer 且主版本升级有双版本并存期；只加载「本租户已启用 × 当前页面用得到」的贡献；宿主与插件之间走带作用域校验的桥接 API，插件 UI 消息视为不可信输入。
- **D13.5 不可信 UI**：第三方首选 Worker + 远程渲染（插件只产出受约束的 UI 描述，组件实现只在宿主），需要完整自由度的走 iframe + 受限 CSP（CSP 由清单生成）；S3 对外开放时必做；第三方不可信 UI 直接 MF 挂载不做。
- **D13.6 控件注册表与 yue 组件 props 的 JSON Schema 机器可读**：`registerWidget(type, {meta, Runtime, Designer, toSearchControl, toCellRenderer})`，meta 含 props JSON Schema、valueType、`devices[]`、能力位、默认运算符、authAware；一份注册表同时作设计器左栏与 `GET /registry/widgets`；设计器、远程渲染白名单、S4 Agent 生成 UI 共用一份契约。
- **D13.7 Base UI 验证门**（Q12）：S1 首迭代对 Menu / Select / Combobox / Tabs / Tooltip / Tree 做键盘与可访问性覆盖验证；通过则锁定，不通过则整体切 Radix；**不混用**；接受自建数据密集组件的交付风险。
- **D13.8 移动端**（Q13）：S2 只做 PC 响应式，预留端覆盖——一份 schema + `device` 维度覆盖，物料端支持矩阵显式降级；H5 / PWA 渲染器 S3 可选；小程序原生渲染本期不纳入。
- **D13.9 后台壳**（Q37）：统一一个壳，按「平台控制台 / 应用」上下文切换，与插件外壳模型一致；不采用三面新窗口。
- **D13.10 布局与偏好**（Q38）：S1 单一侧栏布局；多布局模式与偏好抽屉 S3 可选。
- **D13.11 富文本 / Markdown 编辑器**（Q39）：五份调研均未覆盖，S2 前补一次选型调研后再排期。
- **D13.12 分发渠道**（Q41）：私有 npm 为主——GitHub Packages，scope `@idadawn`，包名 `@idadawn/yue-<包>`，文档中的 `@yue/<包>` 是简称（2026-10-09 产品负责人拍板，见 §9）；shadcn 私有 registry + MCP Server 面向 ISV 与 Agent，S3 可选、S4 必做。
- **D13.13 令牌**：三层结构 S1 必做；DTCG 格式导出 S1 可选；命名禁同名异义，状态色成对，WCAG AA 对比度单测进 CI；`data-theme / data-brand` 由服务端按租户下发，插件只引用语义令牌。实现落点（S1-YUE-004，PR #32）：`@idadawn/yue-tokens` 的 `registry.json` 定义三层令牌，生成 `src/index.css`（语义层在 `html.dark, html[data-theme="dark"]` 下重定义）与首帧脚本 `theme-init.js`；对比度与同名异义由 `tokens.test.ts` 与 `check-tokens.mjs` 守住；文档站经 S1-DOCS-004 改用同一包（S1-DEC-019 R72）。
- **D13.14 联动与脚本三层**：声明式动作 → 可序列化表达式 DSL → QuickJS 沙箱脚本（白名单 API、超时、内存上限、不暴露令牌）；禁 `eval` / `new Function`。
- **D13.15 其他选型**：dnd-kit（包一层抽象）；`@xyflow/react` 12（S3 自由画布）；react-grid-layout；Univer 开源核心（排除 Pro 与商业包，xlsx 导出放后端）；pdfme；quickjs-emscripten；不用 Formily / amis / lowcode-engine / bpmn-js。
- **D13.16 安全硬线**：不用 `eval` 执行租户脚本；令牌不进 URL / iframe（一次性票据或 postMessage 握手）；前端权限裁剪只是体验层，鉴权在服务端；不在前端持有加密密钥；不持久化 token / 锁屏密码。
- **D13.17 为 S4 预留**：机器可读 manifest；带版本的 Schema 与校验器（Agent 生成物走同一校验再落库）；客户端动作 / 导航 / 命令注册表（名称、参数 JSON Schema、权限、人话描述）；SSE 事件 JSON Schema 为唯一真相源生成 TS。
- **D13.18 文档站（2026-10-07 产品负责人确认）**【经 ADR-022 收敛为唯一文档与演示体系，插件默认行为需按其验收补齐】：Rspress 2.0（MDX、React、与 Rslib 同属 Rsbuild 家族）作为 Qian 平台、yue 组件与底座框架 Kun（镜像小节，ADR-021）的使用文档站；开启 `llms` 输出 `llms.txt` / `llms-full.txt` 与每页 Markdown（SSG-MD），供知识库与 Agent 读取；`@rspress/plugin-preview` / `plugin-playground` / `plugin-api-docgen` / `plugin-typedoc` 承担组件活演示与 API 表；.NET 包 API 参考用 DocFX，平台 OpenAPI 用 Scalar；文档源放在各组件文件夹（README 契约见 ADR-020 D20.9），构建前汇总进站点；站内「问 AI」S2 用自有模型网关（ADR-015）与 iClaw（ADR-016）实现，不购买 SaaS。信息架构（七个部分）、组件页模板与 frontmatter 规范见 `docs/prd/specs/docs-site.md`。备选 Docusaurus 3.x；不选 dumi / VitePress / MkDocs（分别因绑定 umi、无 MDX）。

## 3 理由（Rationale）

- 两套体系并存是参考平台最大的前端债务；一套引擎是防复燃的唯一手段（基线 U §0 第 3 条）。
- Base UI 与 shadcn 自有化让组件源码在自己仓库，许可与供应链可控；数据密集组件在 headless 的 TanStack 上自建，避免 AG Grid Enterprise 式的许可陷阱（基线 U §4）。
- MF2 是「租户开通后才出现的插件 UI」唯一成熟的运行时加载方案；版本化插槽借鉴 Backstage 扩展点与 Grafana 静态声明（基线 P §9.1）。
- 统一一个壳与插件外壳模型一致，三面新窗口会使路由与状态割裂。

## 4 备选方案与放弃原因（Alternatives）

- **antd v6 二次封装**：只适合「S2 交付压力极大」时限平台控制台域使用并约定迁移期限；Q12 裁决接受路线 A 风险，不引入第二体系。放弃（保留为应急备选，需另行决策）。
- **Formily / amis / lowcode-engine**：停滞或自带整套 UI 与状态体系。放弃。
- **bpmn-js**：许可证水印要求。放弃。
- **两套元数据（PC / 移动各一份）**：参考平台反例。放弃。

## 5 后果与代价（Consequences）

- 正面：源码自有、许可可控；一份 Schema 驱动设计器、运行时与 Agent；令牌按租户注入使品牌切换零改动。
- 负面：数据密集基础组件（DataTable / Tree / Upload）的 a11y 与 i18n 质量是最大人力热点，与 S2 窗口冲突；MF 零隔离与共享依赖漂移需要严格 SemVer 纪律；供应链风险（dnd-kit 旧架构、Puck 0.x、CodeMirror 迁出 GitHub、Univer Pro 水印）需抽象层隔离。
- 配套：S1 最小集合（令牌、`@yue/core`、AppShell、DataTable 基础版、SchemaForm、Upload、业务选择器、认证页族、壳插槽注册表）；工程化基线 21 项；视觉回归。

过渡态（2026-10-06）：yue 尚不存在；iClaw 组件库为占位。S1 首迭代以验证门结果决定 Base UI / Radix。

## 6 对各阶段的影响

- **S1**：令牌三层、`@yue/core` 全集、`@yue/shell` 单一布局 + MF2 + 插槽注册表、DataTable / SchemaForm / Upload 基础版、业务选择器、认证页族、`@yue/sdk`、元数据契约草案。
- **S2**：控件集与注册表、表单 / 列表渲染器三态、子表、三层联动、流程表单宿主、三大设计器、组件 manifest 全量、富文本选型。
- **S3**：不可信 UI 沙箱、门户 / 报表 / 打印设计器、多布局与偏好、H5 可选、shadcn 私有 registry 可选。
- **S4**：`@yue/agent`、shadcn MCP Server、MCP Apps 视图、WebMCP 可选。

## 7 待确认事项

- ~~Base UI 验证门结果（S1 首迭代）~~ 2026-10-09 裁定：通过，锁定 Base UI；证据与发现见 §9「状态历史」。
- 富文本 / Markdown 编辑器选型（S2 前）。
- S3 移动端范围（审批 / 工作台 H5、小程序 / App 壳）。
- iClaw 前端代码：Q40 允许搬运的四处不含前端，yue 只借鉴令牌思路；shadcn 生成物可用 CLI 重新生成，不构成复用问题。

## 8 参考

- 相关 ADR：ADR-002、ADR-011、ADR-012、ADR-014。
- PRD：`PRD §07`、`PRD §08`、`PRD §11`、`PRD §12`。
- 基线：U §0、§2、§3.0–§3.8、§4、§5、§6、§7.2–§7.4；P §9.1、§9.2、§10.1 第 7 条；F §6 第 7/8/9 条。

## 9 状态历史（Status History）

### 2026-10-09 Base UI 验证门：通过，锁定 Base UI（S1-YUE-003）

**结论**：路线 A 成立。锁定 `@base-ui/react` 1.8.0 作为 `@yue/core` 的无样式原语，不切 Radix，不混用。D13.7 的待确认项关闭。

**验证范围与证据**（`packages/yue/core`、`packages/yue/data`、`apps/yue-playground`，CI 工作流 `yue`）：

| 原语       | 验证项                                                                                             | 结果       |
| -------- | ----------------------------------------------------------------------------------------------- | -------- |
| Menu     | Enter 打开并聚焦首项、方向键、Enter 选中后关闭回焦、子菜单 ArrowRight / ArrowLeft、Escape、禁用项 aria-disabled、受控 open     | 通过       |
| Select   | ArrowDown / Enter 打开 listbox、方向键高亮、Enter 选中回写、Escape 回焦、受控 value / 非受控 defaultValue             | 通过       |
| Combobox | 输入即过滤、方向键高亮（焦点留在输入框）、Enter 选中回写、空态、Escape                                                       | 通过（发现 3） |
| Tabs     | tablist / tab / tabpanel 关联、roving tabindex、方向键循环、Home / End、竖向、RTL、受控 value                    | 通过（发现 2） |
| Tooltip  | Tab 聚焦即显示、Escape 关闭、悬停打开、role=tooltip 与 aria-describedby                                        | 通过（发现 1） |
| Tree     | 自建：方向键 / Home / End / Enter / Space、展开折叠、roving tabindex、aria-level / setsize / posinset、RTL、受控 | 通过（发现 4） |

- 组件级：Vitest 5 浏览器模式（Playwright Chromium）21 个用例、SSR `renderToString` 2 个用例、门禁自测 10 个用例全部通过；每个原语打开态 axe（axe-core 4.11）critical + serious = 0，比 §4.4 的「critical = 0」更严。
- 页面级：Playwright + `@axe-core/playwright` 7 个用例全部通过（整页关闭态与六个原语打开态）。
- 无需 patch 包：下列发现全部在封装层处理。
- 体积：靶场首屏 script 压缩后约 151 KB（预算 250 KB，PRD §14）。按源码占比：react-dom 38%、Base UI 含 floating-ui 约 36%（Combobox 7%、Select 4.6%、Menu 4.4%、Tabs 2.4%、Tooltip 1.8%）、tailwind-merge 6%。

**发现**（记录为封装约定，不构成切换理由）：

1. **Tooltip 不带 ARIA 关联**。Base UI 有意不输出 `role="tooltip"` 与 `aria-describedby`（把提示视为非必要的补充信息）。WAI-ARIA tooltip 模式与 PRD §07 7.15.2 的 ARIA 验收都要求这两项，封装层在打开期间补上（`TooltipTrigger` 的 `aria-describedby` 指向 `TooltipContent`）。
2. **Tabs 默认手动激活**。方向键只移焦点、不切换；封装层默认 `activateOnFocus`（自动激活），需要手动激活时传 `activateOnFocus={false}`。禁用 tab 仍可被方向键聚焦（`aria-disabled`）但不激活，这是 Base UI 的可发现性策略，保留。
3. **Combobox 打开态把页面其余部分标为 aria-hidden**。输入框在弹层外时 Base UI 把弹层的焦点管理设为 modal（`!inputInsidePopup || modal`），其余内容被标 `aria-hidden` 而非 `inert`，axe 的 `aria-hidden-focus` 记为 serious；弹层在焦点移出时即关闭，键盘用户不会停留在被隐藏的内容里。页面级门禁只对 Combobox 打开态豁免该规则；S1-YUE-005 评估向上游反馈或改用输入内置弹层形态。
4. **没有 Tree 原语**。Base UI 与 Radix 都没有，与路线选择无关；Tree 自建在 `@yue/data`，用扁平 treeitem + `aria-level` 而非嵌套 group（嵌套会让父节点的可见文字包含整棵子树，触发 axe `label-content-name-mismatch`）。
5. **Base UI 要求 `date-fns` 作为可选 peer**（只有日期类原语用到）；S1 不装，等日期族时再定。

**后续**：S1-YUE-005 用 shadcn CLI 生成全集时沿用这里的封装约定（`data-slot` / `data-testid`、函数式 className 不透传、ARIA 补齐）；上述验证用例并入组件测试，不再单独维护。

### 2026-10-09 发包渠道：GitHub Packages，scope 改为 `@idadawn`（S1-YUE-003）

GitHub Packages 的 npm 仓库要求包 scope 与仓库所有者同名，`@yue/*` 发不上去。产品负责人拍板改 scope：包名 `@idadawn/yue-<包>`（`@idadawn/yue-tokens`、`@idadawn/yue-core` 等），`publishConfig.registry` 指 `https://npm.pkg.github.com`，Changesets 的固定版本组相应改为 `@idadawn/yue-*`。目录名与分层矩阵的键仍是 `packages/yue/<包>`；设计文档继续用 `@yue/<包>` 作简称（规格 §3.1 有对照说明）。消费方配置与发布步骤见 `packages/yue/README.md` §5。

### S1-YUE-005 的验证门收敛（2026-10-14）

Combobox 新增 Trigger/Value，推荐把搜索输入放在 Content 内；靶场、目录和 Storybook 使用此形态，取消了原先页面级 aria-hidden-focus 整条规则豁免。旧外置输入 API 保留兼容，其页面组合限制在 core 主文档中说明。

Select 的 Base UI 内部 Tab 哨兵在页面级 axe 中产生 aria-hidden-focus 报告；只按三个内部属性识别并排除这些瞬时哨兵，保留整条规则。真实 Tab/Shift+Tab 用例验证焦点不滞留在隐藏节点，负例确认普通隐藏按钮仍被拒绝。PinInput 为首位使用原生 label；ContextMenu 在封装层把 Shift+F10/菜单键转交给 Base UI 的 contextmenu 入口。框架源码保持原版。

### 2026-10-14 统一 Rspress（ADR-022）

产品负责人要求统一使用 Rspress、不再保留 Storybook。能力评估、已发现的 Props 过滤与离线编辑器限制、测试替代和任务边界见 [ADR-022](/pr-preview/pr-181/design/adr/adr-022-rspress-unified-component-docs.md)。S1-YUE-005 重新打开，原完成记录保留为迁移前证据；本次修订不代表替换代码已完成。
