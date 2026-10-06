---
title: 前端与 yue：React 19 + Vite + Tailwind v4 + Base UI + shadcn 源码自有化；六层分层；一套表单/表格/弹层引擎；Module Federation 2 外壳
status: Accepted
date: 2026-10-06
decision_id: D13
supersedes: none
---

# ADR-013 前端与 yue

## 1 背景与问题（Context）

参考平台前端是「两套并存」的反面教材：Vue 3 + 开源后台模板内核之上叠加另一套 UI 库封装与第三方表格，弹窗、表单、表格、Tree、Loading 都有两到三套实现；去重后组件与设计器全集约 251 条目，其中真正的设计器 8 个（基线 U §0 第 1 条）。控件三态（编辑 / 只读 / 单元格）各自硬编码，新增控件改三处；列表 PC / 移动两份完整配置（基线 F §6 第 7/9 条）。

iClaw 前端有好的工程规范与令牌思路（React 19 + TypeScript + Vite + Tailwind v4 + shadcn，console 版令牌架构，工程门禁），但没有组件库：`@iclaw/ui` 是 29 行占位，产品里真正在用的 shadcn 组件只有 `Toaster`，数据密集型控件全部缺失，Base UI 只用了 Dialog 一个原语（基线 U §0 第 2 条、§7.2）。shadcn 自 2026-07 起新项目默认 Base UI（基线 U §4）。前端插件化方面，Module Federation 2 的 `registerRemotes / loadRemote` 允许加载启动后才发现的远程，但 MF 零隔离，只能给可信插件（基线 P §9.1）。三条安全硬线：不用 `eval` 执行租户脚本；令牌不进 URL / iframe；前端权限裁剪只是体验层（基线 U §0 第 6 条）。

## 2 决策（Decision）

- **D13.1 技术栈**（路线 A，沿用 iClaw 已验证组合）：React 19 + TypeScript + Vite + Tailwind v4 + Base UI + shadcn 源码自有化；Zustand + TanStack Query；react-router 7；i18next；Vitest + Playwright + axe；Lighthouse 预算；Changesets 发包；Storybook 10；API client 从 Kun OpenAPI 生成（orval / openapi-typescript）+ 契约快照测试。
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
- **D13.12 分发渠道**（Q41）：私有 npm 为主；shadcn 私有 registry + MCP Server 面向 ISV 与 Agent，S3 可选、S4 必做。
- **D13.13 令牌**：三层结构 S1 必做；DTCG 格式导出 S1 可选；命名禁同名异义，状态色成对，WCAG AA 对比度单测进 CI；`data-theme / data-brand` 由服务端按租户下发，插件只引用语义令牌。
- **D13.14 联动与脚本三层**：声明式动作 → 可序列化表达式 DSL → QuickJS 沙箱脚本（白名单 API、超时、内存上限、不暴露令牌）；禁 `eval` / `new Function`。
- **D13.15 其他选型**：dnd-kit（包一层抽象）；`@xyflow/react` 12（S3 自由画布）；react-grid-layout；Univer 开源核心（排除 Pro 与商业包，xlsx 导出放后端）；pdfme；quickjs-emscripten；不用 Formily / amis / lowcode-engine / bpmn-js。
- **D13.16 安全硬线**：不用 `eval` 执行租户脚本；令牌不进 URL / iframe（一次性票据或 postMessage 握手）；前端权限裁剪只是体验层，鉴权在服务端；不在前端持有加密密钥；不持久化 token / 锁屏密码。
- **D13.17 为 S4 预留**：机器可读 manifest；带版本的 Schema 与校验器（Agent 生成物走同一校验再落库）；客户端动作 / 导航 / 命令注册表（名称、参数 JSON Schema、权限、人话描述）；SSE 事件 JSON Schema 为唯一真相源生成 TS。

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

- Base UI 验证门结果（S1 首迭代）。
- 富文本 / Markdown 编辑器选型（S2 前）。
- S3 移动端范围（审批 / 工作台 H5、小程序 / App 壳）。
- iClaw 前端代码：Q40 允许搬运的四处不含前端，yue 只借鉴令牌思路；shadcn 生成物可用 CLI 重新生成，不构成复用问题。

## 8 参考

- 相关 ADR：ADR-002、ADR-011、ADR-012、ADR-014。
- PRD：`PRD §07`、`PRD §08`、`PRD §11`、`PRD §12`。
- 基线：U §0、§2、§3.0–§3.8、§4、§5、§6、§7.2–§7.4；P §9.1、§9.2、§10.1 第 7 条；F §6 第 7/8/9 条。
