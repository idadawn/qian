# @idadawn/yue-form

唯一的 SchemaForm、FormApi、弹层和消息 API。S1-YUE-007 按 S1-DEC-024 / R108 在靶场对 SDK mock 验收；真实租户页面归 S1-TEN-005。

## 使用

`SchemaForm` 接收 `schema`、`i18n`、`initialValues` 和 `onSubmit`；从 JSON Schema 2020-12 提取字段类型与约束，以 react-hook-form 管理状态，控件复用 core。字段标签用 `ui.layout[].labelKey` 或数据 Schema 的 title key。原始 Schema 不被联动修改。

`mode="readonly"` 与编辑共用字段目录与状态，`auth.fields` 只做体验裁剪；脱敏支持 none、full、last4，不能替代服务端授权与脱敏。`apiRef` 提供 getValues / setValue / reset / validate / submit / setErrors / clearErrors。切换记录用新的 React key 或 reset，避免覆盖正在编辑的数据。

提交回调可以直接调用 `PlatformClient.api<paths>()`；它抛出的 `PlatformError` 按 RFC 6901 pointer 映射到字段，提交结束后聚焦第一处错误。其他异常显示 i18n 错误文案；onError 可进一步处理 409 刷新提示等页面行为。日期时间输入按本地时间展示，提交使用带时区的 ISO 字符串。

`rules.dependencies` 是基础联动：当 source 等于 equals 时，对 target 设置 required、disabled、visible 或 value。复杂 actions、表达式和脚本留给 S2，本期显式拒绝执行，避免静默产生错误结果。

## 弹层与消息

`useModal(i18n)`、`useDrawer(i18n)` 返回 open(options)、close 与 holder，分别复用 core Dialog 与 Sheet。`usePopup(i18n, triggerElement, options)` 复用 Popover，触发元素合并到单个按钮，不产生嵌套交互元素。options 提供 title、description、content 与 onClose。把 holder 放在 React 树中，所有关闭路径恢复焦点。

`FeedbackProvider` 下，`useMessage()` 提供 info / success / error / notification（参数为文案 key）；`useConfirm()` 接收标题与说明，返回 Promise<boolean>。确认、取消、被新确认替换或 Provider 卸载都有明确结果。Toast / Notification 共用 core 的队列与可访问播报。

## 契约与模型体验

FormSchema / ListSchema / WidgetMeta 的 Schema 和生成类型在 contracts 包。运行 `pnpm --filter @idadawn/yue-contracts contracts:generate` 更新，`contracts:check` 验证。SDK 的 WidgetMeta 同源。FormSchema 带 ui.device 与 auth，ListSchema 带 overrides 与 auth；模型生成的元数据先经 validateMetadata 验证，再渲染。`exportRuleCatalog()` 导出校验和脱敏规则的 id 与参数 Schema。

## 文档与验证

权威交互说明是 [docs/index.mdx](docs/index.mdx)，共用示例在 [docs/examples/schema-form-demo.tsx](docs/examples/schema-form-demo.tsx)，状态登记在 docs/scenarios.json。Rspress 收集这些源到组件页，靶场 `/form.html` 用相同示例。运行 `pnpm yue:gates` 验证整体；浏览器测试覆盖字段校验、联动、ErrorDetail、焦点及 FormApi，SSR 测试覆盖元数据和只读，靶场与文档 E2E 覆盖真实交互及 axe。

## 已知限制与延期项

S1 支持字符串、数字、布尔、枚举、日期与嵌套对象；数组编辑器、复杂表达式、端布局覆盖执行和低代码设计器留给 S2。参考应用 CRUD 归 S1-QA-007；本任务的网络证据是 SDK mock，不代表真实后端验收。

## 字段与弹层页脚

文本等字段标签在上，间距统一为 `gap-2`；布尔字段的复选框与标签同行，描述和错误放在标签下方。必填标记、说明和错误使用语义颜色。只读内容继续由相同 SchemaForm 字段目录输出，并采用同一字体与间距。

`useModal` / `useDrawer` / `usePopup` 中的编辑表单自动共享一个页脚，取消在左、保存或确认在右；取消不提交并恢复原触发器焦点。只读详情保留关闭操作。直接使用 SchemaForm 时，宿主仍可通过 `children` 放置附加动作，公开 API 与提交协议不变。
