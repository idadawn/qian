> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Field 表单字段

组合标签、描述、输入和校验反馈。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 安装

```bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
```

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/components/yue/index.md)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

```tsx
import { useId, useState } from 'react';
import * as C from '@idadawn/yue-core';

interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }

export default function Example({ disabled, invalid, rootProps }: DemoProps) { const [value, setValue] = useState(''); const [touched, setTouched] = useState(false); const showError = invalid || (touched && !value.trim()); return <C.Field name="name" disabled={disabled} invalid={showError} {...rootProps}><C.FieldLabel>姓名</C.FieldLabel><C.Input required value={value} onChange={event => setValue(event.target.value)} onBlur={() => setTouched(true)}/><C.FieldDescription>使用真实姓名。</C.FieldDescription>{showError && <C.FieldError match>请输入姓名。</C.FieldError>}</C.Field>; }
```

## 组合结构

- `Field`
- `FieldLabel`
- `FieldDescription`
- `FieldError`
- `FieldControl`
- `FieldItem`

## 状态与交互

预览工具栏可切换默认、暗色、错误场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Field

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldRootState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. Takes precedence over the \`disabled\` prop on the \`\<Field.Control>\` component. |
| name | `string \| undefined` | 否 | 未指定 | Identifies the field when a form is submitted. Takes precedence over the \`name\` prop on the \`\<Field.Control>\` component. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldRootState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| actionsRef | `React.RefObject<FieldRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`validate\`: Validates the field when called. |
| validate | `((value: unknown, formValues: Form.Values) => string \| string[] \| null \| void \| Promise<string \| string[] \| null \| void>) \| undefined` | 否 | 未指定 | A function for custom validation. Return a string or an array of strings with the error message(s) if the value is invalid. Returning nothing, \`null\`, an empty string, or an empty array means the value is valid. Asynchronous functions are supported, but they do not prevent form submission when using \`validationMode="onSubmit"\`. |
| validationMode | `import("../../form/Form.mjs").FormValidationMode \| undefined` | 否 | 'onSubmit' | Determines when the field should be validated. This takes precedence over the \`validationMode\` prop on \`\<Form>\`.  - \`onSubmit\`: triggers validation when the form is submitted, and re-validates on change after submission. - \`onBlur\`: triggers validation when the control loses focus. - \`onChange\`: triggers validation on every change to the control value. |
| validationDebounceTime | `number \| undefined` | 否 | 0 | How long to wait between \`validate\` callbacks if \`validationMode="onChange"\` is used. Specified in milliseconds. |
| invalid | `boolean \| undefined` | 否 | 未指定 | Whether the field is invalid. Useful when the field state is controlled by an external library. |
| dirty | `boolean \| undefined` | 否 | 未指定 | Whether the field's value has been changed from its initial value. Useful when the field state is controlled by an external library. |
| touched | `boolean \| undefined` | 否 | 未指定 | Whether the field has been touched. Useful when the field state is controlled by an external library. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### FieldLabel

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldLabelState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldLabelState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| nativeLabel | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<label>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a label (for example, \`\<div>\`).  This is useful to avoid inheriting label behaviors on \`\<button>\` controls (such as \`\<Select.Trigger>\` and \`\<Combobox.Trigger>\`), including avoiding \`:hover\` on the button when hovering the label, and preventing clicks on the label from firing on the button. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（275 项）](/components/yue/api/native-da6bd73e9e74.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### FieldDescription

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldDescriptionState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLParagraphElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldDescriptionState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-0d20b6be50ee.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### FieldError

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldErrorState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldErrorState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| match | `boolean \| keyof ValidityState \| undefined` | 否 | 未指定 | Determines whether to show the error message according to the field's [ValidityState](https://developer.mozilla.org/en-US/docs/Web/API/ValidityState). Specifying \`true\` will always show the error message, and lets external libraries control the visibility. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### FieldControl

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| defaultValue | `string \| number \| readonly string[] \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldControlState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldControlState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| onValueChange | `((value: string, eventDetails: FieldControl.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Callback fired when the \`value\` changes. Use when controlled. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（302 项）](/components/yue/api/native-904e73d919f9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### FieldItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").FieldItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the wrapped control should ignore user interaction. The \`disabled\` prop on \`\<Field.Root>\` takes precedence over this. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").FieldItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
