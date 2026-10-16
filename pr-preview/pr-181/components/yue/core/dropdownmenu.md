> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# DropdownMenu 下拉菜单

以 Menu 的兼容名称组合下拉操作。


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

export default function Example({ rootProps }: DemoProps) { const [action, setAction] = useState("等待菜单操作"); return <div><C.Menu {...rootProps}><C.MenuTrigger>打开菜单</C.MenuTrigger><C.MenuContent><C.MenuItem onClick={() => setAction("正在编辑")}>编辑</C.MenuItem><C.MenuItem disabled>归档</C.MenuItem><C.MenuSeparator /><C.MenuItem onClick={() => setAction("已删除示例")}>删除</C.MenuItem></C.MenuContent></C.Menu><output aria-live="polite">{action}</output></div>; }
```

## 组合结构

- `DropdownMenu`
- `DropdownMenuTrigger`
- `DropdownMenuContent`
- `DropdownMenuItem`
- `DropdownMenuSeparator`
- `DropdownMenuGroup`
- `DropdownMenuGroupLabel`
- `DropdownMenuSub`
- `DropdownMenuSubTrigger`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### DropdownMenu

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| -------------------- | ----------------------------------------------------------------------------------- | -- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the menu is initially open.  To render a controlled menu, use the \`open\` prop instead. |
| loopFocus | `boolean \| undefined` | 否 | true | Whether to loop keyboard focus back to the first item when the end of the list is reached while using the arrow keys. |
| highlightItemOnHover | `boolean \| undefined` | 否 | true | Whether moving the pointer over items should highlight them. Disabling this prop allows CSS \`:hover\` to be differentiated from the \`:focus\` (\`data-highlighted\`) state. |
| modal | `boolean \| undefined` | 否 | true | Determines if the menu enters a modal state when open. - \`true\`: user interaction is limited to the menu: document page scroll is locked and pointer interactions on outside elements are disabled. - \`false\`: user interaction with the rest of the document is allowed.  On touch devices, a \`true\` modal blocks outside taps but leaves the page scrollable unless the popup spans nearly the full viewport width, matching native iOS behavior.  Nested menus ignore this prop, and menus opened by hover are never modal. |
| onOpenChange | `((open: boolean, eventDetails: MenuRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the menu is opened or closed. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the menu is opened or closed. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the menu is currently open. |
| orientation | `MenuRootOrientation \| undefined` | 否 | 'vertical' | The visual orientation of the menu. Controls whether roving focus uses up/down or left/right arrow keys. |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| closeParentOnEsc | `boolean \| undefined` | 否 | false | When in a submenu, determines whether pressing the Escape key closes the entire menu, or only the current child menu. |
| actionsRef | `React.RefObject<MenuRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the menu.   Call this after any externally controlled closing animation finishes. - \`close\`: When specified, the menu can be closed imperatively. |
| triggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the menu is associated with. This is useful in conjunction with the \`open\` prop to create a controlled menu. There's no need to specify this prop when the menu is uncontrolled (that is, when the \`open\` prop is not set). |
| defaultTriggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the menu is associated with. This is useful in conjunction with the \`defaultOpen\` prop to create an initially open menu. |
| handle | `MenuHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the menu with a trigger. If specified, allows external triggers to control the menu's open state. |
| children | `React.ReactNode \| PayloadChildRenderFunction<unknown>` | 否 | 未指定 | The content of the menu. This can be a regular React node or a render function that receives the \`payload\` of the active trigger. |

### DropdownMenuTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").MenuTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| handle | `MenuHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the trigger with a menu. |
| payload | `unknown` | 否 | 未指定 | A payload to pass to the menu when it is opened. |
| delay | `number \| undefined` | 否 | 100 | How long to wait before the menu may be opened on hover. Specified in milliseconds.  Requires the \`openOnHover\` prop. |
| closeDelay | `number \| undefined` | 否 | 0 | How long to wait before closing the menu that was opened on hover. Specified in milliseconds.  Requires the \`openOnHover\` prop. |
| openOnHover | `boolean \| undefined` | 否 | 未指定 | Whether the menu should also open when the trigger is hovered. |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").MenuTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/pr-preview/pr-181/components/yue/api/native-8ee7734c6312.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| side | `import("@base-ui/react/internals/useAnchorPositioning").Side \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| align | `import("@base-ui/react/internals/useAnchorPositioning").Align \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| sideOffset | `number \| undefined` | 否 | 4 | 以 TypeScript 类型约束为准。 |
| alignOffset | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuPopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuPopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| finalFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => boolean \| HTMLElement \| null \| void) \| undefined` | 否 | 未指定 | Determines the element to focus when the menu is closed.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (trigger or previously focused element). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, or \`false\`/\`undefined\` to do nothing. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（272 项）](/pr-preview/pr-181/components/yue/api/native-7eb9709611ee.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| label | `string \| undefined` | 否 | 未指定 | Overrides the text label to use when the item is matched during keyboard text navigation. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `((event: import("@base-ui/react/types").BaseUIEvent<React.MouseEvent<HTMLDivElement, MouseEvent>>) => void) \| undefined` | 否 | 未指定 | The click handler for the menu item. |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| closeOnClick | `boolean \| undefined` | 否 | true | Whether to close the menu when the item is clicked. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/pr-preview/pr-181/components/yue/api/native-e5f13b3cfc4c.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuSeparator

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").SeparatorState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").SeparatorState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| orientation | `Orientation \| undefined` | 否 | 'horizontal' | The orientation of the separator. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuGroup

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuGroupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `React.ReactNode` | 否 | 未指定 | The content of the component. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuGroupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuGroupLabel

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuGroupLabelState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuGroupLabelState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-181/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### DropdownMenuSub

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| -------------------- | ------------------------------------------------------------------------------------------ | -- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| onOpenChange | `((open: boolean, eventDetails: MenuSubmenuRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the menu is opened or closed. |
| closeParentOnEsc | `boolean \| undefined` | 否 | false | When in a submenu, determines whether pressing the Escape key closes the entire menu, or only the current child menu. |
| children | `React.ReactNode` | 否 | 未指定 | The content of the submenu. |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| orientation | `MenuRootOrientation \| undefined` | 否 | 'vertical' | The visual orientation of the menu. Controls whether roving focus uses up/down or left/right arrow keys. |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the menu is initially open.  To render a controlled menu, use the \`open\` prop instead. |
| loopFocus | `boolean \| undefined` | 否 | true | Whether to loop keyboard focus back to the first item when the end of the list is reached while using the arrow keys. |
| highlightItemOnHover | `boolean \| undefined` | 否 | true | Whether moving the pointer over items should highlight them. Disabling this prop allows CSS \`:hover\` to be differentiated from the \`:focus\` (\`data-highlighted\`) state. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the menu is opened or closed. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the menu is currently open. |
| actionsRef | `React.RefObject<MenuRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the menu.   Call this after any externally controlled closing animation finishes. - \`close\`: When specified, the menu can be closed imperatively. |

### DropdownMenuSubTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuSubmenuTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| label | `string \| undefined` | 否 | 未指定 | Overrides the text label to use when the item is matched during keyboard text navigation. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `((event: import("@base-ui/react/types").BaseUIEvent<React.MouseEvent<HTMLDivElement, MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| delay | `number \| undefined` | 否 | 100 | How long to wait before the menu may be opened on hover. Specified in milliseconds.  Requires the \`openOnHover\` prop. |
| closeDelay | `number \| undefined` | 否 | 0 | How long to wait before closing the menu that was opened on hover. Specified in milliseconds.  Requires the \`openOnHover\` prop. |
| openOnHover | `boolean \| undefined` | 否 | true | Whether the menu should also open when the trigger is hovered. |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuSubmenuTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/pr-preview/pr-181/components/yue/api/native-e5f13b3cfc4c.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- ArrowDown
- ArrowUp
- Home
- End
- Enter
- Escape

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
