---
"@idadawn/yue-tokens": patch
"@idadawn/yue-core": patch
"@idadawn/yue-data": patch
"@idadawn/yue-form": patch
"@idadawn/yue-shell": patch
---

按租户开通流程原型补齐令牌与基础组件（S1-YUE-019）。

tokens：画布改为冷灰（新增 slate 色阶与 650 档）；新增控件、次要、说明三档文字色，强边框，主色浅底 / 柔底 / 柔底文字 / 浅边框 / 强边框，状态色的边框 / 实色 / 深色文字三档，紫与青两种点缀色，品牌深色面三件套；组件层新增 `--yue-input-height`，输入类控件高度按场景整体调档；`--radius-xl` 为 12px；危险色、状态文字与状态底色按原型调整；正文行高改为固定 22px（`text-sm` 的行高写成 1.375rem，13px 的子元素继承同一行高），去掉 body 上强制的灰阶抗锯齿。

core：Button 新增 `soft`、`destructive-outline` 变体与 `xs`、`xl` 尺寸，描边按钮改为强边框加控件文字，实心按钮的底色铺满透明边框（与同尺寸描边按钮外形一致，40px 按钮看上去就是 40px）；Badge 改为底色加边框加实色圆点，Tag 支持 `dot`；Alert 改用深一档文字并新增 `size="sm"`（表单内，文字取状态文字色），图标跟随文字色，md 带标题时上下 14px、标题与正文间距 2px；Result 去掉图标下方多出的间距与标题字距收紧，说明文字最宽 520px、用说明色；Avatar 新增 `tint`、`shape` 与按种子稳定取色的 `avatarTint`；Steps 新增 `tone="inverse"` 与 `size="lg"`；Tabs 的 line 变体新增 `countVariant="pill"`；Descriptions 新增 `grid`、`justified` 布局与 `span: 'full'`，并独立成模块（外壳子集只引 Descriptions 时不再带上 ScrollArea 与拖拽分栏）；MenuItem 新增 `variant="destructive"`；MenuSubTrigger 末尾加子菜单箭头；Input 的 `type="search"` 去掉浏览器自带的清除按钮；新增 globe、key、lock、message、network、shield、sliders、layoutGrid 图标，图标默认线宽 1.75；外壳子集 `./shell` 补上 Avatar、Breadcrumb、Descriptions 与 MenuSub / MenuSubTrigger。修复 Dialog / AlertDialog / Drawer / Popover / Sheet 的 Close 经 `render` 交给描边 Button 时边框被基础类盖成透明：Close 传了 `render` 时不再叠加基础按钮类，样式完全由 render 的元素决定（交给 Button 时用它自己的变体；交给其他元素时须自带样式）。

data：DataTable 新增列 `align`、`toolbarEnd`、`columnToggle` 与 `density`，表头与分页栏按原型调整；表格内部滚动区改为 group，不再与外层出现两个同名 region 地标。TreeSelect 浮层给出固定宽度，树行不再换行（虚拟化行高固定，换行会盖住相邻行）。

form：SchemaForm 新增 `size`、`layout`、`columnWidth`、`groupVariant="card"` 与 `fieldAdornment`（输入框内的后缀、输入框右侧的单位文字与附加按钮，后缀与单位关联到输入框）；`useConfirm` 新增 `tone: 'danger'` 与 `confirmLabel`；`useDrawer` 的 `description` 可传带格式的内容。

shell：新增认证页布局 AuthLayout（品牌深色面加内容区，sign-in 与 form 两档）、PlainLayout、BrandMark、WorkspaceCard 与账号菜单里的语言子菜单 LocaleMenu；AppShell 的侧栏开关移到品牌行；PreferenceControls 新增 `locale` 与 `localeStyle`；PasswordLogin 改为 44px 控件，支持记住账号、忘记密码、其他登录方式与表单下方内容，不再依赖 SchemaForm；TenantSelection 改为工作空间卡片并支持 `applyHref`、`hideBack`，卡片按钮的可访问名称就是企业名，租户号与角色作为说明读出；Authentication 新增 `frame`、`loginProps` 与 `selectionProps`，不传 `frame` 时内容按原型列宽居中（登录 400px、选择工作空间 600px），不再铺满宿主容器；`matchMenuItem` 的根路径「/」只精确命中，不再作为所有路径的前缀。

除 Close 传 `render` 时的样式来源（见上）与 Authentication 不传 `frame` 时的列宽外，原有公开属性保持兼容。
