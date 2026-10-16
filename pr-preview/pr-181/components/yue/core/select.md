> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Select 下拉选择

从预设选项中选取值，显示对应标签。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 安装

```bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
```

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/pr-preview/pr-181/components/yue/index.md)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

```tsx
import { useId, useState } from 'react';
import * as C from '@idadawn/yue-core';

interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }

export default function Example({ disabled, rootProps }: DemoProps) { return <C.Select items={[{ label: '北京', value: 'beijing' }, { label: '上海', value: 'shanghai' }]} disabled={disabled} {...rootProps}><C.SelectTrigger aria-label="城市" placeholder="选择城市"/><C.SelectContent><C.SelectItem value="beijing">北京</C.SelectItem><C.SelectItem value="shanghai">上海</C.SelectItem></C.SelectContent></C.Select>; }
```

## 组合结构

- `Select`
- `SelectTrigger`
- `SelectContent`
- `SelectItem`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Select

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| inputRef | `React.Ref<HTMLInputElement> \| undefined` | 否 | 未指定 | A ref to access the hidden input element. |
| name | `string \| undefined` | 否 | 未指定 | Identifies the field when a form is submitted. |
| form | `string \| undefined` | 否 | 未指定 | Identifies the form that owns the hidden input. Useful when the select is rendered outside the form. |
| autoComplete | `string \| undefined` | 否 | 未指定 | Provides a hint to the browser for autofill. |
| id | `string \| undefined` | 否 | 未指定 | The id of the Select. |
| required | `boolean \| undefined` | 否 | false | Whether the user must choose a value before submitting a form. |
| readOnly | `boolean \| undefined` | 否 | false | Whether the user should be unable to choose a different option from the select popup. |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| multiple | `Multiple \| undefined` | 否 | false | Whether multiple items can be selected. |
| highlightItemOnHover | `boolean \| undefined` | 否 | true | Whether moving the pointer over items should highlight them. Disabling this prop allows CSS \`:hover\` to be differentiated from the \`:focus\` (\`data-highlighted\`) state. |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the select popup is initially open.  To render a controlled select popup, use the \`open\` prop instead. |
| onOpenChange | `((open: boolean, eventDetails: SelectRootChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the select popup is opened or closed. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the select popup is opened or closed. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the select popup is currently open. |
| modal | `boolean \| undefined` | 否 | true | Determines if the select enters a modal state when open. - \`true\`: user interaction is limited to the select: document page scroll is locked and pointer interactions on outside elements are disabled. - \`false\`: user interaction with the rest of the document is allowed.  On touch devices, a \`true\` modal blocks outside taps but leaves the page scrollable unless the popup spans nearly the full viewport width, matching native iOS behavior. |
| actionsRef | `React.RefObject<SelectRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the select. Call this after any externally controlled closing animation finishes. |
| items | `readonly Group<any>[] \| Record<string, React.ReactNode> \| readonly { label: React.ReactNode; value: any; }[] \| undefined` | 否 | 未指定 | Data structure of the items rendered in the select popup. When specified, \`\<Select.Value>\` renders the label of the selected item instead of the raw value. |
| itemToStringLabel | `((itemValue: Value) => string) \| undefined` | 否 | 未指定 | When the item values are objects (\`\<Select.Item value=\{object}>\`), this function converts the object value to a string representation for display in the trigger. If the shape of the object is \`\{ value, label }\`, the label will be used automatically without needing to specify this prop. |
| itemToStringValue | `((itemValue: Value) => string) \| undefined` | 否 | 未指定 | When the item values are objects (\`\<Select.Item value=\{object}>\`), this function converts the object value to a string representation for form submission. If the shape of the object is \`\{ value, label }\`, the value will be used automatically without needing to specify this prop. |
| isItemEqualToValue | `((itemValue: Value, value: Value) => boolean) \| undefined` | 否 | 未指定 | Custom comparison logic used to determine if a select item value matches the current selected value. Useful when item values are objects without matching referentially. Defaults to \`Object.is\` comparison. |
| defaultValue | `SelectValueType<Value, Multiple> \| null \| undefined` | 否 | 未指定 | The uncontrolled value of the select when it's initially rendered.  To render a controlled select, use the \`value\` prop instead. |
| value | `SelectValueType<Value, Multiple> \| null \| undefined` | 否 | 未指定 | The value of the select. Use when controlled. |
| onValueChange | `((value: SelectValueType<Value, Multiple> \| (Multiple extends true ? never : null), eventDetails: SelectRootChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the value of the select changes. |

### SelectTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| placeholder | `ReactNode` | 否 | 未指定 | 没有值时显示的占位文字。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").SelectTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | Whether the component should ignore user interaction. |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").SelectTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/pr-preview/pr-181/components/yue/api/native-8ee7734c6312.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SelectContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| side | `import("@base-ui/react/internals/useAnchorPositioning").Side \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| align | `import("@base-ui/react/internals/useAnchorPositioning").Align \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| sideOffset | `number \| undefined` | 否 | 4 | 以 TypeScript 类型约束为准。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").SelectPopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").SelectPopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| finalFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => boolean \| HTMLElement \| null \| void) \| undefined` | 否 | 未指定 | Determines the element to focus when the select popup is closed.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (trigger or previously focused element). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, or \`false\`/\`undefined\` to do nothing. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SelectItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").SelectItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| label | `string \| undefined` | 否 | 未指定 | Specifies the text label to use when the item is matched during keyboard text navigation.  Defaults to the item text content if not provided. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| value | `any` | 否 | null | A unique value that identifies this select item. |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").SelectItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（272 项）](/pr-preview/pr-181/components/yue/api/native-7eb9709611ee.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- ArrowDown
- ArrowUp
- Enter
- Escape

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
