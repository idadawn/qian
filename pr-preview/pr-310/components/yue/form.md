> For AI agents: the complete documentation index is available at /pr-preview/pr-310/llms.txt, the full documentation bundle is available at /pr-preview/pr-310/llms-full.txt.

# SchemaForm 表单与弹层

SchemaForm 使用 JSON Schema 2020-12 校验数据（解释执行、不在运行期生成代码，只接受 SchemaForm 用到的关键字子集，子集外的关键字在渲染前拒绝），react-hook-form 管理字段状态；编辑与只读共用字段目录。示例请求经 PlatformClient 的 mock 网络层。新建时留空或输入不合法的客户号可看到前置校验；名称填写「服务端错误」可观察 ErrorDetail 的 pointer 映射。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 契约

`schema` 包含 `data / ui / rules / binding` 与 `auth`；`ui.device` 保存端覆盖草案。`rules.dependencies` 支持 source 等于 equals 时设置 target 的 required、disabled、visible 或 value。脚本表达式与复杂 actions 留给 S2，当前渲染显式拒绝非空内容。控件 manifest、FormSchema 与 ListSchema 均有 JSON Schema 与生成类型，未知版本被拒绝。

## 主要属性

| 属性 | 说明 |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| schema | FormSchema 草案；字段类型、必填、长度与格式取自 data |
| i18n | SDK 的多语言实例；标签使用 labelKey 或数据 Schema 的 title key |
| initialValues | 本次表单的初始数据；切换记录用新 key 或 FormApi.reset |
| mode | edit 或 readonly；同一引擎和字段目录 |
| onSubmit | 校验通过后提交；PlatformError 自动定位字段 |
| apiRef | 暴露 getValues、setValue、reset、validate、submit、setErrors、clearErrors |
| className / testId | 样式字符串与稳定测试标识 |
| size / layout / columnWidth | 控件尺寸 md 36px（抽屉内 38px）、lg 40px（申请表单，操作按钮 44px）、xl 44px（认证页）；stack 逐行排列，grid 按可用宽度自动分栏（单栏最小 sm 240px、md 280px），多行文本与 props.span 为 full 的字段独占一行 |
| groupVariant | divided 用分隔线隔开字段组；card 每组一张卡片，组标题可带序号与说明，操作按钮在卡片外 |
| fieldAdornment / fieldExtra | 输入框内的后缀（suffix，如「✓ 可以使用」）、输入框右侧的单位文字（unit，如「人」）与附加按钮（addon，如「重新发送」）；fieldExtra 在输入框下方加附加内容（如密码强度）；后缀、单位与附加内容经 aria-describedby 关联到输入框 |
| submitLabel / inlineActions | 提交按钮文字；抽屉里的子步骤表单用 inlineActions 让操作按钮紧跟表单 |

## 弹层和消息

`useModal / useDrawer / usePopup` 返回 open、close 与 holder，分别使用 core 的 Dialog、Sheet 和 Popover。弹层关闭后恢复触发器焦点。`FeedbackProvider` 下的 `useMessage` 提供消息与通知；`useConfirm` 返回布尔 Promise，取消、替换或卸载均明确结束等待；危险操作传 `tone: 'danger'`（确认按钮为危险色）与 `confirmLabel`（如「冻结」）。`useDrawer` 的说明可传带格式的内容。

## 规则目录与限制

`exportRuleCatalog()` 导出校验与脱敏规则的 id、参数 Schema 和关键字；只读支持 none、full、last4。前端 auth 只做体验裁剪，服务端仍需授权。S1 提供字符串、数字、布尔、枚举、日期与嵌套对象；数组编辑器、表达式 DSL、端布局覆盖执行由后续任务完成。
