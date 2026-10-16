> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Tooltip 提示

通过悬停或键盘焦点显示补充说明。


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

export default function Example({ rootProps }: DemoProps) { return <C.TooltipProvider delay={0}><C.Tooltip {...rootProps}><C.TooltipTrigger>查看提示</C.TooltipTrigger><C.TooltipContent>保存当前更改</C.TooltipContent></C.Tooltip></C.TooltipProvider>; }
```

## 组合结构

- `TooltipProvider`
- `Tooltip`
- `TooltipTrigger`
- `TooltipContent`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### TooltipProvider

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------- | --------------------- | -- | --- | --------------------------------------------------------------------------------------------------------------------- |
| children | `React.ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| delay | `number \| undefined` | 否 | 300 | How long to wait before opening the tooltip on hover. Specified in milliseconds. |
| closeDelay | `number \| undefined` | 否 | 未指定 | How long to wait before closing a tooltip. Specified in milliseconds. |
| timeout | `number \| undefined` | 否 | 400 | Another tooltip will open instantly if the previous tooltip is closed within this timeout. Specified in milliseconds. |

### Tooltip

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------------------- | -------------------------------------------------------------------------------------- | -- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| defaultOpen | `boolean \| undefined` | 否 | false | Whether the tooltip is initially open.  To render a controlled tooltip, use the \`open\` prop instead. |
| open | `boolean \| undefined` | 否 | 未指定 | Whether the tooltip is currently open. |
| onOpenChange | `((open: boolean, eventDetails: TooltipRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the tooltip is opened or closed. |
| onOpenChangeComplete | `((open: boolean) => void) \| undefined` | 否 | 未指定 | Event handler called after any animations complete when the tooltip is opened or closed. |
| disableHoverablePopup | `boolean \| undefined` | 否 | false | Whether the tooltip contents can be hovered without closing the tooltip. |
| trackCursorAxis | `"none" \| "both" \| "x" \| "y" \| undefined` | 否 | 'none' | Determines which axis the tooltip should track the cursor on. |
| actionsRef | `React.RefObject<TooltipRootActions \| null> \| undefined` | 否 | 未指定 | A ref to imperative actions. - \`unmount\`: Unmounts the tooltip popup. - \`close\`: Closes the tooltip imperatively when called. |
| disabled | `boolean \| undefined` | 否 | false | Whether the tooltip is disabled. |
| handle | `TooltipHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the tooltip with a trigger. If specified, allows external triggers to control the tooltip's open state. Can be created with the Tooltip.createHandle() method. |
| children | `React.ReactNode \| PayloadChildRenderFunction<unknown>` | 否 | 未指定 | The content of the tooltip. This can be a regular React node or a render function that receives the \`payload\` of the active trigger. |
| triggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the tooltip is associated with. This is useful in conjunction with the \`open\` prop to create a controlled tooltip. There's no need to specify this prop when the tooltip is uncontrolled (that is, when the \`open\` prop is not set). |
| defaultTriggerId | `string \| null \| undefined` | 否 | 未指定 | ID of the trigger that the tooltip is associated with. This is useful in conjunction with the \`defaultOpen\` prop to create an initially open tooltip. |

### TooltipTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TooltipTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | If \`true\`, the tooltip will not open when interacting with this trigger. Note that this doesn't apply the \`disabled\` attribute to the trigger element. If you want to disable the trigger element itself, you can pass the \`disabled\` prop to the trigger element via the \`render\` prop. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| handle | `TooltipHandle<unknown> \| undefined` | 否 | 未指定 | A handle to associate the trigger with a tooltip. |
| payload | `unknown` | 否 | 未指定 | A payload to pass to the tooltip when it is opened. |
| delay | `number \| undefined` | 否 | 600 | How long to wait before opening the tooltip on hover. Specified in milliseconds. |
| closeDelay | `number \| undefined` | 否 | 0 | How long to wait before closing the tooltip. Specified in milliseconds. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TooltipTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| closeOnClick | `boolean \| undefined` | 否 | true | Whether the tooltip should close when this trigger is clicked. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/components/yue/api/native-8ee7734c6312.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### TooltipContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| side | `import("@base-ui/react/internals/useAnchorPositioning").Side \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| align | `import("@base-ui/react/internals/useAnchorPositioning").Align \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| sideOffset | `number \| undefined` | 否 | 6 | 以 TypeScript 类型约束为准。 |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TooltipPopupState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TooltipPopupState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
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
