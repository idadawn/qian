---
"@idadawn/yue-core": patch
"@idadawn/yue-data": patch
"@idadawn/yue-form": patch
"@idadawn/yue-shell": patch
"@idadawn/yue-tokens": patch
---

管理台样式刷新：新增画布令牌 `--yue-canvas`，浅色侧栏改为白底。core 新增 Steps 步骤条、MenuRadioGroup / MenuRadioItem 单选菜单与 `isIconName`。Badge 支持状态圆点，Alert 支持标题与语气图标，Result 改为大图标居中版式，Button 新增 link 变体，Tabs 新增 line 变体与计数，Sheet 新增页头、主体、页脚与宽度档，Descriptions 新增上下排布，Pagination 改为描边按钮，图标从 14 个增加到 50 个。DataTable 改为卡片容器并支持表头插槽，页长显示「N 条/页」。SchemaForm 的枚举可按语言包前缀显示选项文案，新增 `qian.segmented` 分段控件。抽屉表单的操作按钮移到固定页脚，抽屉里的子步骤表单用 `inlineActions` 把按钮留在表单内、不占页脚。SchemaForm 新增带标题的字段组（无字段节点带 `labelKey`）与 `fieldExtra` 附加提示，自动填充语义补 name / email / tel / organization / one-time-code。AppShell 的侧栏改为品牌、工作区卡片、分组导航（支持图标与角标）和底部账号卡，顶栏放侧栏开关、面包屑与工具；主题和语言改为图标单选菜单。原有公开属性与受控协议保持兼容。
