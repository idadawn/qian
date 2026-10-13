# 专题 spec · yue 组件库

> 状态：Draft v1（2026-10-06）。依据：ADR-013（前端与 yue）为主，兼引 ADR-002（插件模型与 MF2）、ADR-010（网关与令牌）、ADR-014（市场与 registry）、ADR-018（阶段）及补充裁决 Q12 / Q13 / Q37 / Q38 / Q39 / Q41。本 spec 规定 yue 的范围、分层、契约、流程与验收；安全规则以 §13 为准，验收方法以 §14 为准。
>
> yue 是 Qian 全部前端面（平台控制台、应用运行时、设计器、移动 H5、S4 Agent 界面）共用的唯一组件库与运行时契约包。它不是「又一个 UI 库」，而是把参考平台「两套并存、三处呈现、控件无 Schema」的教训反过来写成约束。

---

## 本版关键决策 D1–D16

| # | 决策 | 要点 |
|---|---|---|
| D1 | **路线 A**：React 19 + TypeScript + Vite + Tailwind v4 + Base UI 原语 + shadcn 源码自有化；数据密集组件在 TanStack 上自建 | 接受 S2 交付风险（Q12）；S1 首迭代设 Base UI 原语验证门（Menu / Select / Combobox / Tabs / Tooltip / Tree 的键盘与可访问性），不通过整体切 Radix，**不混用**；平台控制台域不临时引入 antd |
| D2 | **六层分层，依赖方向用工具强制** | tokens → core → shell / data / form / charts / code-editor → biz → runtime / widgets → designer；横切 sdk / agent / mobile 只被上层消费；eslint-plugin-boundaries + dependency-cruiser 进 CI |
| D3 | **一套表单 / 表格 / 弹层引擎** | SchemaForm、DataTable、弹层 API 各只有一套，贯穿 S1 后台 CRUD 与 S2 低代码运行时；业务组件只做叶子，不得新建引擎 |
| D4 | **Module Federation 2 外壳 + 版本化插槽注册表** | `@yue/shell` 以 MF2 运行时加载官方插件 UI；共享依赖 singleton；插槽 / 契约 ID 带 `/vN`；不可信 UI 走 Worker 远程渲染或 iframe（S3） |
| D5 | **令牌三层 + 明暗两态** | primitive → semantic → component；状态色成对 + `Tone` 类型；WCAG AA 对比度单测进 CI；运行时主题注入 `data-theme / data-brand`；DTCG 导出 S1 可选、S4 必做 |
| D6 | **机器可读组件目录** | 每个组件 / 控件导出 manifest（props JSON Schema、slots、events、a11y 角色、令牌依赖、示例）；`registerWidget` 一份注册表同时作设计器左栏与 `GET /registry/widgets`；设计器、远程渲染白名单、S4 Agent 生成 UI 共用一份契约 |
| D7 | **安全硬线** | 不 `eval` 执行租户脚本；令牌不进 URL / iframe；前端权限裁剪只是体验层；不在前端持有加密密钥；不持久化明文令牌（详见 §13.10） |
| D8 | **一份 Schema + `device` 维度** | FormSchema / ListSchema 不维护两份端配置；端差异用 overrides 表达；控件 manifest 声明支持端并显式降级 |
| D9 | **统一一个壳，按「平台控制台 / 应用」上下文切换**（Q37） | 不采用三面新窗口；与插件外壳模型一致 |
| D10 | **S1 单一侧栏布局**（Q38） | 7 种布局模式与偏好抽屉 S3 可选 |
| D11 | **S2 只做 PC 响应式**（Q13） | `@yue/mobile` H5 渲染器 S2 可选 / S3；小程序原生渲染不纳入 S1–S3 |
| D12 | **审批设计器为纵向节点树（DOM 自绘）；不用 bpmn-js** | React Flow 自由画布 S3 可选；BPMN 只作导出器 |
| D13 | **联动三层** | 声明式动作 → 可序列化表达式 DSL（JSONLogic 类）→ QuickJS 沙箱脚本（S2 可选兜底，白名单 API、超时、内存上限、不暴露令牌）；自由 JS 按钮不做 |
| D14 | **分发：私有 npm 为主**（Q41） | GitHub Packages，scope `@idadawn`，包名 `@idadawn/yue-<包>`（2026-10-09 拍板，§3.1）；shadcn 私有 registry + MCP Server S3 可选、S4 必做 |
| D15 | **文档站 Storybook 10；产品文档不混入 React 18 的 Docusaurus** | Storybook 用例复用为测试 |
| D16 | **控件包与后端 Field Type Provider 同名同版本发布** | 控件 `type` 即字段类型编码；前后端同一 SemVer，CI 校验配对 |

---

## 0. 定位与阅读方式

yue 同时承担三种角色，三者缺一不可：

1. **组件库**：平台控制台、应用运行时、设计器、移动 H5 共用的 React 组件，S1 先交付支撑技术组件界面的最小集合。
2. **运行时契约包**：FormSchema / ListSchema / 控件 manifest / SSE 事件等 JSON Schema 与由其生成的 TypeScript 类型。契约先于组件存在，后端、设计器、运行时、Agent 都对着同一份契约工作。
3. **工程基线**：分层边界、a11y、i18n、视觉回归、发包流程等门禁，由 §14 统一验收。

本 spec 的写法：D1–D16 是本版已拍板的决策，阅读时先确认是否认同这些前提；§1–§2 划范围，§3 是契约（最需要评审的部分），§4–§5 说明怎么做与谁消费，§6 是选型，§7–§8 对接 §13 与 §14，§9 指向后续阶段。所有「参考平台」的引用都只作为反例或功能对照，不代表实现路径。

与参考平台前端的核心差异可以压缩成一句话：参考平台用「两套并存」换取快速堆功能（弹窗、表单、表格、树各有两到三套实现，控件在编辑、只读、单元格三处各自维护，元数据没有 Schema），yue 用「一套引擎 + 一份契约 + 工具强制的分层」换取多团队长期共用的可维护性。这决定了 yue 在 S1 看起来比参考平台「少」，但每一项都带着 S2–S4 要复用的契约。

## 1. 范围

范围划分遵守三条原则。第一，**阶段克制**：每阶段只做让该阶段退出标准跑通所必需的组件，S1 不做任何设计器、不做低代码控件、不做图表。第二，**契约先行**：凡是 S2 以后才有 UI 的能力（控件注册表、FormSchema、SSE 事件、客户端动作），其 Schema 与注册 API 在 S1 就定义并进 CI，避免 S2 把 S1 的后台表单重写一遍。第三，**横切先行**：令牌、SDK、注册表、分层 lint 在第一个组件之前就位，因为它们改起来的代价随组件数线性增长。

### 1.1 S1 交付（支撑「开发人员写出多租户应用」的界面）

| 层 | 交付 |
|---|---|
| `@yue/tokens` | 三层令牌、明暗两态、shadcn 语义变量映射、对比度单测、运行时主题注入点；DTCG 导出可选 |
| `@yue/core` | 约 30 个基础组件：Button 族、Input 族（Input / Textarea / InputNumber / InputPassword / PinInput）、Checkbox / Radio / Switch / Toggle / Segmented、Select / Combobox、Label / Field、Dialog / AlertDialog / Sheet / Drawer / Popover / Tooltip / HoverCard / DropdownMenu / ContextMenu、Tabs / Accordion / Card / Separator / ScrollArea / Resizable、Badge / Tag / Avatar / Skeleton / Spinner / Progress / Alert / Toast / Notification / Empty / Result、Breadcrumb / Pagination / BackTop / FullScreen、Icon、Descriptions（提前）；每个组件暴露 `data-slot` 与稳定 `data-testid` |
| `@yue/shell` | AppShell 单一侧栏布局；后端菜单 → 路由 + `menuTypeRegistry`（页面 / 外链两类）；Page / PageHeader / ColPage / LeftTree；Preferences 对象与 `initPreferences({ namespace, overrides })`；租户切换器 + 平台代管横幅；主题 / 语言切换、用户下拉；兜底页三件套（403 / 404 / 500 + ErrorBoundary / OfflineBanner / UpdateBanner）；运行时配置注入；MF2 远程加载与插槽注册表 |
| `@yue/data` | DataTable 基础版（TanStack Table v9：列 schema、服务端分页 / 排序 / 筛选、行选择、列显隐、固定列、空 / 加载 / 错误态、工具栏）；Tree / TreeSelect / Cascader；DatePicker / DateRange / TimePicker / TimeRange；Upload（分片 + MD5 秒传 + 进度 + 断点 + 预览）；ImagePreview |
| `@yue/form` | Field 抽象 + react-hook-form 内核 + Ajv 校验；SchemaForm 基础版 + `FormApi`；`dependencies` 基础联动；编辑 / 只读态引擎级能力；校验与脱敏规则目录；弹层 API `useModal / useDrawer / usePopup`；Message / Confirm / Notification 统一出口 |
| `@yue/biz` | ApiSelect / `useRemoteOptions`、DictSelect、OrganizeSelect、UserSelect、RoleSelect、菜单 / 按钮授权树、IconPicker |
| 认证页族 | 账号密码（含验证码策略）、SSO 跳转与回调、多租户二次选择、登录过期弹窗；可选：验证码登录、第三方登录按钮区、忘记密码、Captcha |
| `@yue/sdk` | 注册表 API（导航 / 设置 / 命令 / 快捷键 / 预览器 / 客户端动作 / 控件）、`PlatformClient`、request 拦截器链（鉴权刷新队列、租户头、大数处理）、i18n、mock 中间件、`.d.ts` 契约 |
| 契约包 | 控件 manifest 格式、FormSchema / ListSchema 草案（含 `device`、`auth`）、SSE 事件 JSON Schema、权限码目录格式、Filter AST TS 类型（从后端 Schema 生成） |
| 工程化 | §4 的 21 项基线全部就位 |

不在 S1：多标签完整版（基础版可选）、Command Palette、7 种布局、偏好抽屉、Cron、富文本、Markdown、CodeEditor、子表、任何设计器、门户物料、图表。

### 1.2 S2 交付（低代码构建）

- `@yue/core` 补 Slider / Rate / ColorPicker / Steps / Timeline / Statistic / EllipsisText / JsonViewer；可选 Command Palette、Watermark、Carousel / QrCode / Barcode、Transfer。
- `@yue/data` 进阶：树表、列权限 `auth`、行内编辑、复杂表头、列宽拖拽与列序、虚拟滚动；ImportWizard / ExportDialog、SuperQuery、ConditionBuilder（被列表 / 权限 / 流程复用）；可选个人视图、合计行。
- `@yue/code-editor`：CodeEditor（Monaco 桌面懒加载 / CodeMirror 6 轻量）、Cron；富文本 / Markdown 待选型（Q39）。
- `@yue/biz` 补 PosSelect / GroupSelect / UsersSelect、PopupSelect 族、RelationForm（Attr / Query 可选）、InterfaceSelect、授权树的列 / 字段 / 数据页签。
- `@yue/widgets` S2 控件集：基础输入、选择、日期、上传、组织类、子表、下拉树与弹窗选择、关联表单、公式（数字 / 日期）、系统控件、六种布局容器；每控件 meta + Runtime（edit / readonly / cell）+ Designer 面板 + 端支持矩阵。
- `@yue/runtime`：表单渲染器（三态由注册表提供）、子表、联动三层、列表渲染器（普通 / 左树右表 / 树形必做；分组 / 卡片 / 行内编辑可选）、动作执行器插件点、流程表单宿主（`formOperates` 字段权限、按钮矩阵由引擎返回）、审批中心基础（待办 / 已办 / 发起 + 时间线）、设计物生命周期 UI（发布 / 预览 / 生成菜单）。
- `@yue/designer`（基础设施：dnd-kit 封装、画布容器、schema 驱动属性面板、大纲树、撤销重做、校验定位、预览）+ `@yue/designer-form` + `@yue/designer-list` + `@yue/designer-flow`（纵向节点树）。
- `@yue/charts`：ECharts 6 封装（S2 可选，S3 门户 / 报表必做）。
- 组件 manifest 全量导出；视觉回归进 CI；应用切换（同壳按上下文）。
- `@yue/mobile` H5 表单渲染器：S2 可选，作为后段验证项。

### 1.3 S3 交付（面向客户）

- 通知抽屉；门户设计器与运行时（Puck 模型或自研栅格）；不可信 UI 沙箱（Worker 远程渲染白名单 = yue manifest；iframe sandbox）。
- 可选：多布局与偏好抽屉、主题预设、扫码登录 / 注册、申请 / 加入企业、自动化流设计器（React Flow）、打印（pdfme）/ 报表（Univer）/ 大屏设计器与运行时、透视表、外链运行时、工作台、移动 H5 审批与工作台、shadcn 私有 registry。

### 1.4 S4 预留（本期只定契约）

`@yue/agent`（ToolCallCard、ThinkingTimeline、AgentTrace、Composer、来源 / 记忆引用芯片；只消费 S1 定义的 SSE 事件契约）；shadcn MCP Server；MCP Apps 视图；AI 助手悬浮窗。

---

## 2. 非目标

1. 不混用两套设计体系：不在任何前端面并存 yue 与 antd / MUI / Element 之类的第二套组件库；Base UI 验证门不过则整体切 Radix。
2. 不做小程序原生渲染（S1–S3）、不做原生 App（本期）；移动端是同一 React 渲染器的端适配。
3. 不 `eval` / `new Function` 执行租户脚本；不做「自定义按钮任意 JS」。
4. 不维护两份端配置（PC / 移动各一份元数据）。
5. 不复刻参考平台全量：7 种布局模式、锁屏、内置 IM、三面新窗口切换、两套图标体系、双 Modal 实现等不做。
6. 不把 yue 做成「框架无关」口号：yue 只服务 React 19；分层靠工具强制，不靠约定。
7. 不在 yue 内实现业务规则：权限判定、数据过滤、脱敏以服务端为准，yue 只按元数据渲染。
8. 不自建 Agent 对话引擎：`@yue/agent` 只是事件流的展示组件。
9. 不用 bpmn-js、Formily、amis、lowcode-engine、hiprint、vxe-table 等自带体系或许可证受限的库。
10. 不把 Docusaurus（React 18）混入 yue 工作区。

---

## 3. 数据模型

### 3.1 包结构与依赖方向

```
应用面（平台控制台 / 应用运行时 / 设计器 / 移动 H5 / Agent 界面）
  ▲
  │ @yue/designer, @yue/designer-form|list|flow|portal     设计器层（S2 / S3）
  │ @yue/runtime  @yue/widgets                              低代码运行时层（S2）
  │ @yue/biz                                                业务选择器层（S1 起；经 PlatformClient 取数）
  │ @yue/shell  @yue/data  @yue/form  @yue/charts  @yue/code-editor   后台通用层（S1 / S2）
  │ @yue/core                                               基础组件层（S1）
  │ @yue/tokens                                             设计令牌层（S1）
  │
  横切（只被上层消费）：@yue/sdk（注册表 API、PlatformClient、request、i18n、mock、运行时配置）
                      @yue/contracts（无 UI 的 Schema 与 TS 类型）
                      @yue/agent（S4）  @yue/mobile（S2 可选 / S3）
```

| 包 | 职责 | 允许依赖 | 阶段 |
|---|---|---|---|
| `@yue/tokens` | CSS 变量、TS `Tone` 类型、主题注入、DTCG 导出 | 无 | S1 |
| `@yue/contracts` | FormSchema / ListSchema / PortalLayout / FlowNodes 的 JSON Schema 与生成的 TS 类型；控件 manifest Schema；SSE 事件 Schema；权限码目录格式；Filter AST 类型 | 无 | S1 草案 / S2 定稿 |
| `@yue/core` | Base UI + shadcn 自有化的基础组件 | tokens | S1 |
| `@yue/sdk` | 注册表（导航 / 设置 / 命令 / 快捷键 / 预览器 / 客户端动作 / 控件 / 菜单类型）、`PlatformClient`、request 拦截器链、i18n、mock、`window.__APP_CONF__` 注入 | tokens, contracts | S1 |
| `@yue/shell` | AppShell、菜单 → 路由、MF2 宿主与插槽、租户切换器、认证页族、兜底页 | core, sdk, form（登录表单） | S1 |
| `@yue/data` | DataTable、Tree 族、日期族、Upload、ImportWizard、ConditionBuilder | core, sdk | S1 / S2 |
| `@yue/form` | Field、SchemaForm、FormApi、联动、校验与脱敏规则目录、弹层 API、消息出口 | core, sdk, contracts | S1 |
| `@yue/charts` | ECharts 封装与令牌桥接 | core, tokens | S2 可选 |
| `@yue/code-editor` | Monaco / CodeMirror 统一接口、Cron | core | S2 |
| `@yue/biz` | 组织 / 用户 / 角色 / 岗位 / 字典 / 远程选项 / 弹窗选择 / 关联表单 / 授权树 / IconPicker | core, data, form, sdk | S1 / S2 |
| `@yue/widgets` | 低代码控件：meta + Runtime(edit / readonly / cell) + Designer 面板 | core, data, form, biz, sdk, contracts | S2 |
| `@yue/runtime` | 表单 / 列表 / 详情渲染器、动作执行器、流程表单宿主、审批中心基础 | widgets 及以下 | S2 |
| `@yue/designer*` | 设计器基础设施与三大设计器 | runtime 及以下 | S2 / S3 |
| `@yue/mobile` | H5 渲染器端适配 | runtime 及以下 | S2 可选 / S3 |
| `@yue/agent` | Agent 交互组件 | core, sdk, contracts | S4 |

规则：下层不得 import 上层；同层之间 shell / data / form / charts / code-editor 互不依赖（shell 对 form 的登录表单依赖是唯一例外，经 ADR 记录）；横切包不得依赖除 tokens / contracts 以外的任何包。

**归层判定题**（新组件进哪一层，按顺序回答）：
1. 它是否需要知道「平台」的存在（组织、用户、字典、数据接口）？是 → 至少是 `@yue/biz`；否 → 继续。
2. 它是否由元数据（FormSchema / ListSchema）驱动渲染？是 → `@yue/widgets` 或 `@yue/runtime`；否 → 继续。
3. 它是否承载页面骨架、导航、上下文（租户 / 应用 / 身份）？是 → `@yue/shell`；否 → 继续。
4. 它是否是数据密集（表格、树、日期、上传）或表单体系（Field / SchemaForm / 弹层）？是 → `@yue/data` 或 `@yue/form`；否 → `@yue/core`。
5. 任何回答里出现「需要新建一套弹层 / 表单 / 表格引擎」→ 否决，改为复用现有引擎并只做叶子（D3）。

分层违规在 CI 直接失败，不接受「临时引用、下个迭代修」；需要跨层的能力一律通过 `@yue/sdk` 的注册表或依赖注入（`PlatformClient`）解耦。

**npm 包名**（2026-10-09 产品负责人拍板）：文中 `@yue/<包>` 是简称；实际发布的包名是 `@idadawn/yue-<包>`（如 `@idadawn/yue-core`），发到 GitHub Packages——它要求 scope 与仓库所有者同名（ADR-021 D21.2 的「一个 GitHub Packages 命名空间」）。目录仍是 `packages/yue/<包>`，分层矩阵按目录名登记。

### 3.2 控件注册表契约 `registerWidget`

```ts
// @yue/sdk
export type Device = 'pc' | 'h5' | 'miniapp';
export type Presentation = 'edit' | 'readonly' | 'cell';

export interface WidgetMeta {
  type: string;                 // 稳定编码，发布后不可改；与后端 Field Type Provider 同名
  version: string;              // SemVer，与后端 Provider 同版本（D16）
  name: I18nKey;
  category: 'basic' | 'advanced' | 'system' | 'layout' | 'biz' | 'ocr';
  props: JSONSchema2020;        // 设计器属性面板由此生成；Agent 生成 UI 由此校验
  valueType: 'string' | 'number' | 'boolean' | 'date' | 'datetime' | 'time'
           | 'array' | 'object' | 'file' | 'ref' | 'none';
  events: string[];             // 可绑定的事件名
  devices: Device[];            // 支持端；不支持的端显式降级为只读或提示
  capabilities: {
    bindsField: boolean;        // 是否绑定数据字段（布局容器为 false）
    canBeColumn: boolean;
    canBeSearch: boolean;
    canBeGroup: boolean;
    canBeInSubtable: boolean;
    supportsReadonly: boolean;
    participatesInLinkage: boolean;
  };
  defaultOperators: FilterOperator[];   // 与后端「运算符 × 字段类型」矩阵一致
  authAware: boolean;           // 是否参与字段权限（可见 / 可编辑 / 脱敏）
  sensitivityHint?: 'public' | 'internal' | 'pii' | 'finance' | 'secret';
  i18nKeys: string[];
  a11y: { role: string; labelledBy: 'label' | 'aria-label' };
  tokens: string[];             // 依赖的令牌名，供主题校验
}

export interface WidgetDefinition<P = unknown, V = unknown> {
  meta: WidgetMeta;
  Runtime: React.ComponentType<{ presentation: Presentation; props: P; value: V;
                                 onChange?: (v: V) => void; formApi: FormApi; auth: FieldAuth }>;
  Designer?: React.ComponentType<DesignerPanelProps<P>>;   // 缺省由 meta.props 生成
  toSearchControl?: (props: P) => SearchControlSchema;      // 查询表单派生
  toCellRenderer?: (props: P) => CellRenderer<V>;           // 列表单元格派生
}

export function registerWidget(def: WidgetDefinition): void;
export function listWidgets(filter?: { device?: Device; category?: string }): WidgetMeta[];
```

- 同一份注册表同时是设计器左栏数据源、运行时分派表与 `GET /registry/widgets` 的返回体（服务端只做权限过滤与缓存）。
- `Runtime` 必须覆盖三种 `presentation`；不允许为只读态或单元格另写组件（参考平台 806 行详情渲染器是反例）。
- `type` 命名：官方 `qian.<name>`，插件 `<插件 id>.<name>`；注册冲突即启动失败。

注册表有三类消费者，对一致性的要求各不相同：**设计器**读 `meta` 生成左栏与属性面板，要求 `props` Schema 完整且带默认值与 `title / description`（多语言 key）；**运行时**按 `type` 分派 `Runtime` 并传入 `presentation`，要求三态行为一致（同一 `value` 在 edit / readonly / cell 下显示语义相同，只是交互不同）；**服务端与 Agent** 经 `GET /registry/widgets` 读 `meta`，要求 `type@version` 与后端 Field Type Catalog 配对、`defaultOperators` 与运算符矩阵一致、`devices` 与端支持矩阵一致。任何一处不一致都由 CI 的契约测试拦截，而不是等到设计器里拖出控件才发现。

控件在不同端的行为必须**显式**：`devices` 不含当前端时，运行时渲染为只读文本或「此端不支持」提示，绝不静默丢弃字段（参考平台在某小程序端静默缺失 7 类控件是反例）。

### 3.3 FormSchema 四段契约草案

```jsonc
{
  "$schema": "https://schema.qian.dev/form/v1",
  "version": "1.2.0",                      // 元数据版本（草稿 / 发布快照各自不可变）
  "id": "qian.demo.order.form",
  "data": {                                // 数据契约：JSON Schema 2020-12，additionalProperties:false
    "type": "object",
    "properties": {
      "orderNo":  { "type": "string", "x-sensitivity": "internal" },
      "amount":   { "type": "number", "minimum": 0 },
      "customer": { "type": "string", "x-ref": "qian.crm.customer" },
      "items":    { "type": "array", "items": { "$ref": "#/$defs/item" } }
    },
    "required": ["orderNo", "customer"],
    "$defs": { "item": { "type": "object", "properties": { "sku": { "type": "string" }, "qty": { "type": "integer" } } } }
  },
  "ui": {                                  // 布局与控件：自有 UI Schema
    "layout": [
      { "widget": "qian.row", "props": { "cols": 2 }, "children": [
        { "field": "orderNo", "widget": "qian.input", "props": { "readonlyMask": "none" } },
        { "field": "amount",  "widget": "qian.number", "props": { "precision": 2 } }
      ]},
      { "field": "customer", "widget": "qian.popupSelect", "props": { "source": { "kind": "entity", "ref": "qian.crm.customer" } } },
      { "field": "items", "widget": "qian.subtable", "props": { "columns": ["sku", "qty"], "summary": ["qty"] } }
    ],
    "device": {                            // 端覆盖，而非第二份元数据（D8）
      "h5": { "overrides": [ { "path": "layout[0].props.cols", "value": 1 } ] }
    }
  },
  "rules": {                               // 联动与校验：三层中的前两层
    "actions": [
      { "when": { "==": [ { "var": "amount" }, 0 ] }, "do": { "setRequired": { "customer": false } } }
    ],
    "expressions": {
      "total": { "*": [ { "var": "amount" }, 1.13 ] }   // 可序列化表达式 DSL（JSONLogic 类）
    },
    "serverHooks": ["before-save", "after-save"]        // 服务端 Hook 名，由后端注册
  },
  "binding": {                             // 数据绑定：实体 / 字段 / 系统列约定
    "entity": "qian.demo.order",
    "fields": { "orderNo": "order_no", "amount": "amount", "customer": "customer_id" },
    "systemColumns": ["tenant_id", "created_by", "created_at", "row_version", "deleted_at"]
  },
  "auth": {                                // 体验层裁剪提示；服务端为准（§13.10）
    "fields": { "amount": ["read", "write"], "customer": ["read"] }
  }
}
```

**四段为什么分开。** `data` 是唯一允许被后端、OpenAPI、MCP 直接复用的部分，所以它必须是标准 JSON Schema 2020-12 且 `additionalProperties: false`；`ui` 承载布局与控件选择，是 yue 自有的 UI Schema，允许随版本演进；`rules` 只放可序列化的声明式动作与表达式 DSL，保证 Agent 能生成、服务端能校验、小程序端能执行；`binding` 把表单与实体字段、系统列约定绑定，是 Schema Migrator 与数据引擎读取的部分。四段分开的直接收益是：改布局不触发数据契约版本变化，改规则不触发迁移，Agent 生成表单时可以只生成 `data + ui` 而把 `binding` 交给平台填充。

**版本与兼容。** `version` 遵循 SemVer：`data` 的破坏性变化（删字段、改类型、收紧必填）升大版本并要求迁移计划；`ui / rules` 的变化升小版本；草稿与发布快照各自不可变，运行时只读当前生效快照。校验器（Ajv，前后端同一份编译产物）必须拒绝未知 `$schema` 与超出版本范围的文档，而不是宽松通过。

**`x-` 扩展。** `x-sensitivity`（字段敏感度，供脱敏与导出审计）、`x-ref`（引用实体）、`x-widget-hint`（推荐控件）是本 spec 定义的扩展关键字，其余 `x-` 关键字一律由校验器拒绝，防止契约被私有字段侵蚀。

### 3.4 ListSchema 契约草案（base + overrides）

```jsonc
{
  "$schema": "https://schema.qian.dev/list/v1",
  "version": "1.0.0",
  "id": "qian.demo.order.list",
  "base": {
    "dataSource": { "kind": "entity", "ref": "qian.demo.order" },
    "layout": "table",                               // table | left-tree | tree-table | group | card | inline-edit
    "query": [ { "field": "orderNo", "widget": "qian.input" }, { "field": "createdAt", "widget": "qian.dateRange" } ],
    "columns": [
      { "field": "orderNo", "width": 160, "fixed": "left" },
      { "field": "amount", "align": "right", "summary": "sum" },
      { "field": "customer", "resolver": "qian.crm.customer" }   // 值翻译按需批量
    ],
    "actions": {
      "toolbar": [ { "kind": "builtin", "id": "add" }, { "kind": "builtin", "id": "import" } ],
      "row":     [ { "kind": "builtin", "id": "edit" }, { "kind": "startFlow", "flow": "qian.demo.order.approval" } ]
    },
    "filter": { "logic": "and", "groups": [] },      // Filter AST，与后端共用
    "sort": [ { "field": "createdAt", "dir": "desc" } ]
  },
  "overrides": {
    "h5": { "layout": "card", "columns": ["orderNo", "amount"] }
  },
  "auth": { "buttons": true, "columns": true, "fields": true, "dataScope": true }   // 四开关产出可授权项
}
```

**合并规则。** 运行时按当前 `device` 取 `overrides[device]` 与 `base` 做浅合并：`layout` 整体覆盖；`columns` 若为字符串数组则表示「从 base 中按字段名挑选并排序」，若为对象数组则逐列合并；`query / actions` 按 `id / field` 对位合并；`filter / sort / dataSource / auth` 不允许被端覆盖（它们影响服务端语义）。合并结果再经同一份 ListSchema 校验器校验，保证任何端上看到的都是合法 Schema。

**与权限的关系。** `auth` 四开关只决定「发布时产出哪些可授权项」（按钮、列、字段、数据范围），不参与运行时判定；运行时的列裁剪、按钮显隐来自服务端返回的授权结果，列表组件据此渲染。两者分开是为了避免参考平台「设计器不勾选就不受管控」的双重门漏洞演变成「不勾选就不校验」。

### 3.5 令牌命名

| 层 | 形式 | 示例 | 规则 |
|---|---|---|---|
| primitive | `--yue-p-<family>-<step>` | `--yue-p-blue-500`、`--yue-p-space-4`、`--yue-p-radius-md`、`--yue-p-duration-fast` | 色阶 50–950；字号 / 行高阶梯；间距刻度；圆角；阴影；动效时长与曲线；z-index；断点 |
| semantic | `--yue-<role>[-foreground]` | `--yue-primary` / `--yue-primary-foreground`、`--yue-destructive`、`--yue-sidebar`、`--yue-ring` | 全部映射到 shadcn 语义变量（`--primary` 等）供 Base UI 组件消费；状态色成对 `--yue-ok` / `--yue-ok-bg`，TS `Tone = 'ok' \| 'warn' \| 'error' \| 'info' \| 'neutral'` |
| component | `--yue-<component>-<prop>` | `--yue-button-radius`、`--yue-table-row-height` | 按组件按需，只能引用 semantic 或 primitive |

规则：禁止同名异义（`--accent` 冲突为反例）；颜色以可加 alpha 的形式存放（oklch 分量，如 `.43 .144 260`），消费方写 `oklch(var(--yue-<role>))` 或 `oklch(var(--yue-<role>) / <alpha>)`，不要把语义令牌直接当颜色值使用；由一个主色派生完整色阶；明暗两态在 `html.dark, html[data-theme="dark"]` 下重定义 semantic 层并设置 `color-scheme`（两种切换方式都认，文档站用前者，S1-DEC-019 R72）；`data-brand` 由服务端按租户下发；禁止媒体查询换品牌色。

### 3.6 组件 manifest 格式

```jsonc
{
  "name": "Button", "package": "@yue/core", "version": "1.4.0",
  "props": { /* JSON Schema 2020-12 */ },
  "slots": ["icon", "default"],
  "events": ["onClick"],
  "a11y": { "role": "button", "keyboard": ["Enter", "Space"] },
  "tokens": ["--yue-primary", "--yue-button-radius"],
  "examples": [{ "title": "主按钮", "props": { "variant": "primary" } }],
  "status": "stable" | "beta" | "deprecated"
}
```

注册表导出 `yue-manifest.json`，是远程渲染白名单与 S4 Agent 生成 UI 的唯一词表。

### 3.7 SSE 事件契约（S1 定义，S4 消费）

以 JSON Schema 为唯一真相源生成 TS，事件枚举含 `text / recommend / toolCall / toolResult / artifact / clientAction / progress / end / error`；`clientAction` 的参数必须对应 `@yue/sdk` 客户端动作注册表中的一项（名称、参数 Schema、权限、人话描述）；事件不携带令牌或密钥。

---

## 4. 流程

### 4.1 设计令牌 → 组件 → 文档站 → 视觉回归 → 发包

1. **令牌**：在 `@yue/tokens` 修改 → 对比度单测与冲突单测 → 生成 CSS 变量与 TS 类型 →（可选）DTCG 导出。
2. **组件**：shadcn CLI 生成源码进 `@yue/core` 后品牌化；每个组件必须带 Storybook 用例（默认 / 悬停 / 禁用 / 错误 / 暗色）、manifest、`data-slot / data-testid`、i18n key。
3. **文档站**：Storybook 10（CSF Factories、Vitest 集成、a11y addon、自动 props 表、令牌可视化页）；用例复用为交互测试。
4. **视觉回归**：Playwright 截图比对，CI 固定字体与容器镜像，基线入库；S1 可选、S2 阻断。
5. **发包**：Changesets；SemVer（0.x 每个 minor 可破坏；1.0 后只在大版本破坏，废弃至少保留一个 minor 并附 codemod）；tsdown / Rslib 输出 ESM + d.ts，CSS 单独导出；重型物料（Monaco、富文本、ECharts、地图）独立包懒加载；publint；私有 npm。

### 4.2 控件包与后端 Field Type Provider 同名同版本发布（D16）

- 一个低代码控件 = 前端 `@yue/widgets` 中的 `WidgetDefinition` + 后端 `qian.dynamic-data` 的 Field Type Provider（类型映射、校验、值翻译、运算符集）。
- 两者共享 `type` 与 `version`；清单 `contributes.to: qian.form.field-type/v1` 同时声明两侧产物。
- CI 校验配对：前端 manifest 的 `type@version` 必须在后端 Field Type Catalog 中存在且 `defaultOperators` 与矩阵一致，否则发布阻断。
- 升级：Provider 先发布（additive），控件后发布；破坏性变更开新 `type` 或新大版本并给迁移脚本。

### 4.3 主版本并存

yue 主版本可并存一个过渡窗口（MF2 共享依赖按主版本分组）；外壳只允许一个 React 版本；插件声明 `engines.yue` 范围，不满足即不加载并给出结构化错误。

### 4.4 Base UI 验证门（S1 首迭代）

验证项：Menu / Select / Combobox / Tabs / Tooltip / Tree 的键盘导航、焦点管理、ARIA、RTL、受控 / 非受控、SSR 兼容；通过标准：axe critical = 0、键盘路径用例全过、无需 patch 包。不通过 → 整体切 Radix 并重新生成 shadcn 源码；不混用。结论记入 ADR-013 的 Status History。

**结论（2026-10-09，S1-YUE-003）**：通过，锁定 `@base-ui/react` 1.8；组件级 21 个浏览器用例 + 页面级 7 个 axe 用例全过，critical + serious = 0，无需 patch 包。五项发现（Tooltip 的 ARIA 关联、Tabs 默认手动激活、Combobox 打开态 aria-hidden、无 Tree 原语、date-fns 可选 peer）与封装层处理见 [ADR-013 §9](../../adr/ADR-013-frontend-yue.md#9-状态历史status-history)。

---

### 4.5 组件状态与弃用流程

| 状态 | 进入条件 | 承诺 |
|---|---|---|
| `experimental` | 有 Storybook 用例与 manifest；未过 a11y 门 | 任意版本可破坏；不进 `yue-manifest.json` 稳定词表 |
| `beta` | a11y critical = 0；视觉基线建立；至少一个真实消费方 | minor 可破坏但附迁移说明 |
| `stable` | 两个 minor 无破坏性变更；文档完整；契约测试覆盖 | 只在大版本破坏；进入远程渲染白名单与 Agent 词表 |
| `deprecated` | 有替代组件；codemod 可用 | 至少保留一个 minor；控制台与 lint 给出警告与替代名 |

弃用必须同时做三件事：manifest 标 `deprecated` 并指向替代、lint 规则报告使用处、Changesets 条目说明迁移路径。没有 codemod 的弃用只允许在大版本。

### 4.6 插件作者贡献控件的流程

1. 用 `qian` CLI 从模板生成控件骨架（`meta` + 三态 `Runtime` + 可选 `Designer` + 测试 + Storybook）。
2. 在本地以「开发者租户」加载：外壳按 `engines.yue` 校验版本后以 MF2 挂载。
3. 后端同名 Field Type Provider 先合入并发布 Catalog；CI 校验 `type@version` 配对、运算符矩阵一致、端矩阵一致。
4. 通过 TCK 的前端子集（manifest 校验、a11y、三态一致性、不读取令牌、不访问白名单外的 API）。
5. `partner` 以上进入人工审核；`private / community` 控件只能在 Worker 远程渲染或 iframe 中运行（S3），不进共享包。

## 5. 职责映射

### 5.1 插件 / 服务 ↔ yue 包

| Qian 插件 / 服务 | 消费的 yue 部分 | 向 yue 注册的内容 |
|---|---|---|
| `qian.shell`（前端外壳静态站） | `@yue/shell`、`@yue/tokens`、`@yue/sdk` | 宿主：MF2 远程清单、插槽注册表、菜单类型注册表、运行时配置注入 |
| `qian.identity` + `connector.*` | 认证页族、`@yue/form` | 登录方式插件（`AuthProvider` 契约）、第三方登录按钮、多租户选择 |
| `qian.tenancy` | `@yue/shell` 租户切换器、`@yue/data` DataTable、`@yue/form` | 租户运营后台 CRUD 页、代管横幅 |
| `qian.authz` | `@yue/biz` 授权树、`@yue/data` ConditionBuilder（S2） | 权限码目录、`Explain` UI 数据结构 |
| 组织与身份（Directory） | `@yue/biz` OrganizeSelect / UserSelect / RoleSelect / PosSelect | 组织树数据源、通讯录页 |
| `qian.files` | `@yue/data` Upload / ImagePreview | 存储路径策略、预览票据契约 |
| `qian.config` | `@yue/form` SchemaForm（配置 Schema 自动渲染） | 配置 Schema → 设置页 |
| 消息（通知中心） | `@yue/shell` 通知抽屉（S3）、消息类型 → 落地深链 | 消息类型注册 |
| 低代码表单（S2） | `@yue/widgets`、`@yue/runtime`、`@yue/designer-form` | 控件集、Field Type Provider 配对、服务端 Hook 名 |
| 列表与页面（S2） | `@yue/runtime` 列表渲染器、`@yue/designer-list`、`@yue/data` 进阶 | 列表形态策略、动作执行器、菜单类型 |
| 审批内核（S2） | `@yue/runtime` 流程表单宿主与审批中心、`@yue/designer-flow` | 节点类型（schema + 属性面板 + 执行器）、`actions` 契约 |
| 数据访问 / 数据接口（S2） | `@yue/code-editor`、数据集配置 UI | Filter AST 类型、运算符矩阵 |
| 门户 / 报表 / 打印 / 大屏（S3） | `@yue/charts`、`@yue/designer-portal`、Univer / pdfme 封装 | 门户物料注册表（单一注册表 + 端维度） |
| 应用市场（S3） | `@yue/shell` 应用中心、插件卡片视图模型 | 市场详情页权限清单展示 |
| Agent 运行时（S4） | `@yue/agent`、客户端动作注册表 | SSE 事件流、客户端动作 |

### 5.2 与 qian.shell 的关系

`qian.shell` 是部署单元（静态站 + MF2 宿主），`@yue/shell` 是它使用的库。外壳只做：加载远程、按清单 `contributes` 注册插槽、按后端菜单生成路由、维护当前上下文（租户 / 应用 / 身份）并注入请求头、处理「未启用插件」的前端拦截。外壳不包含任何业务页面。

### 5.3 与设计器的关系

设计器只消费 `@yue/contracts` 与控件注册表：左栏 = `listWidgets()`，属性面板 = `meta.props` 自动生成（手写面板只在声明化不足时补充），校验 = 与服务端共用的 Ajv 校验器（Agent 生成物走同一校验再落库），产出 = 带 `$schema` 与版本的 FormSchema / ListSchema / FlowNodes。

### 5.4 与 Agent 的关系

S1 起预留三件事：机器可读 manifest、带版本的 Schema 与校验器、客户端动作 / 导航 / 命令注册表。S4 的 Agent 生成 UI = 生成符合 `yue-manifest.json` 词表与 FormSchema 的 JSON，经校验器后渲染；`@yue/agent` 只负责展示 SSE 事件流，不包含任何推理逻辑。

---

### 5.5 多前端面共享与首屏预算

平台控制台、应用运行时、设计器三个前端面共享同一个 yue，收益是一致性，风险是「共享 = 首屏膨胀」。约束：`@yue/core + tokens + sdk` 是唯一允许进入外壳首屏的包；`data / form / biz` 按路由懒加载；`widgets / runtime / designer` 只在进入应用或设计器上下文时加载；重型第三方库（Monaco、ECharts、Univer、pdfme、富文本）各自独立包并由消费方显式引入。Lighthouse 对外壳首屏的 script 体积预算是 error 级门禁，预算值在 §14 维护。

## 6. 选型与待决策项

选型遵守五条原则：**许可证先于功能**（排除 AGPL、要求水印可见、禁止多租户 PaaS 的条款）；**维护活跃度**（一年以上无发版的库不进推荐）；**React 生态原生**（不接受 Vue 生态移植或 jQuery 封装）；**可抽象隔离**（拖拽、画布、物料注册表都包一层 yue 抽象，供应链变动时只改适配层）；**一个场景一种原语**（同一职责不并存两个库）。表中「状态」列为锁定 / 验证门 / 待决策三种。



| 类别 | 推荐 | 备选 | 不建议（理由） | 状态 |
|---|---|---|---|---|
| 组件底座 | Base UI + Tailwind v4 + shadcn 源码自有化 | Radix（验证门不过时整体切换） | antd / MUI 混用（双轨反例） | 锁定（验证门 2026-10-09 通过） |
| 无样式原语 | `@base-ui/react` 1.x | Radix | — | 锁定（验证门 2026-10-09 通过，ADR-013 §9） |
| 表格 | TanStack Table v9 + TanStack Virtual | VisActor VTable / AntV S2（透视、大数据，S3）；AG Grid Community | 依赖 AG Grid Enterprise 却不采购；Vue 生态表格 | 锁定 |
| 表单 | 自有 Schema（数据部分 JSON Schema 2020-12 + 自有 UI Schema）+ react-hook-form + Ajv | RJSF / JSON Forms 作参考实现 | Formily（停滞）、amis（自带体系）；zod 只用于 TS 侧导出契约 | 锁定 |
| 拖拽 | dnd-kit（yue 包一层抽象） | pragmatic-drag-and-drop；`@dnd-kit/react` 1.0 后迁移 | react-beautiful-dnd（废弃）、react-dnd（停更）、SortableJS | 锁定，抽象层隔离供应链风险 |
| 流程 / 逻辑画布 | 审批流：DOM 节点树自绘；自动化流：`@xyflow/react` 12（S3） | AntV X6、LogicFlow | bpmn-js（水印许可证）、Elsa Studio 嵌入 | 锁定 |
| 栅格布局 | react-grid-layout 2.x | — | Vue 生态 | 锁定 |
| 页面 / 门户搭建 | S2 自研（借鉴 Puck 的注册表 + 字段描述 + slot）；S3 门户可直接用 Puck | GrapesJS | lowcode-engine、craft.js（停滞） | 待 S3 |
| 图表 | ECharts 6（option 纯 JSON、运行时切主题） | AntV G2、VChart、Recharts | — | 锁定 |
| 电子表格 / 报表 | Univer 开源核心 + .NET 取数 / 展开 / 导出（xlsx 放后端避开 Pro） | 采购 Univer Pro；FortuneSheet | Luckysheet、x-spreadsheet（停更）、Handsontable（商业） | 待 S3 |
| 打印模板 | pdfme 6.x | — | hiprint（jQuery） | 待 S3 |
| 大屏 | 自研：自由画布 + react-grid-layout + ECharts | 借鉴 GoView / DataEase 交互 | 参考平台大屏源码（许可证）、DataV React（停更） | 待 S3 |
| 代码编辑器 | Monaco（桌面、ESM）+ CodeMirror 6（移动 / 轻量） | — | — | 锁定（CodeMirror 仓库迁出 GitHub，供应链审计换地址） |
| 前端脚本沙箱 | quickjs-emscripten（表达式 / 联动脚本，内存上限、超时）；iframe sandbox 隔离第三方 UI | SES、near-membrane | `eval` / `new Function`、ShadowRealm（Stage 2.7） | 锁定（S2 可选兜底） |
| 状态 / 服务端状态 | Zustand + TanStack Query | — | — | 锁定 |
| 路由 / i18n | react-router 7 + i18next | — | — | 锁定 |
| API client | 从 Kun OpenAPI 生成（orval / openapi-typescript）+ 契约快照 | — | 手写 fetch 封装 | 锁定 |
| 富文本 / Markdown | **待调研**（Q39）：S2 前补一次 React 富文本编辑器选型 | — | TinyMCE 5 / Vditor（参考平台用法，非 React 生态） | 待决策 |
| 文档站 | Storybook 10；产品文档 Rspress 2 / Fumadocs | Ladle | Docusaurus（React 18） | 锁定 |
| 多标签 KeepAlive | 待评估方案（React 无原生 KeepAlive） | Activity / offscreen 方案 | iframe 多实例 | 待决策（S1 可选 / S2 必做） |

---

## 7. 安全考量

以 §13.10 为准，本 spec 的落点：

1. `@yue/runtime` 的联动三层中，第三层 QuickJS 沙箱只暴露白名单 `formApi` 子集，不暴露 `window`、`fetch`、令牌与 `PlatformClient`；超时与内存上限来自配置 Schema。
2. `@yue/sdk` request 拦截器链：令牌只存内存 + HttpOnly refresh；刷新队列合并并发；租户头由当前上下文注入，服务端以令牌为准；大数（int64）按字符串处理。
3. 嵌入（iframe / 预览 / 大屏 / 外链）统一走 `usePostMessageHandshake` 或一次性预览票据；禁止把令牌拼进 URL；CSP 由插件清单 `permissions.ui` 生成，外壳在加载远程前校验来源白名单。
4. `auth` 字段只做体验裁剪：被隐藏的字段仍可能在服务端返回脱敏值，组件不得据此推断权限。
5. 不可信 UI（S3）：Worker 远程渲染只允许 `yue-manifest.json` 中 `status: stable` 的组件；iframe sandbox 不带 `allow-same-origin`。
6. 不在 localStorage 持久化 access token、锁屏密码或任何密钥；Preferences 只存非敏感偏好。
7. 依赖供应链：`catalog:` 统一版本、锁文件、publint、SBOM；重型第三方库独立包并懒加载，便于单独升级。
8. 可寻址属性 `data-testid / data-slot` 不含业务数据；错误边界不向用户展示堆栈。

---

## 8. 验收标准（S1 最小集）

对应 §14 的层级与门禁；证据按「PR、命令、环境、结果」四件事留存。

**令牌与主题**
- [x] `@yue/tokens` 三层令牌生成 CSS 变量与 TS 类型；明暗两态对比度单测 WCAG AA ≥ 4.5:1 全部通过（CI）——PR #32，`packages/yue/tokens/src/tokens.test.ts`，CI `yue` 的 unit 门禁
- [x] 令牌命名冲突单测通过；不存在同名异义——同上，另由 `packages/yue/tokens/scripts/check-tokens.mjs` 在 lint 门禁扫描工作区全部 CSS
- [x] `data-theme / data-brand` 运行时注入生效：切换后无需刷新且无首帧闪烁（Playwright 用例）——`apps/yue-playground/e2e/theme.spec.ts` 的首帧状态快照，CI `yue` 的 a11y 门禁

**基础组件**
- [ ] `@yue/core` S1 清单约 30 个组件全部有 Storybook 用例（默认 / 悬停 / 禁用 / 错误 / 暗色）与 manifest
- [ ] 全部组件 axe critical = 0；Menu / Select / Combobox / Tabs / Tooltip / Tree 键盘路径用例通过（验证门）
- [ ] 每个组件暴露 `data-slot` 与稳定 `data-testid`
- [ ] 组件文案 100% 走 i18n key；硬编码文案 lint 为零

**外壳**
- [ ] AppShell 单一侧栏布局；后端菜单 → 路由；`menuTypeRegistry` 支持页面与外链两类并可由插件注册新类型
- [ ] MF2 加载至少一个官方插件 UI；插槽注册表带 `/vN`；`engines.yue` 不满足时拒绝加载并返回结构化错误
- [ ] 租户切换器切换后请求头与上下文同步更新；代管横幅在平台代管模式下常驻
- [ ] 403 / 404 / 500 兜底页、ErrorBoundary、OfflineBanner、UpdateBanner 可触发
- [ ] 运行时配置经 ConfigMap 注入，改配置不重编译（staging 演示）

**数据与表单**
- [ ] DataTable 基础版：服务端分页 / 排序 / 筛选、行选择、列显隐、固定列、三态（空 / 加载 / 错误）用例通过
- [ ] Upload 分片 + MD5 秒传 + 断点续传在 staging 对 `qian.files` 通过
- [ ] SchemaForm 由 FormSchema 草案渲染并提交；Ajv 校验错误定位到字段；编辑 / 只读态切换无需第二套组件
- [ ] 校验规则目录与脱敏规则目录可枚举（带 id 与参数 Schema），前后端同一份

**业务选择器与认证**
- [ ] OrganizeSelect / UserSelect / RoleSelect 经 `PlatformClient` 取数，不直连 URL
- [ ] 菜单 / 按钮授权树完成一次真实授权并在服务端生效（不是仅前端勾选）
- [ ] 账号密码 + SSO（通用 OIDC）+ 多租户选择 + 登录过期弹窗 E2E 通过；令牌不出现在 URL

**契约与 SDK**
- [ ] FormSchema / ListSchema / 控件 manifest / SSE 事件的 JSON Schema 发布并带版本；TS 类型由 Schema 生成
- [ ] `registerWidget` 契约可用：注册一个示例控件后同时出现在 `listWidgets()` 与 `GET /registry/widgets`
- [ ] API client 从 Kun OpenAPI 生成；契约快照测试进 CI

**工程化**
- [x] 分层边界 lint（eslint-plugin-boundaries / dependency-cruiser）进 CI 且无违规（S1-YUE-003：`packages/yue/boundaries.config.mjs`，自测 `tests/gates/yue/`）
- [ ] CI 门禁矩阵全部启用：lint、typecheck、unit、a11y、Lighthouse（script 体积 error 级）、边界检查、publint、循环依赖、i18n 硬编码（S1-YUE-003 已启用前八项，工作流 `yue`；i18n 硬编码 lint 待 `@yue/sdk` 的 i18n 落地后加入）
- [ ] Changesets 发包到私有 npm 成功一次；`0.x` 版本策略写入 CONTRIBUTING（S1-YUE-003 已配置 Changesets 与 `0.x` 策略，见 `packages/yue/README.md`「版本策略」；发包渠道待定，见该节「发布」）
- [ ] Storybook 10 文档站可访问，含令牌可视化页
- [ ] 视觉回归基线建立（S1 可选；未建立需在验收记录标 blocked 并给出 S2 期限）

---

**S2 验收预告（不计入 S1 退出，供排期参考）**
- [ ] S2 控件集每个控件三态一致性测试通过；`type@version` 与后端 Field Type Catalog 100% 配对
- [ ] 表单渲染器以同一份 FormSchema 在 PC 与 `device: h5` 覆盖下渲染，不存在第二份元数据
- [ ] 联动三层各有用例：声明式动作、表达式 DSL、（如启用）QuickJS 沙箱在超时与内存上限下被终止且无令牌可达
- [ ] 三大设计器的产出物经共用校验器后落库；Agent 生成的样例 Schema 走同一校验
- [ ] 视觉回归成为 PR 阻断门禁；manifest 全量导出并被远程渲染白名单消费
- [ ] 列表渲染器三种必做形态的 E2E 通过；`auth` 四开关产出的可授权项在服务端生效

## 9. 后续扩展

| 阶段 | 扩展 | 前置 |
|---|---|---|
| S2 | `@yue/widgets` 控件集、`@yue/runtime`、三大设计器、DataTable 进阶、CodeEditor / Cron、`@yue/charts`、manifest 全量、视觉回归阻断；`@yue/mobile` H5 渲染器（可选） | S1 契约包与注册表 |
| S3 | 不可信 UI 沙箱（Worker 远程渲染 + iframe）、门户设计器与运行时、通知抽屉、多布局与偏好抽屉、主题预设、打印 / 报表 / 大屏、外链运行时、shadcn 私有 registry（可选） | manifest 词表、CSP 生成、MF2 版本并存 |
| S4 | `@yue/agent`（ToolCallCard、ThinkingTimeline、AgentTrace、Composer）、shadcn MCP Server（Agent 检索与安装 yue 区块）、MCP Apps 视图、AI 助手悬浮窗、Agent 生成 UI 经 manifest 与校验器落库 | S1 定义的 SSE 事件契约、客户端动作注册表、机器可读 manifest |

---

## 待确认

1. `@yue/contracts` 作为独立无 UI 包是本 spec 的拆分建议（基线把契约放在「yue 内核」），需 ADR-013 确认包边界。
2. `@yue/shell` 对 `@yue/form` 的同层依赖（登录表单）是分层规则的唯一例外，需 ADR-013 记录；否则认证页族应下沉为独立包。
3. 已裁决（R17）：D16「控件包与 Field Type Provider 同名同版本发布」保留；ADR-011 D11.9 已补 Field Type Catalog 版本策略（按 `type@version` 登记、SemVer 判兼容），与本 spec 一致。
4. 多标签 KeepAlive 等价能力的方案未定（S1 可选 / S2 必做），影响 `@yue/shell` 路由设计。
5. 富文本 / Markdown 编辑器选型（Q39）S2 前补调研，影响 `@yue/code-editor` 的包边界。
6. ListSchema 的 `layout` 枚举含 `group / card / inline-edit`（S2 可选），与功能规格章的列表形态分级需一致。
7. `WidgetMeta.category` 预留 `ocr`，但 OCR 控件库本期不做（§12.8）；是否保留枚举值需产品确认。
