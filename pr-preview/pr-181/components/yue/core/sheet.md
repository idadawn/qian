> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Sheet 侧栏弹层

从侧边展示筛选或补充信息。


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

export default function Example({ rootProps }: DemoProps) { return <C.Sheet {...rootProps}><C.SheetTrigger>打开侧栏</C.SheetTrigger><C.SheetContent><C.SheetTitle>筛选条件</C.SheetTitle><C.SheetDescription>设置查询条件。</C.SheetDescription><C.Input aria-label="关键词"/><C.SheetClose>关闭</C.SheetClose></C.SheetContent></C.Sheet>; }
```

## 组合结构

- `Sheet`
- `SheetTrigger`
- `SheetContent`
- `SheetTitle`
- `SheetDescription`
- `SheetClose`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Sheet

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------------------- | ------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the dialog is currently open. |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the dialog is initially open.  To render a controlled dialog, use the \`open\` prop instead. |
| modal | `boolean \| "trap-focus" \| undefined` | 否 | true | Determines if the dialog enters a modal state when open. - \`true\`: user interaction is limited to just the dialog: focus is trapped, document page scroll is locked, and pointer interactions on outside elements are disabled. - \`false\`: user interaction with the rest of the document is allowed. - \`'trap-focus'\`: focus is trapped inside the dialog, but document page scroll is not locked and pointer interactions outside of it remain enabled.  When \`modal\` is \`true\` or \`'trap-focus'\`, render \`\<Dialog.Close>\` inside \`\<Dialog.Popup>\` so touch screen readers can escape the popup. |
| onOpenChange | `((open: boolean, eventDetails: DialogRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the dialog is opened or closed. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the dialog is opened or closed. |
| disablePointerDismissal | `boolean \| undefined` | 否 | false | Whether to prevent the dialog from closing on outside presses. For non-modal dialogs, this also prevents the dialog from closing when focus moves outside of it. |
| actionsRef | `React.RefObject<DialogRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the dialog. Call this after any externally controlled closing animation finishes. - \`close\`: Closes the dialog imperatively when called. |
| handle | `DialogHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the dialog with a trigger. If specified, allows external triggers to control the dialog's open state. Can be created with the Dialog.createHandle() method. |
| children | `React.ReactNode \| PayloadChildRenderFunction<unknown>` | 否 | 未指定 | The content of the dialog. This can be a regular React node or a render function that receives the \`payload\` of the active trigger. |
| triggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the dialog is associated with. This is useful in conjunction with the \`open\` prop to create a controlled dialog. There's no need to specify this prop when the dialog is uncontrolled (that is, when the \`open\` prop is not set). |
| defaultTriggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the dialog is associated with. This is useful in conjunction with the \`defaultOpen\` prop to create an initially open dialog. |

### SheetTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").DialogTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `((((instance: HTMLButtonElement \| null) => void \| (() => VoidOrUndefinedOnly) \| DO_NOT_USE_OR_YOU_WILL_BE_FIRED_CALLBACK_REF_RETURN_VALUES[keyof DO_NOT_USE_OR_YOU_WILL_BE_FIRED_CALLBACK_REF_RETURN_VALUES]) \| RefObject<HTMLButtonElement \| null>) & (RefObject<HTMLElement \| null> \| ((instance: HTMLElement \| null) => void \| (() => VoidOrUndefinedOnly) \| DO_NOT_USE_OR_YOU_WILL_BE_FIRED_CALLBACK_REF_RETURN_VALUES[keyof DO_NOT_USE_OR_YOU_WILL_BE_FIRED_CALLBACK_REF_RETURN_VALUES]))) \| null \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| id | `string \| undefined` | 否 | 未指定 | ID of the trigger. In addition to being forwarded to the rendered element, it is also used to specify the active trigger for the dialog in controlled mode (with the DialogRoot \`triggerId\` prop). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| handle | `DialogHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the trigger with a dialog. Can be created with the Dialog.createHandle() method. |
| payload | `unknown` | 否 | 未指定 | A payload to pass to the dialog when it is opened. |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").DialogTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（281 项）](/pr-preview/pr-181/components/yue/api/native-264dad10c4e5.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SheetContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AlertDialogPopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AlertDialogPopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| finalFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => boolean \| HTMLElement \| null \| void) \| undefined` | 否 | 未指定 | Determines the element to focus when the dialog is closed.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (trigger or previously focused element). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, \`null\` to fall back to the default behavior, or \`false\`/\`undefined\` to do nothing. |
| initialFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((openType: InteractionType) => boolean \| HTMLElement \| null \| void) \| undefined` | 否 | 未指定 | Determines the element to focus when the dialog is opened. By default, focus moves to the first tabbable element inside the popup, except when the dialog is opened by touch — then the popup itself is focused to avoid opening the virtual keyboard.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (first tabbable element or popup). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, \`null\` to fall back to the default behavior, or \`false\`/\`undefined\` to do nothing. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
| side | `"left" \| "right" \| undefined` | 否 | 'right' | 以 TypeScript 类型约束为准。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SheetTitle

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AlertDialogTitleState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLHeadingElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AlertDialogTitleState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-994ffe62ae8f.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SheetDescription

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AlertDialogDescriptionState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLParagraphElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AlertDialogDescriptionState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-0d20b6be50ee.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### SheetClose

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AlertDialogCloseState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AlertDialogCloseState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/pr-preview/pr-181/components/yue/api/native-8ee7734c6312.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- Tab
- Shift+Tab
- Escape

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
