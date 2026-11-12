> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Combobox 可搜索选择

在弹层内部搜索并选择选项。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 安装

```bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
```

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/pr-preview/pr-202/components/yue/index.md)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

```tsx
import { useId, useState } from 'react';
import * as C from '@idadawn/yue-core';

interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }

export default function Example({ disabled, rootProps }: DemoProps) { return <C.Combobox items={['北京', '上海', '杭州']} disabled={disabled} {...rootProps}><C.ComboboxTrigger aria-label="选择城市"><C.ComboboxValue placeholder="选择城市"/></C.ComboboxTrigger><C.ComboboxContent aria-label="城市选项"><C.ComboboxInput aria-label="搜索城市"/><C.ComboboxList>{(city: string) => <C.ComboboxItem value={city} key={city}>{city}</C.ComboboxItem>}</C.ComboboxList></C.ComboboxContent></C.Combobox>; }
```

## 组合结构

- `Combobox`
- `ComboboxInput`
- `ComboboxContent`
- `ComboboxList`
- `ComboboxEmpty`
- `ComboboxItem`
- `ComboboxTrigger`
- `ComboboxValue`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Combobox

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| form | `string \| undefined` | 否 | 未指定 | Identifies the form that owns the internal input. Useful when the combobox is rendered outside the form. |
| filter | `((item: Value, query: string, itemToString?: ((item: Value) => string) \| undefined) => boolean) \| null \| undefined` | 否 | 未指定 | Filter function used to match items vs input query. Receives the source item, which is the derived value's item when \`items\` is a \`createItems()\` collection, and the item itself otherwise. |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| name | `string \| undefined` | 否 | 未指定 | Identifies the field when a form is submitted. |
| id | `string \| undefined` | 否 | 未指定 | The id of the component. |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| items | `readonly any[] \| readonly Group<any>[] \| ComboboxItemCollection<Value, Value> \| undefined` | 否 | 未指定 | The items to be displayed in the list. Can be a flat array of items, an array of groups with items, or a collection created by the \`createItems()\` function, which derives each item's selection value and label. Nullish entries are not supported: remove them from the data before passing it. |
| required | `boolean \| undefined` | 否 | false | Whether the user must choose a value before submitting a form. |
| grid | `boolean \| undefined` | 否 | false | Whether list items are presented in a grid layout. When enabled, arrow keys navigate across rows and columns inferred from DOM rows. |
| inline | `boolean \| undefined` | 否 | false | Whether the list is rendered inline without using the component's own popup.  Specify \`open\` unconditionally in conjunction with this prop so the list is considered visible: \`\<Combobox.Root inline open>\`  In a \`Combobox.Root\` > \`Dialog.Root\` composition, bind the Combobox's \`open\` and \`onOpenChange\` props to the \`Dialog\`'s \`open\` and \`onOpenChange\` state instead so the component resets its transient state (filter query, highlighted item, and input value) when the dialog closes. |
| modal | `boolean \| undefined` | 否 | false | Determines if the popup enters a modal state when open. - \`true\`: user interaction is limited to the popup: document page scroll is locked and pointer interactions on outside elements are disabled. - \`false\`: user interaction with the rest of the document is allowed.  On touch devices, a \`true\` modal blocks outside taps but leaves the page scrollable unless the popup spans nearly the full viewport width, matching native iOS behavior. |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the popup is initially open.  To render a controlled popup, use the \`open\` prop instead. |
| loopFocus | `boolean \| undefined` | 否 | true | Whether to loop keyboard focus back to the input when the end of the list is reached while using the arrow keys. The first item can then be reached by pressing \<kbd>ArrowDown\</kbd> again from the input, or the last item can be reached by pressing \<kbd>ArrowUp\</kbd> from the input. The input is always included in the focus loop per [ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/patterns/combobox/). When disabled, focus does not move when on the last element and the user presses \<kbd>ArrowDown\</kbd>, or when on the first element and the user presses \<kbd>ArrowUp\</kbd>. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the popup is opened or closed. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the popup is currently open. Use when controlled. |
| readOnly | `boolean \| undefined` | 否 | false | Whether the user should be unable to choose a different option from the popup. |
| openOnInputClick | `boolean \| undefined` | 否 | true | Whether the popup opens when clicking the input. |
| inputValue | `string \| number \| readonly string[] \| undefined` | 否 | 未指定 | The input value of the combobox. Use when controlled. |
| defaultInputValue | `string \| number \| readonly string[] \| undefined` | 否 | 未指定 | The uncontrolled input value when initially rendered.  To render a controlled input, use the \`inputValue\` prop instead. |
| inputRef | `React.Ref<HTMLInputElement> \| undefined` | 否 | 未指定 | A ref to the hidden input element. |
| filteredItems | `readonly Value[] \| readonly Group<Value>[] \| undefined` | 否 | 未指定 | Filtered items to display in the list. When provided, the list uses these items instead of filtering the \`items\` prop internally. When \`items\` is also provided, this array must preserve its flat or grouped structure. With a \`createItems()\` collection, pass source items rather than derived values. Nullish entries are not supported, as in \`items\`. Use when you want to control filtering logic externally with the \`useFilter()\` hook. |
| virtualized | `boolean \| undefined` | 否 | false | Whether the items are being externally virtualized. |
| limit | `number \| undefined` | 否 | -1 | The maximum number of items to display in the list. |
| locale | `Intl.LocalesArgument` | 否 | 未指定 | The locale to use for string comparison. Defaults to the user's runtime locale. |
| multiple | `Multiple \| undefined` | 否 | false | Whether multiple items can be selected. |
| autoComplete | `string \| undefined` | 否 | 未指定 | Provides a hint to the browser for autofill. |
| autoHighlight | `boolean \| undefined` | 否 | false | Whether the first matching item is highlighted automatically while filtering. |
| highlightItemOnHover | `boolean \| undefined` | 否 | true | Whether moving the pointer over items should highlight them. Disabling this prop allows CSS \`:hover\` to be differentiated from the \`:focus\` (\`data-highlighted\`) state. |
| itemToStringLabel | `((itemValue: Value) => string) \| undefined` | 否 | 未指定 | When the item values are objects (\`\<Combobox.Item value=\{object}>\`), this function converts the object value to a string representation for display in the input. If the shape of the object is \`\{ value, label }\`, the label will be used automatically without needing to specify this prop. With a \`createItems()\` collection, this receives the derived value, and the collection's \`getLabel\` takes precedence for values it can resolve. |
| itemToStringValue | `((itemValue: Value) => string) \| undefined` | 否 | 未指定 | When the item values are objects (\`\<Combobox.Item value=\{object}>\`), this function converts the object value to a string representation for form submission. If the shape of the object is \`\{ value, label }\`, the value will be used automatically without needing to specify this prop. With a \`createItems()\` collection, this receives the derived value. |
| isItemEqualToValue | `((itemValue: Value, value: Value) => boolean) \| undefined` | 否 | 未指定 | Custom comparison logic used to determine if a combobox item value matches the current selected value. Useful when item values are objects without matching referentially. With a \`createItems()\` collection, both arguments are derived values. Defaults to \`Object.is\` comparison. |
| defaultValue | `ComboboxValueType<Value, Multiple> \| null \| undefined` | 否 | 未指定 | The uncontrolled selected value of the combobox when it's initially rendered.  To render a controlled combobox, use the \`value\` prop instead. |
| actionsRef | `React.RefObject<AriaCombobox.Actions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the combobox. Call this after any externally controlled closing animation finishes. |
| onOpenChange | `((open: boolean, eventDetails: ComboboxRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the popup is opened or closed. |
| onInputValueChange | `((inputValue: string, eventDetails: ComboboxRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the input value changes. |
| onItemHighlighted | `((highlightedValue: Value \| undefined, eventDetails: ComboboxRoot.HighlightEventDetails) => void) \| undefined` | 否 | 未指定 | Callback fired when an item is highlighted or unhighlighted. Receives the highlighted item value (or \`undefined\` if no item is highlighted) and event details with a \`reason\` property describing why the highlight changed. The \`reason\` can be: - \`'keyboard'\`: the highlight changed due to keyboard navigation. - \`'pointer'\`: the highlight changed due to pointer hovering. - \`'none'\`: the highlight changed programmatically. |
| value | `ComboboxValueType<Value, Multiple> \| null \| undefined` | 否 | 未指定 | The selected value of the combobox. Use when controlled. |
| onValueChange | `((value: ComboboxValueType<Value, Multiple> \| (Multiple extends true ? never : null), eventDetails: ComboboxRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the selected value of the combobox changes. |

### ComboboxInput

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AutocompleteInputState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLInputElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AutocompleteInputState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（302 项）](/pr-preview/pr-202/components/yue/api/native-904e73d919f9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| side | `import("@base-ui/react/internals/useAnchorPositioning").Side \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| align | `import("@base-ui/react/internals/useAnchorPositioning").Align \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| sideOffset | `number \| undefined` | 否 | 4 | 以 TypeScript 类型约束为准。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AutocompletePopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AutocompletePopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| finalFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => void \| boolean \| HTMLElement \| null) \| undefined` | 否 | 未指定 | Determines the element to focus when the popup is closed.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (trigger or previously focused element). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, or \`false\`/\`undefined\` to do nothing. |
| initialFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((openType: InteractionType) => void \| boolean \| HTMLElement \| null) \| undefined` | 否 | 未指定 | Determines the element to focus when the popup is opened.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (first tabbable element or popup). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, or \`false\`/\`undefined\` to do nothing. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-202/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxList

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AutocompleteListState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `React.ReactNode \| ((item: any, index: number) => React.ReactNode)` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AutocompleteListState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-202/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxEmpty

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AutocompleteEmptyState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AutocompleteEmptyState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-202/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ComboboxItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| value | `any` | 否 | null | A unique value that identifies this item. |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `((event: import("@base-ui/react/types").BaseUIEvent<React.MouseEvent<HTMLDivElement, MouseEvent>>) => void) \| undefined` | 否 | 未指定 | An optional click handler for the item when selected. It fires when clicking the item with the pointer, as well as when pressing \`Enter\` with the keyboard if the item is highlighted when the \`Input\` or \`List\` element has focus. |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ComboboxItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| index | `number \| undefined` | 否 | 未指定 | The index of the item in the list. Improves performance when specified by avoiding the need to calculate the index automatically from the DOM. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/pr-preview/pr-202/components/yue/api/native-e5f13b3cfc4c.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ComboboxTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ComboboxTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/pr-preview/pr-202/components/yue/api/native-8ee7734c6312.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ComboboxValue

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | -------------------------------------------------------------- | -- | --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| children | `React.ReactNode \| ((selectedValue: any) => React.ReactNode)` | 否 | 未指定 | Accepts a function that returns a \`ReactNode\` to format the selected value. Treat the value as read-only: in \`multiple\` mode it may be a shared frozen array when nothing is selected. |
| placeholder | `React.ReactNode` | 否 | 未指定 | The placeholder value to display when no value is selected. This is overridden by \`children\` if specified, or by a null item's label in \`items\`. |

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
