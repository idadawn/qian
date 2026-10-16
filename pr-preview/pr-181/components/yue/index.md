> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# yue 组件库

yue 是 Qian 的前端组件库（[ADR-013](/pr-preview/pr-181/design/adr/adr-013-frontend-yue.md)、[yue 专题规格](/pr-preview/pr-181/design/prd/specs/yue.md)）。包发 GitHub Packages，包名 `@idadawn/yue-<包>`，设计文档里的 `@yue/<包>` 是同一个包的简称。core 已提供 51 个组件入口，每页包含真实交互演示、源码与完整 Props。安装前请在项目 .npmrc 配置 @idadawn:registry=[https://npm.pkg.github.com，并由本机](https://npm.pkg.github.com，并由本机) npm 登录提供读包权限。下表是 S1 各包的范围。

| 包 | 阶段 | 内容 |
| --------------------- | -- | ---------------------------------------------------- |
| `@idadawn/yue-tokens` | S1 | 三层设计令牌（primitive → semantic → component）、明暗两态与首帧防闪烁。 |
| `@idadawn/yue-core` | S1 | 51 个基础组件：按钮、输入、选择、弹层、反馈、导航等。 |
| `@idadawn/yue-shell` | S1 | AppShell 单一侧栏布局、页面骨架、租户切换器与兜底页。 |
| `@idadawn/yue-data` | S1 | DataTable 基础版、树与级联、日期时间、分片上传。 |
| `@idadawn/yue-form` | S1 | SchemaForm 基础版、校验与基础联动、弹层 API。 |
| `@idadawn/yue-biz` | S1 | 业务选择器（字典、组织、用户、角色）、菜单与按钮授权树。 |
| `@idadawn/yue-sdk` | S1 | 注册表 API、PlatformClient、请求拦截器链与 i18n。 |

## 基础组件

- [Button 按钮](/pr-preview/pr-181/components/yue/core/button.md)：触发保存等操作，支持尺寸、强调程度、加载和禁用状态。

- [IconButton 图标按钮](/pr-preview/pr-181/components/yue/core/iconbutton.md)：用于紧凑操作，必须提供可访问名称。

- [ButtonGroup 按钮组](/pr-preview/pr-181/components/yue/core/buttongroup.md)：把相关操作放在一起，保持各按钮的独立语义。

- [Input 文本输入](/pr-preview/pr-181/components/yue/core/input.md)：录入单行文本，支持原生表单属性与错误状态。

- [Textarea 多行输入](/pr-preview/pr-181/components/yue/core/textarea.md)：录入说明等多行文本，保留原生编辑与表单语义。

- [InputNumber 数字输入](/pr-preview/pr-181/components/yue/core/inputnumber.md)：通过键盘或增减按钮输入数字，并遵守范围限制。

- [InputPassword 密码输入](/pr-preview/pr-181/components/yue/core/inputpassword.md)：在保留输入值的同时切换密码显示状态。

- [PinInput 验证码输入](/pr-preview/pr-181/components/yue/core/pininput.md)：分格录入验证码，完成一格后自动移动焦点。

- [Checkbox 复选框](/pr-preview/pr-181/components/yue/core/checkbox.md)：独立勾选选项，支持键盘和受控状态。

- [Radio 单选项](/pr-preview/pr-181/components/yue/core/radio.md)：与 RadioGroup 组合，在一组选项中选择一项。

- [RadioGroup 单选组](/pr-preview/pr-181/components/yue/core/radiogroup.md)：管理一组互斥选择，方向键可切换选项。

- [Switch 开关](/pr-preview/pr-181/components/yue/core/switch.md)：控制一个立即生效的二态设置。

- [Toggle 切换按钮](/pr-preview/pr-181/components/yue/core/toggle.md)：用按下状态表达格式或工具的选择。

- [Segmented 分段选择](/pr-preview/pr-181/components/yue/core/segmented.md)：在少量互斥模式之间切换。

- [Select 下拉选择](/pr-preview/pr-181/components/yue/core/select.md)：从预设选项中选取值，显示对应标签。

- [Combobox 可搜索选择](/pr-preview/pr-181/components/yue/core/combobox.md)：在弹层内部搜索并选择选项。

- [Label 标签](/pr-preview/pr-181/components/yue/core/label.md)：用原生关联把说明与输入控件连接起来。

- [Field 表单字段](/pr-preview/pr-181/components/yue/core/field.md)：组合标签、描述、输入和校验反馈。

- [Dialog 对话框](/pr-preview/pr-181/components/yue/core/dialog.md)：在模态弹层中完成编辑，关闭后恢复焦点。

- [AlertDialog 确认对话框](/pr-preview/pr-181/components/yue/core/alertdialog.md)：对重要操作提供明确的确认和取消选择。

- [Sheet 侧栏弹层](/pr-preview/pr-181/components/yue/core/sheet.md)：从侧边展示筛选或补充信息。

- [Drawer 抽屉](/pr-preview/pr-181/components/yue/core/drawer.md)：在抽屉中承载更多选项与补充操作。

- [Popover 浮层](/pr-preview/pr-181/components/yue/core/popover.md)：在触发器旁展示短小的设置或操作内容。

- [Tooltip 提示](/pr-preview/pr-181/components/yue/core/tooltip.md)：通过悬停或键盘焦点显示补充说明。

- [HoverCard 悬浮卡片](/pr-preview/pr-181/components/yue/core/hovercard.md)：悬停查看对象的补充资料。

- [Menu 菜单](/pr-preview/pr-181/components/yue/core/menu.md)：通过键盘或指针选择操作，禁用项保持可辨识。

- [DropdownMenu 下拉菜单](/pr-preview/pr-181/components/yue/core/dropdownmenu.md)：以 Menu 的兼容名称组合下拉操作。

- [ContextMenu 右键菜单](/pr-preview/pr-181/components/yue/core/contextmenu.md)：通过右键或菜单快捷键打开上下文操作。

- [Tabs 标签页](/pr-preview/pr-181/components/yue/core/tabs.md)：在多个内容面板之间切换，并维护焦点语义。

- [Accordion 折叠面板](/pr-preview/pr-181/components/yue/core/accordion.md)：按需展开问题说明或分组内容。

- [Card 卡片](/pr-preview/pr-181/components/yue/core/card.md)：组合标题、说明、内容和操作区域。

- [Separator 分隔线](/pr-preview/pr-181/components/yue/core/separator.md)：表达相邻内容的视觉或语义分隔。

- [ScrollArea 滚动区域](/pr-preview/pr-181/components/yue/core/scrollarea.md)：在受限区域中展示长内容，并提供可访问名称。

- [Resizable 可调整面板](/pr-preview/pr-181/components/yue/core/resizable.md)：通过拖动或键盘调整相邻面板的宽度。

- [Badge 徽标](/pr-preview/pr-181/components/yue/core/badge.md)：用简短文本表达状态或分类。

- [Tag 标签](/pr-preview/pr-181/components/yue/core/tag.md)：展示分类并通过有名称的移除按钮删除标签。

- [Avatar 头像](/pr-preview/pr-181/components/yue/core/avatar.md)：显示头像或后备文字，提供对象名称。

- [Skeleton 骨架屏](/pr-preview/pr-181/components/yue/core/skeleton.md)：在内容载入时展示布局占位。

- [Spinner 加载指示](/pr-preview/pr-181/components/yue/core/spinner.md)：表达正在处理的状态，并提供读屏名称。

- [Progress 进度](/pr-preview/pr-181/components/yue/core/progress.md)：显示已知比例的任务进度。

- [Alert 提示信息](/pr-preview/pr-181/components/yue/core/alert.md)：用语义色和文本展示需要用户注意的信息。

- [Toast 短消息](/pr-preview/pr-181/components/yue/core/toast.md)：通过共享消息队列显示短时反馈并允许关闭。

- [Notification 通知](/pr-preview/pr-181/components/yue/core/notification.md)：通过共享消息队列展示较完整的通知内容。

- [Empty 空状态](/pr-preview/pr-181/components/yue/core/empty.md)：解释暂无内容的原因并提供后续操作。

- [Result 结果页](/pr-preview/pr-181/components/yue/core/result.md)：展示操作结果及下一步入口。

- [Breadcrumb 面包屑](/pr-preview/pr-181/components/yue/core/breadcrumb.md)：展示当前位置与上级路径。

- [Pagination 分页](/pr-preview/pr-181/components/yue/core/pagination.md)：在结果页之间切换，并约束首尾边界。

- [BackTop 返回顶部](/pr-preview/pr-181/components/yue/core/backtop.md)：在长内容中快速回到顶部。

- [FullScreen 全屏](/pr-preview/pr-181/components/yue/core/fullscreen.md)：调用浏览器全屏 API，并向宿主报告失败。

- [Icon 图标](/pr-preview/pr-181/components/yue/core/icon.md)：通过统一入口使用图标并区分装饰与语义用途。

- [Descriptions 描述列表](/pr-preview/pr-181/components/yue/core/descriptions.md)：展示多项字段名称与对应内容。

- [设计令牌](/pr-preview/pr-181/components/yue/tokens.md)

- [SchemaForm 表单与弹层](/pr-preview/pr-181/components/yue/form.md)

- [数据组件](/pr-preview/pr-181/components/yue/data.md)
