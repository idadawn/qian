> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Accordion 折叠面板

按需展开问题说明或分组内容。


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

export default function Example({ disabled, rootProps }: DemoProps) { return <C.Accordion disabled={disabled} {...rootProps}><C.AccordionItem value="one"><C.AccordionHeader><C.AccordionTrigger>如何使用</C.AccordionTrigger></C.AccordionHeader><C.AccordionContent>组件可以通过 props 配置。</C.AccordionContent></C.AccordionItem><C.AccordionItem value="two"><C.AccordionHeader><C.AccordionTrigger>如何扩展</C.AccordionTrigger></C.AccordionHeader><C.AccordionContent>使用插槽组合组件。</C.AccordionContent></C.AccordionItem></C.Accordion>; }
```

## 组合结构

- `Accordion`
- `AccordionItem`
- `AccordionHeader`
- `AccordionTrigger`
- `AccordionContent`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Accordion

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| defaultValue | `AccordionValue<unknown> \| undefined` | 否 | 未指定 | The uncontrolled value of the item(s) that should be initially expanded.  To render a controlled accordion, use the \`value\` prop instead. |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AccordionRoot.State<unknown>) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| value | `AccordionValue<unknown> \| undefined` | 否 | 未指定 | The controlled value of the item(s) that should be expanded.  To render an uncontrolled accordion, use the \`defaultValue\` prop instead. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AccordionRoot.State<unknown>> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| orientation | `Orientation \| undefined` | 否 | 'vertical' | Deprecated following the [APG guidance update](https://github.com/w3c/aria-practices/pull/3434) to remove roving focus.  This prop no longer affects keyboard focus behavior. |
| loopFocus | `boolean \| undefined` | 否 | 未指定 | Deprecated following the [APG guidance update](https://github.com/w3c/aria-practices/pull/3434) to remove roving focus.  This prop no longer affects keyboard focus behavior. |
| multiple | `boolean \| undefined` | 否 | false | Whether multiple items can be open at the same time. |
| onValueChange | `((value: AccordionValue<unknown>, eventDetails: AccordionRootChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when an accordion item is expanded or collapsed. Provides the new value as an argument. |
| keepMounted | `boolean \| undefined` | 否 | false | Whether to keep the element in the DOM while the panel is closed. This prop is ignored when \`hiddenUntilFound\` is used. |
| hiddenUntilFound | `boolean \| undefined` | 否 | false | Allows the browser's built-in page search to find and expand the panel contents.  Overrides the \`keepMounted\` prop and uses \`hidden="until-found"\` to hide the element without removing it from the DOM. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### AccordionItem

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AccordionItemState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| value | `any` | 否 | 未指定 | A unique value that identifies this accordion item. If no value is provided, a unique ID will be generated automatically. Use when controlling the accordion programmatically, or to set an initial open state. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AccordionItemState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| onOpenChange | `((open: boolean, eventDetails: AccordionItem.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the panel is opened or closed. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### AccordionHeader

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AccordionHeaderState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLHeadingElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AccordionHeaderState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-28007fc0a502.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### AccordionTrigger

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AccordionTriggerState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | true | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`false\` if the rendered element is not a button (for example, \`\<div>\`). |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AccordionTriggerState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（282 项）](/pr-preview/pr-122/components/yue/api/native-850dbb022e07.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### AccordionContent

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").AccordionPanelState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").AccordionPanelState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| keepMounted | `boolean \| undefined` | 否 | false | Whether to keep the element in the DOM while the panel is closed. This prop is ignored when \`hiddenUntilFound\` is used. |
| hiddenUntilFound | `boolean \| undefined` | 否 | false | Allows the browser's built-in page search to find and expand the panel contents.  Overrides the \`keepMounted\` prop and uses \`hidden="until-found"\` to hide the element without removing it from the DOM. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（273 项）](/pr-preview/pr-122/components/yue/api/native-11e3e0a70cc9.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
