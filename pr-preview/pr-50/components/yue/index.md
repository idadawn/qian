> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# yue 组件库

yue 是 Qian 的前端组件库（[ADR-013](/pr-preview/pr-50/design/adr/adr-013-frontend-yue.md)、[yue 专题规格](/pr-preview/pr-50/design/prd/specs/yue.md)）。包发 GitHub Packages，包名 `@idadawn/yue-<包>`，设计文档里的 `@yue/<包>` 是同一个包的简称。S1-DOCS-002 起每个组件一页，带活演示与 props 表；下表是 S1 交付的包。

| 包                     | 阶段 | 内容                                                   |
| --------------------- | -- | ---------------------------------------------------- |
| `@idadawn/yue-tokens` | S1 | 三层设计令牌（primitive → semantic → component）、明暗两态与首帧防闪烁。 |
| `@idadawn/yue-core`   | S1 | 约 30 个基础组件：按钮、输入、选择、弹层、反馈、导航等。                       |
| `@idadawn/yue-shell`  | S1 | AppShell 单一侧栏布局、页面骨架、租户切换器与兜底页。                      |
| `@idadawn/yue-data`   | S1 | DataTable 基础版、树与级联、日期时间、分片上传。                        |
| `@idadawn/yue-form`   | S1 | SchemaForm 基础版、校验与基础联动、弹层 API。                       |
| `@idadawn/yue-biz`    | S1 | 业务选择器（字典、组织、用户、角色）、菜单与按钮授权树。                         |
| `@idadawn/yue-sdk`    | S1 | 注册表 API、PlatformClient、请求拦截器链与 i18n。                 |
