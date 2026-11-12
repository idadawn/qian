> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Tabs 标签页

在多个内容面板之间切换，并维护焦点语义。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 安装

```bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
```

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/pr-preview/pr-122/components/yue/index.md)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

```tsx
import { useId, useState } from 'react';
import * as C from '@idadawn/yue-core';

interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }

export default function Example({ rootProps }: DemoProps) { return <C.Tabs defaultValue="overview" {...rootProps}><C.TabsList aria-label="内容分类"><C.TabsTab value="overview">概览</C.TabsTab><C.TabsTab value="settings">设置</C.TabsTab></C.TabsList><C.TabsPanel value="overview">概览内容</C.TabsPanel><C.TabsPanel value="settings">设置内容</C.TabsPanel></C.Tabs>; }
```

## 组合结构

- `Tabs`
- `TabsList`
- `TabsTab`
- `TabsPanel`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Tabs

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| defaultValue | `any` | 否 | 0 | The default value. Use when the component is not controlled. When the value is \`null\`, no Tab will be active. |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TabsRootState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| value | `any` | 否 | 未指定 | The value of the currently active \`Tab\`. Use when the component is controlled. When the value is \`null\`, no Tab will be active. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TabsRootState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| orientation | `BaseOrientation \| undefined` | 否 | 'horizontal' | The component orientation (layout flow direction). |
| onValueChange | `((value: TabsTab.Value, eventDetails: TabsRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Callback invoked when new value is being set.  The event \`reason\` is \`'none'\` for user-initiated changes, such as a click or keyboard navigation; \`'initial'\` for the first automatic selection or fallback in uncontrolled roots when \`defaultValue\` is omitted or \`undefined\`, including when the implicit initial value is disabled or missing; \`'disabled'\` for automatic fallback when the selected tab becomes disabled in uncontrolled roots; or \`'missing'\` for automatic fallback when the selected tab is removed, or when an explicit \`defaultValue\` never matches a mounted tab in uncontrolled roots.  For automatic changes, the selected value can be \`null\` when no enabled Tab is available as a fallback.  Automatic changes cannot be canceled; calling \`eventDetails.cancel()\` for \`'initial'\`, \`'disabled'\`, or \`'missing'\` has no effect. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### TabsList

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TabsListState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TabsListState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| loopFocus | `boolean \| undefined` | 否 | true | Whether to loop keyboard focus back to the first item when the end of the list is reached while using the arrow keys. |
| activateOnFocus | `boolean \| undefined` | 否 | true | Whether to automatically change the active tab on arrow key focus. Otherwise, tabs will be activated using \<kbd>Enter\</kbd> or \<kbd>Space\</kbd> key press. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### TabsTab

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TabsTabState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLButtonElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | Whether the Tab is disabled.  If a first Tab on a \`\<Tabs.List>\` is disabled, it won't initially be selected. Instead, the next enabled Tab will be selected. However, it does not work like this during server-side rendering, as it is not known during pre-rendering which Tabs are disabled. To work around it, ensure that \`defaultValue\` or \`value\` on \`\<Tabs.Root>\` is set to an enabled Tab's value. |
| value | `any` | 是 | 未指定 | The value of the Tab. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TabsTabState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（281 项）](/pr-preview/pr-122/components/yue/api/native-ecb207c60294.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### TabsPanel

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").TabsPanelState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| value | `any` | 是 | 未指定 | The value of the TabPanel. It will be shown when the Tab with the corresponding value is active. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").TabsPanelState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| keepMounted | `boolean \| undefined` | 否 | false | Whether to keep the HTML element in the DOM while the panel is hidden. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- ArrowLeft
- ArrowRight
- Home
- End

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
