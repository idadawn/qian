# 迁移映射 05 · 参考平台前端组件与设计器全集 → yue 分层与阶段、工程化、移动端策略

> 来源基线：U（前端架构与 yue 组件库）。相关决策：ADR-013 前端与 yue、ADR-002 插件模型（前端插件化 / MF2）、ADR-016 iClaw 边界；PRD §07（S1 外壳与基础组件）、§08（S2 控件集与设计器）、§14（质量）。
> 口径：前端不「移植」参考平台的 Vue 代码，而是以其组件与设计器全集（去重后 251 条目）作为 yue 的**物料清单与能力对照**，在 React 19 + Base UI + Tailwind v4 + shadcn 源码自有化的栈上重建。「目标插件 ID」列写 yue 包与宿主 Qian 插件。优先级标记同映射 01。

## 0. 本域的迁移判断

参考平台前端是「两套并存」的典型反面教材：框架无关原语 + 自绘壳之上再叠加业务 UI 库封装与第三方表格，结果弹窗、表单引擎、表格引擎、Tree、IconPicker、Loading、二维码都有两到三套实现；控件在编辑态、只读详情、列表单元格三处各自维护；事件脚本经 `eval` 执行且能拿到访问令牌；令牌拼进 iframe URL。我方 iClaw 前端有好的工程规范与令牌思路，但没有组件库，数据密集组件全部缺失。yue 按 ADR-013 走路线 A：Base UI + Tailwind v4 + shadcn 源码自有化为基础层，数据密集组件在 TanStack 上自建，**只保留一套**表单 / 表格 / 弹层引擎贯穿 S1 后台 CRUD 与 S2 低代码运行时，六层分层用 lint 工具强制，Module Federation 2 外壳 + 带版本的插槽注册表 S1 必做。为 S4 从 S1 就预留三件事：组件与控件的机器可读 manifest、带版本的 Schema 与校验器、客户端动作 / 导航 / 命令注册表。

## 1. 参考平台前端全集 → yue 映射总表

| 参考平台组件族 | 概括其做法 | Qian / yue 处理方式 | 目标 yue 包 / 宿主插件 | 阶段·优先级 | 说明与理由 |
|---|---|---|---|---|---|
| A · 原语层（31 组：accordion、dialog、dropdown-menu、select、tabs、tooltip、tree 等） | shadcn 风格的 Vue 原语封装，内核不依赖业务 UI 库 | 重新设计：`@yue/core` 基于 Base UI + shadcn 源码自有化；S1 首迭代做原语覆盖性验证门（Menu / Select / Combobox / Tabs / Tooltip / Tree 的键盘与可访问性），不通过整体切 Radix，**不混用** | `@yue/core` | S1·P0 | ADR-013；Q12 接受路线 A 风险 |
| B · 基础组件（15：Button 三处实现、Icon 两套图标体系、DropdownMenu 两处、Arrow 两处…） | 多套并存 | 每个组件只保留一套；Icon 单一入口（iconify 名 / 组件 / 图片 url）与单一图标体系；每个组件 DOM 暴露 `data-slot` 与稳定 `data-testid` | `@yue/core` | S1·P0 | 反例：多套实现 |
| C · 布局与壳（AdminLayout 7 种布局模式、BasicLayout、AuthenticationLayout、TabsView 四种风格、自绘 Menu、Breadcrumb、Page / ColPage / LeftTree、IFrameView、LazyContainer） | 7 种布局 + header 4 种模式 + 多标签拖拽 / 右键 / 中键 / 固定；KeepAlive + iframe 多实例 | AppShell **单一侧栏布局**（S1），多布局 S3 可选；后端菜单元数据 → 路由 + `menuTypeRegistry`；Page / PageHeader / ColPage / LeftTree S1；多标签基础版 S1 可选、完整版 S2（React 侧 KeepAlive 等价能力需评估）；壳插槽（logo、header-left/right、user、footer） | `@yue/shell` + `qian.shell` | S1·P0 / S1·P1 / S3·P1 | 补充裁决 Q38 |
| C' · 设计器布局控件（Row 栅格、Card、Collapse、Tab、Steps、TableGrid 表格容器） | Collapse 在某小程序端不渲染；TableGrid 约 300 行专用合并逻辑 | yue 容器组件 S1；作为控件走控件注册表并声明端支持 S2；TableGrid S2 可选 | `@yue/core` + `@yue/widgets` | S1·P0 / S2·P0 / S2·P1 | 端差异显式降级 |
| D · 表单录入叶子（34：Input / Textarea / InputNumber / NumberRange / Password 强度 / PinInput / Select / TreeSelect / Cascader / Radio / Checkbox / Switch / Slider / Rate / DatePicker 族 / TimePicker 族 / ColorPicker / IconPicker 两套 / Cron / 富文本 TinyMCE / Markdown Vditor / CodeEditor 两套 / AutoComplete / I18nInput / Upload 族 / Sign / Signature / Location / OCR / Calculate / DateCalculate / AreaSelect / Carousel） | 只读详情态仅 Input / InputNumber 支持；`useScan` 在 PC 仅声明；上传分片 + MD5 在主应用 | 基础输入、选择、开关 → `@yue/core` S1；日期 / 时间族、Upload（分片 + MD5 + 预览）、Tree / TreeSelect / Cascader → `@yue/data` S1；Slider / Rate / ColorPicker / Steps / Timeline S2；CodeEditor（桌面 Monaco + 移动 CodeMirror 6）、Cron S2 必做；富文本 / Markdown 选型待补；Sign / Signature / Location S3；OCR S4；编辑 / 只读态为引擎级统一能力 | `@yue/core` / `@yue/data` / `@yue/code-editor` / `@yue/widgets` | S1·P0 / S2·P0 / S2·P1 / S3·P1 / S4 | 补充裁决 Q39 |
| E · 业务选择器与关联（18：Organize / Pos / User / Users / Role / Group Select、选人弹窗、PopupSelect / PopupTableSelect / PopupAttr、RelationForm / Attr / Query、ApiComponent、字典选项三源、SelectInterface 参数映射、BillRuleModal、MsgModal） | ApiComponent 远程选项高阶包装；字典 store 全量缓存；参数来源 5 种 | `@yue/biz` 全部经注入的 `PlatformClient` 取数，不直接依赖后端 URL；`useRemoteOptions` 泛型包装复刻 ApiComponent 模式；ApiSelect / DictSelect / OrganizeSelect / UserSelect / RoleSelect / 菜单与按钮授权树 / IconPicker S1；PosSelect / GroupSelect / UsersSelect / PopupSelect 族 / RelationForm / InterfaceSelect S2；BillRuleSelect / MsgTemplateSelect S2–S3 可选；签章选择 S3 | `@yue/biz` | S1·P0 / S2·P0 / S3·P1 | 鉴权与租户管理界面 S1 就需要 |
| F · 系统字段与子表（OpenData 六系统控件共用只读组件、BillRule 单据编号、InputTable 设计子表） | InputTable 是参考平台最复杂的控件 | 系统字段只读展示 S2；子表（行内编辑、增删复制、批量、合计、校验汇总、外链模式只保留新增）S2 必做 | `@yue/widgets` | S2·P0 | 复杂度热点第一 |
| G · 数据展示（13：Text / TextTag、Barcode、Qrcode 两处、Empty、ImagePreview、EllipsisText、JsonViewer、CountTo 两处、SpineText、Tree 三套、Iframe、Alert、Image / Video） | Tree 三套、Qrcode 两处 | 各保留一套；Tree（工具栏 / 右键 / 搜索 / 行操作）S1；Empty / Alert / ImagePreview S1；QrCode S1 可选（扫码登录时提前）；Barcode / JsonViewer / Statistic / EllipsisText S2 可选 | `@yue/core` / `@yue/data` | S1·P0 / S1·P1 / S2·P1 | — |
| H · 反馈与弹层（15：Modal 两套、Drawer 两套、Popup 全屏页式弹层、函数式弹窗、Message / Notification / Confirm 统一出口、Loading 三套、Tooltip / HelpTooltip、HoverCard / Popover、ContextMenu 两套、Captcha 四种、Fallback 兜底页、ValidatePopover、BatchOperate、BackTop / FullScreen） | register 模式与 API 模式两套弹窗 | 一套弹层 API `useModal / useDrawer / usePopup`（open / close / lock / setState / 共享数据），表单弹窗按 `popupType` general / fullScreen / drawer 三选一；Message / Confirm / Notification 统一出口；Fallback 403 / 404 / 500 / 离线 + ErrorBoundary + OfflineBanner + UpdateBanner S1；Captcha S1 可选（登录策略要求时必做）；ValidatePopover 演进为设计器校验定位（S2）；BatchOperate 三态结果 S1 | `@yue/core` / `@yue/form` / `@yue/designer` | S1·P0 / S1·P1 / S2·P0 | 反例：弹层双轨 |
| I · 引擎与运行时渲染器（12：两套 Schema 表单引擎、两套 Schema 表格引擎、编辑态 Parser、806 行只读详情渲染器、动态列表 1256 + 1003 行、搜索表单派生、动作执行器、门户 / 报表 / 打印运行时、流程表单宿主、外链运行时） | 双轨引擎；控件三处呈现各自维护；详情态只两个控件支持 | **一套表单引擎**：Field 抽象 + react-hook-form + Ajv（由 Schema 生成）+ SchemaForm（JSON Schema 数据 + UI Schema 布局 + API 式 FormApi + `auth` 字段级权限 + 声明式 `dependencies` 联动）S1，贯穿后台 CRUD 与低代码；**一套 DataTable**（TanStack Table v9 + Virtual）S1 基础 / S2 进阶（树表、列权限、行内编辑、复杂表头）；表单渲染器三态（edit / readonly / cell）由控件注册表提供，不再有独立详情渲染器 S2；列表渲染器按「形态策略 + 动作执行器 + 视图 / 权限装饰器」拆分 S2；搜索表单派生 S2；流程表单宿主（节点字段权限、按 `actions` 渲染按钮）S2；门户 / 报表 / 打印运行时 S3；外链运行时 S3 可选 | `@yue/form` / `@yue/data` / `@yue/runtime` | S1·P0 / S2·P0 / S3·P0 / S3·P1 | ADR-013 核心裁决 |
| J · 壳部件（17：偏好抽屉 13 组、Theme / Color / Language / Layout Toggle、GlobalSearch、LockScreen、Notification 消息抽屉、UserDropdown、CheckUpdates、Watermark、SystemToggle 三面新窗口切换、TenantDropDown、MyCollect、Chat 内置 IM、AI 助手悬浮窗、GlobalTranslate、Copyright） | 「控制台 / 应用前台 / 应用后台」三面以新窗口切换；AI 助手前端动作集硬编码 | **统一一个壳**，按「平台控制台 / 应用」上下文切换（Q37）；主题 / 语言切换、用户下拉、租户切换器 + 平台代管常驻横幅、当前上下文显式声明与请求头注入 S1；Preferences 对象 S1、偏好抽屉 UI S3 可选；全局搜索 S2 可选；通知抽屉（消息类型 → 落地页由注册表声明）S3；Watermark S2 可选；收藏 / 常用 S3；CheckUpdates → UpdateBanner S1；锁屏不做；内置 IM 不做；AI 助手 / 划词翻译 S4，前端动作改客户端动作注册表 | `@yue/shell` + `qian.shell` / 消息 / 工作台 / Agent 运行时插件 | S1·P0 / S2·P1 / S3·P0 / S3·P1 / S4 / 不做 | 补充裁决 Q37 / Q38 |
| K · 认证页族（10：账号密码、验证码登录、扫码登录、注册、忘记密码、第三方登录、登录过期弹窗、多租户二次选择、SSO 跳转与票据轮询、申请 / 加入企业） | 移动端 MD5 后 AES 传输；申请 / 加入企业承接服务未交付 | 统一 `AuthProvider` 契约、登录方式各为一个插件、走标准 OIDC code + PKCE；账号密码 + SSO 回调 + 多租户选择 + 过期弹窗 S1 必做；验证码登录 / 第三方登录按钮区 / 忘记密码 S1 可选；扫码登录 / 注册 / 申请加入 S3 | `@yue/shell` 认证页族 + `qian.identity` / `qian.tenancy` | S1·P0 / S1·P1 / S3·P1 | 与映射 01 一致 |
| L · 门户 / 仪表盘物料（7 族：仪表盘卡片 8 个、布局 card / tab、基础 text / image / carousel / video / iframe、系统 todo / notice、图表 10 种、filter、EchartsUI + useEcharts） | PC 门户 21 个 key、移动端 22 个 key 不同步；没有图表「组件库」 | 单一物料注册表 + 端 `visibility`；`@yue/charts` ECharts 6 封装 `Chart + useChart`、令牌桥接主题、option 可序列化 S2 可选 → S3 必做；门户物料 S3 | `@yue/charts` / `@yue/widgets`（门户物料）+ `qian.portal` | S2·P1 / S3·P0 | 与映射 04 一致 |
| M · 通用交互（16：ConditionBuilder、SuperQueryModal、ImportModal 三步、ExportModal、BtnEvent、PreviewModal、HistoryPopover、FormExtraPanel 数据日志、ConvertModal、PrintSelect / PrintBrowse、ShortLinkModal、AddDetailExtraModal、AiFieldModal、KitForm、TemTransformModal、ScriptDemo） | ConditionBuilder 被数据过滤 / 高级查询 / 数据权限 / 关联查询 / 9 个流程面板复用 | ConditionBuilder（操作符按字段类型、输出 Filter AST）S2 必做且归内核层被列表 / 权限 / 流程复用；ImportWizard / ExportDialog / SuperQuery S2 必做；PreviewModal（设计中 / 已发布 × Web / 二维码）S2；BtnEvent → 动作配置 S2；数据日志面板、详情页签配置 S2 可选；ConvertModal / PrintSelect 随打印 S2；ShortLink 随外链 S3；AI 入口 S4；ScriptDemo 不做 | `@yue/data` / `@yue/designer` / `@yue/runtime` | S2·P0 / S2·P1 / S3·P1 / S4 / 不做 | — |
| N · 流程运行时（8：FlowParser 1173 行按任务状态生成全部按钮、意见 / 签名 / 附件区、流程图查看、RecordTimeList 时间线、候选 / 转审加签选人、子流程查看、委托代理、审批中心列表） | 按钮矩阵在前端算；审批中心「系统开关 + 菜单权限」双重控制 | 按引擎返回的 `actions`（含 `inputSchema / preconditions / humanOnly`）渲染按钮，前端不算矩阵；审批中心待办 / 已办 / 我发起的 / 抄送 + 详情页 + 时间线 S2；待签 / 批量 / 流程图 S3；签名板 S3；委托代理随 grant 原语 | `@yue/runtime`（流程表单宿主）+ `qian.workflow.workbench` | S2·P0 / S3·P1 | 与映射 03 一致 |
| O · 设计器（10：表单设计器三栏 + 50 个属性面板、列表设计器 PC / 移动两份配置、流程设计器 bpmn-js 二次封装 + 约 30 个节点面板、门户设计器 vue-grid-layout、打印设计器 jQuery 底座、报表设计器 Univer 封装、数据集设计器、大屏独立 Vue 站以 iframe + token 嵌入、系统表单登记、菜单管理表单） | 8 个设计器 + 2 个登记 / 配置表单；属性面板手写 | 设计器基础设施（dnd-kit 封装拖拽、画布容器、Schema 驱动属性面板、大纲树、撤销重做状态机、校验器 + 定位、预览弹窗、i18n 文案输入）S2 必做；表单 / 列表 / 审批流（纵向节点树 DOM 自绘）设计器 S2 必做；数据接口配置 UI S2 必做；门户设计器（Puck 模型或 react-grid-layout）S3 必做；打印（pdfme）/ 报表（Univer 开源核心）/ 大屏设计器 S3 可选；自动化流设计器（React Flow）S3 可选；系统表单登记 → 插件清单（S1）；菜单管理表单 S1 | `@yue/designer` + `@yue/designer-*` + 各宿主插件 | S2·P0 / S3·P0 / S3·P1 / S1·P0 | 不用 bpmn-js（水印许可证）；补充裁决 Q22 |
| P · 页面渲染器（7：菜单类型 0–11 映射到 OnlineModelView / OnlineDictView / OnlineReportView / OnlinePortalView / IFrameView / pageMap 代码页面 / NotFoundView，if / else 硬编码；IFrameView 拼 token） | 菜单即元数据，但映射硬编码 | `menuTypeRegistry` 可注册：S1 页面 / 外链两类，S2–S3 由各插件扩充（在线表单、流程、字典、报表、门户）；iframe 嵌入用一次性票据或 postMessage 握手，禁止 URL 带 token | `@yue/shell` + `@yue/sdk` | S1·P0 | 安全硬线 |
| Q · 租户管理中心前端（4：租户列表 / 表单、菜单授权树黑名单、重置密码 / 管理员维护、中心账号管理无角色） | 从主前端裁剪；账号无角色、路由与后端均不校验；「至少一个主库」校验是死代码 | 租户运营后台在同一壳按上下文切换，运营账号走真实鉴权；菜单 / 插件授权树改白名单；复用 `@yue/biz` 授权树 | `@yue/biz` + `qian.tenancy` | S1·P0 | 与映射 01 一致 |
| R · 移动端专有（9：uni-app 五端渲染器约 50 控件、移动端列表 `appColumnData`、工作台、审批中心、消息 / IM / 推送、门户 tab、扫一扫协议、通讯录 / 我的 / 企业切换、文件中心） | 一套 uni-app 出五端；在线脚本 `eval` 小程序端失效；某小程序静默缺 7 类控件；URL 带 token | S1 只做契约层；`@yue/mobile`（同一 React 渲染器的端适配，H5 / PWA）S2 **不交付**（Q13），S3 可选；小程序与 App 以壳嵌 H5 + 少量原生桥；小程序原生渲染不纳入 S1–S3；不支持的端显式降级 | `@yue/mobile` + 各宿主插件 | S1·P0（契约）/ S3·P1 / 不做（原生渲染） | 补充裁决 Q13 |

## 2. yue 六层分层与阶段

| 层 / 包 | 内容 | S1 最小集合（支撑登录、租户、鉴权、文件上传等技术组件界面） | S2 | S3 / S4 |
|---|---|---|---|---|
| `@yue/tokens` 设计令牌 | primitive（色阶、字号、间距、圆角、阴影、动效、z-index、断点）→ semantic → component 三层；明暗两态 + `color-scheme`；状态色成对 + `Tone` 类型；语义令牌 → shadcn 变量全映射；命名唯一真相源（禁同名异义）；WCAG AA 对比度单测进 CI；运行时主题注入点 `data-theme / data-brand`（服务端按租户下发） | 全部 P0；DTCG 导出 P1 | ECharts 主题桥接；密度模式 P1 | 主题预设 / 背景 / 字体包上架、灰度 / 色弱、画布主题 S3 可选；DTCG S4 必做 |
| `@yue/core` 基础组件 | Base UI + shadcn 源码自有化 51 个组件 | Button 族、Input 族、Checkbox / Radio / Switch / Toggle / Segmented、Select / Combobox、Label / Field、Dialog / AlertDialog / Sheet / Drawer / Popover / Tooltip / HoverCard / DropdownMenu / ContextMenu、Tabs / Accordion / Card / Separator / ScrollArea / Resizable、Badge / Tag / Avatar / Skeleton / Spinner / Progress / Alert / Toast / Notification / Empty / Result、Breadcrumb / Pagination / BackTop / FullScreen、Icon、Descriptions（提前）；Captcha P1 | Slider / Rate / ColorPicker / Steps / Timeline / Statistic / EllipsisText / JsonViewer、Command Palette、Watermark、Carousel / Image / Video / QrCode / Barcode | — |
| `@yue/shell` 后台壳 | AppShell、菜单 → 路由 + `menuTypeRegistry`、多标签、Page / ColPage / LeftTree、Preferences、运行时配置注入、租户切换器 + 代管横幅、主题 / 语言切换、用户下拉、兜底页三件套、认证页族、MF2 运行时 + 带版本插槽注册表 | 单一侧栏布局；页面 / 外链两类菜单；多标签基础版 P1 | 应用切换（同壳按上下文）、多标签完整版、全局搜索 P1 | 通知抽屉 S3 必做；多布局 / 偏好抽屉、收藏 S3 可选；AI 助手 S4 |
| `@yue/data` 数据组件 | DataTable（TanStack）、Tree / TreeSelect / Cascader、日期时间族、Upload、ImportWizard / ExportDialog、SuperQuery、ConditionBuilder | DataTable 基础版、Tree 族、Date / Time 及 Range、Upload（分片 + MD5 + 预览）、ImagePreview | DataTable 进阶（树表、列权限、行内编辑、复杂表头必做；个人视图、合计可选）、ImportWizard / ExportDialog / SuperQuery / ConditionBuilder 必做、Transfer / Descriptions 可选 | 透视表 / Canvas 表格 S3 可选 |
| `@yue/form` 表单体系 | Field + react-hook-form + Ajv、SchemaForm + FormApi、声明式 `dependencies`、编辑 / 只读态、校验与脱敏规则目录、弹层 API、Message / Confirm 统一出口 | 全部 P0（基础联动） | 完整联动 | — |
| `@yue/charts` / `@yue/code-editor` | ECharts 6 封装；Monaco + CodeMirror 6 统一接口、Cron、富文本 / Markdown | — | CodeEditor / Cron 必做；charts、富文本 / Markdown 可选 | 门户 / 报表卡片 S3 必做 |
| `@yue/biz` 业务选择器 | 经 `PlatformClient` 取数的组织 / 用户 / 角色 / 岗位 / 分组 / 字典 / 远程选项 / 弹窗选择 / 关联表单 / 接口选择 / 授权树 / IconPicker | ApiSelect、DictSelect、OrganizeSelect、UserSelect、RoleSelect、菜单与按钮授权树、IconPicker | PosSelect / GroupSelect / UsersSelect、PopupSelect 族、RelationForm 族、InterfaceSelect、授权树列 / 字段 / 数据页签；AreaSelect / BillRuleSelect 可选 | 签章 / 签名选择 S3 可选 |
| `@yue/runtime` + `@yue/widgets` 低代码运行时与物料 | 元数据契约包（FormSchema / ListSchema / PortalLayout / FlowNodes 的 JSON Schema，带 `$schema` 与版本）、控件注册表 `registerWidget(type, {meta, Runtime, Designer, toSearchControl, toCellRenderer})`、表单渲染器三态、子表、三层联动、公式控件、列表渲染器、动作执行器、流程表单宿主、端支持矩阵与显式降级 | 契约草案（含 `device`、`auth`）、控件 manifest 格式 P0 | 控件集、渲染器、子表、联动、列表形态（普通 / 左树 / 树形必做，分组 / 卡片 / 行内编辑可选）、动作执行器、流程表单宿主、审批中心基础、设计物生命周期 UI 必做；详情页签 / 数据日志、`@yue/mobile` 可选 | 门户运行时 S3 必做；报表 / 打印 / 大屏运行时、外链运行时 S3 可选 |
| `@yue/designer` + `@yue/designer-*` 设计器 | 设计器基础设施；表单 / 列表 / 审批流 / 门户 / 自动化 / 打印 / 报表 / 大屏设计器；产出物校验器与设计器共用 | — | 基础设施、表单 / 列表 / 审批流（纵向节点树）设计器、数据接口配置 UI、校验器共用 必做；manifest 全量 | 门户设计器 S3 必做；自动化 / 打印 / 报表 / 大屏 S3 可选；代码生成预览 S3 可选 |
| 横切 `@yue/sdk` / `@yue/agent` / `@yue/mobile` | 注册表 API（导航 / 设置 / 命令 / 快捷键 / 预览器 / 客户端动作 / 控件）、`PlatformClient`、request 拦截器链（鉴权刷新队列、租户头、大数处理）、i18n、mock 中间件、运行时配置注入；SSE 事件 JSON Schema；移动端渲染器 | `@yue/sdk` 全部 P0；SSE 事件契约 P0（`@yue/agent` 组件 S4） | `@yue/mobile` 按 Q13 不交付 | `@yue/mobile` S3 可选；`@yue/agent`（ToolCallCard、ThinkingTimeline、AgentTrace、Composer）S4；shadcn MCP Server S4 |

依赖方向严格自下而上（tokens → core → shell / data / form / charts / code-editor → biz → runtime / widgets → designer），横切包只被上层消费，用 eslint-plugin-boundaries / dependency-cruiser 写死并进 CI。业务组件只做叶子，不得新建弹层 / 表单 / 表格引擎。

## 3. 工程化基线迁移

| 项 | 参考平台 / iClaw 现状 | yue 基线 | 阶段 |
|---|---|---|---|
| monorepo 与分层边界 | 参考平台只靠两条 ESLint 规则守住两道边界、业务层无规则、反向依赖 effects / stores / router；iClaw console 与 web 零共享 | pnpm workspace + `catalog:` + Turborepo；分层边界用 eslint-plugin-boundaries / dependency-cruiser 写死进 CI | S1·P0 |
| 构建产物 | 参考平台不发包 | tsdown 或 Rslib 输出 ESM + d.ts，CSS 单独导出；重型物料（Monaco、富文本、地图、ECharts）独立包懒加载 | S1·P0 |
| 文档站 | 参考平台无文档站；iClaw 用 React 18 的 Docusaurus 帮助中心 | 现有 Rspress 2 统一组件与产品文档，活演示、完整 props 表与令牌页；移除 Storybook（ADR-022） | S1·P0 |
| 机器可读组件清单 | 无 | 每个组件 / 控件导出 manifest（props JSON Schema、slots、events、a11y 角色、令牌依赖、示例）；注册表可导出 JSON | S1·P0（格式）/ S2·P0（全量） |
| 测试 | 参考平台业务层 0 测试、hooks 全注释；iClaw E2E / axe / 视觉用例未接 CI | Vitest 5 + Testing Library + `@vitest/browser`；Rspress 与独立测试共用 React 示例；Playwright E2E 进 CI | S1·P0 |
| 契约测试 | iClaw 手写形状快照、不读 OpenAPI | 从 Kun OpenAPI 生成类型（orval / openapi-typescript）；SSE 事件 JSON Schema 生成 TS | S1·P0 |
| 视觉回归 | iClaw 无基线 | Playwright 截图比对，固定字体与容器镜像，覆盖默认 / 悬停 / 禁用 / 错误 / 暗色 | S1·P1 / S2·P0 |
| 无障碍 | 未真正接入 | jsx-a11y、独立浏览器测试、Rspress 静态页面 axe + Playwright（critical / serious = 0）、令牌对比度单测 | S1·P0 |
| 性能门禁 | iClaw 有 Lighthouse 预算 | Lighthouse CI 首屏预算（script 体积 error 级）；路由级 lazy；防「共享 yue = 首屏膨胀」 | S1·P0 |
| CI 门禁矩阵 | 参考平台无 CI | lint、typecheck、unit、a11y、visual、Lighthouse、边界检查、publint、循环依赖 | S1·P0 |
| 发包与版本 | — | Changesets；SemVer（0.x 每 minor 可破坏，1.0 后只在大版本破坏并附 codemod）；私有 npm 为运行时渠道 | S1·P0 |
| 源码分发 | — | shadcn 私有 registry（面向 ISV / 插件作者）+ shadcn MCP Server（Agent 检索与安装 yue 区块） | S3·P1 / S4·P0（补充裁决 Q41） |
| 主题与品牌 | iClaw web 两套令牌 `--accent` 同名异义；媒体查询换品牌色 | 令牌 JSON + `data-theme / data-brand` 注入点；服务端按租户下发；品牌默认值经 `overrides` 注入；禁止媒体查询换品牌色 | S1·P0（机制）/ S3·P1（租户开放） |
| 国际化 | 参考平台硬编码占位；iClaw 硬编码中文 | 组件文案全部走 i18n key；内置 zh-CN / zh-TW / en-US；按域拆分懒加载；远程语言包；i18n key 表作为契约发布 | S1·P0 |
| 运行时配置 | 参考平台 `_app.config.js` + `window.__APP_CONF__` 改配置不重编译 | **沿用**改配置不重编译，但改成同源 JSON `/app-config.json`（K3s ConfigMap 挂载，外壳只解析与校验）并给外壳加 meta CSP：脚本形态下能写 ConfigMap 就能在外壳源执行任意脚本（S1-YUE-012） | S1·P0 |
| 开发体验 | iClaw console mock 中间件 | mock 模式用 Vite 中间件 + 路由表，每个插件自带 `mock.ts`；多入口预览；联调代理可注租户头 | S1·P0 |
| 代码规范 | 参考平台提交钩子全注释 | ESLint 9 flat、Stylelint、Prettier 校验入 CI、cspell、commitlint + 钩子启用 | S1·P0 |
| 测试可寻址 | iClaw `data-testid` 约定 | `data-testid` 命名 `<app>-<page>-<element>`；组件 `data-slot`；也是 Agent 可寻址基础 | S1·P0 |
| 样式策略 | 参考平台约 3800 行 SCSS 覆盖；iClaw 7211 行 BEM 大文件 | Tailwind + cva，令牌用 CSS 变量；禁止全局 BEM 大文件与 `!important` 覆盖第三方库 | S1·P0 |
| 安全基线 | 参考平台 AES 默认密钥是前端常量；token 与锁屏密码持久化；iframe URL 传 token | 不在前端持有加密密钥；不持久化 token / 锁屏密码明文；嵌入第三方页面用一次性票据或 postMessage 握手；不用 `eval` 执行租户脚本；前端权限裁剪只是体验层 | S1·P0 |

## 4. 移动端策略

| 阶段 | 做什么 | 优先级 | 归属 |
|---|---|---|---|
| S1 | 不做移动 UI。契约层七件事：认证走 OIDC / PKCE + 设备级 refresh、禁止 URL 带 token 嵌页；登录配置 / 租户选择 / 第三方登录 / 扫码登录 / 推送注册设计成任意客户端可消费的 API；表单 / 列表 schema 带 `device` 维度（一份 schema 按端覆盖）；控件 manifest 声明支持端；文件上传 / 定位代理 / OCR 作独立插件暴露 HTTP 契约；语言包可远程拉取；统一「能力链接 / 深链」规范 + 「消息类型 → 落地页」注册表 | P0 | `qian.identity` / `qian.tenancy` / `qian.forms` / `qian.notify` / `@yue/sdk` |
| S2 | 只做 PC 响应式，预留端覆盖（Q13）；`@yue/mobile` H5 表单渲染器**不交付**（基线 U 建议 S2 后段验证项，按决策顺延） | — | — |
| S3 | `@yue/mobile`：同一 React 渲染器的端适配，H5 / PWA 为主；工作台（收藏 / 最近 / 常用 + 待办角标）、审批中心（tab 与按钮矩阵由引擎返回，补流程图与打印缺口）、消息中心（类型分发 + 推送深链）、门户（物料按端过滤）、企业切换 / 加入；小程序与 App 用壳嵌 H5 + 少量原生桥（扫码、定位、推送、文件）；控件子集先覆盖基础输入 + 上传 + 组织人员 + 关联弹窗 + 子表 + 定位 / 扫码 / 签名；联动只用声明式动作 + 表达式 DSL；不支持的端显式降级 | P1（视客户面需求） | `@yue/mobile` + 工作台 / 审批 / 消息 / 门户插件 |
| S4 | 移动端消费的 schema、深链协议与扫码协议天然是 Agent 可生成 / 调用的产物 | — | Agent 运行时插件 |
| 不做 | 小程序原生渲染（S1–S3 不纳入）；原生 App（本期） | — | — |

## 5. 技术选型对照（参考平台 → yue）

| 类别 | 参考平台 | yue 推荐 | 不采用及理由 |
|---|---|---|---|
| 组件底座 | Vue 3 + 开源后台模板原语 + ant-design-vue 封装 + vxe-table（两套并存） | Base UI + Tailwind v4 + shadcn 源码自有化；S1 验证门不过则整体切 Radix | 两套设计体系混用；antd v6 仅作「控制台域临时」备选并约定迁移期限 |
| 拖拽 | SortableJS / vuedraggable | dnd-kit（抽象一层便于迁移） | react-beautiful-dnd（废弃）、react-dnd（停更） |
| 流程画布 | bpmn-js 二次封装（隐藏水印） | 纵向节点树 DOM 自绘；自由画布 `@xyflow/react` 12 | bpmn-js（许可证要求水印可见）、Elsa Blazor Studio |
| 栅格 / 门户搭建 | vue-grid-layout | react-grid-layout 2.x；门户可用 Puck 模型 | lowcode-engine（停更）、craft.js（停滞） |
| 表格 | vxe-table + 自封装表格引擎 | TanStack Table v9 + Virtual | 依赖 AG Grid Enterprise 却不采购 |
| 表单引擎 | 两套 Schema 表单引擎 | 自有 Schema（数据对齐 JSON Schema 2020-12 + 自有 UI Schema）+ react-hook-form + Ajv | Formily（停更）、amis（自带体系）；zod 只用于导出契约 |
| 图表 | echarts 5.6 + echarts-stat，无图表组件库 | ECharts 6 封装进 `@yue/charts` | — |
| 电子表格 / 报表 | Univer 封装 + 未使用的商业包声明 | Univer 1.0 开源核心 + .NET 取数 / 展开 / 导出 | Univer Pro、Handsontable、Luckysheet |
| 打印模板 | jQuery 设计器 | pdfme 6.x | 参考平台所用 Vue 封装 |
| 代码编辑器 | Codemirror 与 Monaco 两套 | Monaco（桌面）+ CodeMirror 6（移动 / 轻量）统一接口 | — |
| 富文本 / Markdown | TinyMCE 5 / Vditor | **未决**，S2 前补选型（Q39） | — |
| 脚本沙箱 | `eval` / `new Function` | iframe sandbox（第三方 UI）+ quickjs-emscripten（表达式 / 联动脚本，内存上限、超时） | `eval`、ShadowRealm（阶段早） |
| 状态 / 路由 / i18n | Pinia / vue-router / vue-i18n | Zustand + TanStack Query；路由 S1 用 `@yue/shell` 最小历史路由，S2 页面运行时引入 react-router 7；i18next | — |
| API client | 手写封装 | 从 Kun OpenAPI 生成 + 契约测试 | 手写 fetch 封装 |
| 外壳集成 | 单体前端 | Module Federation 2 运行时加载 + 带版本插槽注册表；共享依赖 singleton；不可信 UI 走 Worker 远程渲染或 iframe（S3） | 第三方不可信 UI 直接 MF 挂载（零隔离） |

## 6. 明确不做清单

| 不做的能力 | 原因 |
|---|---|
| 移植参考平台 Vue 前端或任何 Vue 组件 | 技术栈不同；两套并存是反例；许可证 |
| 两套设计体系长期混用（Base UI 与 Radix 或 antd 并存） | 复燃双轨；antd 仅限控制台域临时且约定迁移期限 |
| 锁屏 | 密码前端比对，仅 UI 遮罩，不是安全措施 |
| 内置 IM 聊天 | 非平台核心 |
| 7 种布局模式与偏好抽屉（S1） | S1 单一侧栏；S3 可选（Q38） |
| 「控制台 / 应用前台 / 应用后台」三面新窗口切换 | 统一一个壳按上下文切换（Q37） |
| `eval` / `new Function` 执行租户脚本 | 安全硬线；三层策略替代 |
| bpmn-js | 许可证要求水印可见 |
| Formily / amis / lowcode-engine / craft.js | 停滞或自带整套体系 |
| 第三方不可信 UI 直接 MF 挂载 | 零隔离；S3 用 Worker / iframe 沙箱 |
| 小程序原生渲染（S1–S3）、原生 App（本期） | 多端策略以 H5 / PWA 为主 |
| S2 H5 表单渲染器 | Q13：S2 只做 PC 响应式 |
| 直接搬 iClaw 前端代码 | 补充裁决 Q40：只允许搬四处后端代码；前端令牌架构、console 壳、注册表模式只借鉴思路重做；shadcn 生成物可直接 CLI 重新生成 |
| 独立的 806 行只读详情渲染器、两份端配置 | 注册表三态呈现；一份 schema + `device` 覆盖 |

## 7. 必须修正的做法

| # | 参考平台 / iClaw 做法 | 风险 | yue 替代做法 |
|---|---|---|---|
| 1 | 弹窗 / 表单 / 表格 / Tree / IconPicker / Loading / 二维码两到三套实现 | 维护成本、行为不一致 | 每类只保留一套引擎 / 组件，业务组件只做叶子，边界 lint 强制 |
| 2 | 控件在编辑 / 只读详情 / 列表单元格三处各自维护 | 新增控件改三处 | 注册表一次声明 edit / readonly / cell 三种呈现 |
| 3 | 事件脚本、按钮脚本、Mock 数据经 `eval`；脚本可拿到 accessToken 并以用户身份调任意接口；小程序端 `eval` 不可用 | 任意代码执行、令牌泄露、端失效 | 声明式动作 → 表达式 DSL → QuickJS 沙箱三层；不向脚本暴露令牌 |
| 4 | 大屏 / 外链 / 移动端报表与门户把 token 拼进 iframe URL；access store 持久化 token 与锁屏密码 | 令牌泄露 | 一次性票据或 postMessage 握手；不持久化敏感字段 |
| 5 | `v-auth` 只在 mounted 判定一次；数据权限前端只有开关、运行时不读取 | 前端被当作安全边界 | `auth` 作为引擎一等字段；前端权限是体验层，鉴权在 Kun |
| 6 | 菜单类型 0–11 → 7 种页面渲染器 if / else 硬编码 | 不可扩展 | `menuTypeRegistry` 可注册 |
| 7 | 表单 PC / APP 共用一份 JSON 靠 `visibility`，列表却两份 `appColumnData`；门户 21 / 22 个物料 key 不同步 | 元数据双份、端差异静默丢控件 | 一份 schema + `device` 维度覆盖；单一物料注册表 + 端 `visibility`；显式降级 |
| 8 | 分层只靠口号与两条 ESLint 规则 | 反向依赖 | eslint-plugin-boundaries / dependency-cruiser 进 CI |
| 9 | 无文档站、业务层 0 测试、钩子全注释、不发包、无 CI | 多团队消费不可控 | §3 工程化基线全部 S1 必做 |
| 10 | iClaw web 两套令牌且 `--accent` 同名异义、后引入者胜出；媒体查询 ≤768px 换品牌色 | 主题漂移 | 令牌命名唯一真相源 + 冲突与对比度单测；`data-brand` 注入 |
| 11 | AI 助手前端动作集硬编码 switch | Agent 不可发现 | 客户端动作注册表（名称、参数 JSON Schema、权限、人话描述） |
| 12 | 前端 AES 默认密钥是常量；移动端 MD5 后 AES 传输密码 | 伪加密 | TLS；不在前端持有密钥 |

## 8. 未决问题

1. **Base UI 验证门的通过标准与时点**：ADR-013 要求 S1 首迭代验证 Menu / Select / Combobox / Tabs / Tooltip / Tree 的键盘与可访问性；需明确「不通过」的判定指标与切 Radix 的决策人。
2. **控制台域临时 antd v6 的开关**：Q12 接受路线 A 风险，但未明确是否允许该备选；若允许，迁移期限与适用范围（仅平台控制台、租户可见运行时不用）需写入 ADR-013。
3. **多标签 KeepAlive 等价能力**：React 无原生 KeepAlive，参考平台靠 Vue 内置能力 + iframe 多实例；S2 完整多标签是否必须保留页面状态，影响壳的技术方案。
4. **富文本 / Markdown 编辑器选型**（Q39）：五份调研均未覆盖；S2 前补一次选型，并决定是否作为控件进 S2 必做集。
5. **`@yue/mobile` 的启动时点**：Q13 把 S2 H5 渲染器移出；S3 是否必做取决于客户面需求，需在 S2 中期拍板以便控件 manifest 的 `devices[]` 有实际消费者。
6. **iClaw 前端资产的复用边界**：Q40 只允许搬四处后端代码，前端 console 令牌架构与 ConsoleShell 按「借鉴思路重做」处理；若所有方后续放宽，可节省 S1 约一个迭代的壳与令牌工作。
7. **设计器形态与 S4 的关系**：审批流纵向节点树（Q22）已定；表单 / 列表设计器的属性面板「Schema 驱动」声明化比例预计 60%，剩余 40% 手写面板是否要求同样导出 `uiMeta` 以供 Agent 生成。
8. **供应链钉版**：dnd-kit 旧架构停更、`@dnd-kit/react` 未到 1.0、Puck 0.x、CodeMirror 迁出 GitHub、Univer Pro 水印；需在 S1 建立前端依赖的供应链审计清单与抽象层策略。
