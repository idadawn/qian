> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# ContextMenu 右键菜单

通过右键或菜单快捷键打开上下文操作。


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

export default function Example({ rootProps }: DemoProps) { const [action, setAction] = useState("等待右键操作"); return <div><C.ContextMenu {...rootProps}><C.ContextMenuTrigger className="rounded border p-6">右键或按菜单快捷键</C.ContextMenuTrigger><C.ContextMenuContent><C.ContextMenuItem onClick={() => setAction("已复制示例")}>复制</C.ContextMenuItem><C.ContextMenuSeparator /><C.ContextMenuItem onClick={() => setAction("已粘贴示例")}>粘贴</C.ContextMenuItem></C.ContextMenuContent></C.ContextMenu><output aria-live="polite">{action}</output></div>; }
```

## 组合结构

- `ContextMenu`
- `ContextMenuTrigger`
- `ContextMenuContent`
- `ContextMenuItem`
- `ContextMenuSeparator`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### ContextMenu

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| -------------------- | ------------------------------------------------------------------------------------------ | -- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| onOpenChange | `((open: boolean, eventDetails: ContextMenuRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the menu is opened or closed. |
| closeParentOnEsc | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| orientation | `MenuRootOrientation \| undefined` | 否 | 'vertical' | The visual orientation of the menu. Controls whether roving focus uses up/down or left/right arrow keys. |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the menu is initially open.  To render a controlled menu, use the \`open\` prop instead. |
| loopFocus | `boolean \| undefined` | 否 | true | Whether to loop keyboard focus back to the first item when the end of the list is reached while using the arrow keys. |
| highlightItemOnHover | `boolean \| undefined` | 否 | true | Whether moving the pointer over items should highlight them. Disabling this prop allows CSS \`:hover\` to be differentiated from the \`:focus\` (\`data-highlighted\`) state. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the menu is opened or closed. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the menu is currently open. |
| actionsRef | `React.RefObject<MenuRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Manually unmounts the menu.   Call this after any externally controlled closing animation finishes. - \`close\`: When specified, the menu can be closed imperatively. |

### ContextMenuTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-d661887b693b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ContextMenuContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuPopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuPopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| finalFocus | `boolean \| React.RefObject<HTMLElement \| null> \| ((closeType: InteractionType) => boolean \| HTMLElement \| null \| void) \| undefined` | 否 | 未指定 | Determines the element to focus when the menu is closed.  - \`false\`: Do not move focus. - \`true\`: Move focus based on the default behavior (trigger or previously focused element). - \`RefObject\`: Move focus to the ref element. - \`function\`: Called with the interaction type (\`mouse\`, \`touch\`, \`pen\`, or \`keyboard\`).   Return an element to focus, \`true\` to use the default behavior, or \`false\`/\`undefined\` to do nothing. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（272 项）](/components/yue/api/native-7eb9709611ee.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ContextMenuItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").ContextMenuItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| label | `string \| undefined` | 否 | 未指定 | Overrides the text label to use when the item is matched during keyboard text navigation. |
| ref | `Ref<HTMLElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `((event: import("@base-ui/react/types").BaseUIEvent<React.MouseEvent<HTMLDivElement, MouseEvent>>) => void) \| undefined` | 否 | 未指定 | The click handler for the menu item. |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").ContextMenuItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| closeOnClick | `boolean \| undefined` | 否 | true | Whether to close the menu when the item is clicked. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/components/yue/api/native-e5f13b3cfc4c.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ContextMenuSeparator

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").SeparatorState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").SeparatorState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| orientation | `Orientation \| undefined` | 否 | 'horizontal' | The orientation of the separator. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/components/yue/api/native-4133715c817b.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

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
