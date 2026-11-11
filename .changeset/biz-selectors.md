---
"@idadawn/yue-biz": patch
"@idadawn/yue-data": patch
"@idadawn/yue-core": patch
---

biz 交付 S1 基础版业务选择器：ApiSelect / useRemoteOptions、DictSelect、OrganizeSelect、UserSelect、RoleSelect、菜单与按钮授权树、IconPicker，全部经注入的 PlatformClient 取数、文案走 i18n key，取数契约由 OpenAPI 生成。data 的 Tree 增加 cascade 开关（关闭后节点独立勾选），TreeSelect / Cascader 触发器的可访问名称改为「标签 + 当前值」以满足 WCAG 2.5.3；core 导出已登记图标名 ICON_NAMES。
