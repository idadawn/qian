> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Resizable 可调整面板

通过拖动或键盘调整相邻面板的宽度。


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

export default function Example({ rootProps }: DemoProps) { return <C.Resizable className="h-32 w-full min-w-80 max-w-xl rounded border" {...rootProps}><C.ResizablePanel defaultSize="50%" minSize="20%"><div className="p-4">左侧内容</div></C.ResizablePanel><C.ResizableHandle label="调整面板宽度"/><C.ResizablePanel minSize="20%"><div className="p-4">右侧内容</div></C.ResizablePanel></C.Resizable>; }
```

## 组合结构

- `Resizable`
- `ResizablePanel`
- `ResizableHandle`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Resizable

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----- | ---------------------------- | -- | --- | --------------- |
| style | `CSSProperties \| undefined` | 否 | 未指定 | CSS properties. |

⚠️ The default inline styles cannot be overridden, except for [\`overflow\`](https://react-resizable-panels.vercel.app/examples/overflow). |
\| disabled | `boolean \| undefined` | 否 | 未指定 | Disable resize functionality. |
\| id | `string \| undefined` | 否 | 未指定 | Uniquely identifies this group within an application.Falls back to \`useId\` when not provided.

ℹ️ This value will also be assigned to the \`id\` and \`data-testid\` attributes; (an explicit \`data-testid\` prop will take precedence). |
\| children | `ReactNode` | 否 | 未指定 | Panel and Separator components that comprise this group. |
\| orientation | `"horizontal" \| "vertical" \| undefined` | 否 | 未指定 | Specifies the resizable orientation ("horizontal" or "vertical"); defaults to "horizontal" |
\| defaultLayout | `Layout \| undefined` | 否 | 未指定 | Default layout for the Group.

ℹ️ This value allows layouts to be remembered between page reloads.

⚠️ Slight layout shift may occur when server-rendering panels with percentage-based default sizes.Refer to the documentation for suggestions on how to minimize the impact of this. |
\| disableCursor | `boolean \| undefined` | 否 | 未指定 | This library sets custom mouse cursor styles to indicate drag state.Use this prop to disable that behavior for Panels and Separators in this group. |
\| elementRef | `Ref<HTMLDivElement \| null> \| undefined` | 否 | 未指定 | Ref attached to the root \`HTMLDivElement\`. |
\| groupRef | `Ref<GroupImperativeHandle \| null> \| undefined` | 否 | 未指定 | Exposes the following imperative API:

- \`getLayout(): Layout\`
- \`setLayout(layout: Layout): Layout\`

ℹ️ The \`useGroupRef\` and \`useGroupCallbackRef\` hooks are exported for convenience use in TypeScript projects. |
\| onLayoutChange | `((layout: Layout) => void \| undefined) \| undefined` | 否 | 未指定 | Called when the Group's layout is changing.

⚠️ For layout changes caused by pointer events, this method is called each time the pointer is moved.For most cases, it is recommended to use the \`onLayoutChanged\` callback instead. |
\| onLayoutChanged | `((layout: Layout, meta: LayoutChangedMeta) => void) \| undefined` | 否 | 未指定 | Called after the Group's layout has been changed.

ℹ️ For layout changes caused by pointer events, this method is not called until the pointer has been released.This method is recommended when saving layouts to some storage api.

ℹ️ The second argument contains meta information about the layout change.The \`isUserInteraction\` attribute signals whether the resize was caused by direct user input.It is true for resizes caused by pointer or keyboard inputand false for other triggers (e.g. imperative API calls, initial mount, etc.)The \`requestedLayout\` attribute is the layout before constraints were applied for the current Group size;prefer it when persisting layouts. |
\| resizePreviewMode | `ResizePreviewMode \| undefined` | 否 | 未指定 | Controls whether pointer dragging updates \`Panel\` sizes immediately,or renders overlay separator previews until the pointer is released.

Defaults to \`"panel"\` (immediate resizing); \`"separator"\` defers resizing until release.

Customize previews using the \`SeparatorOverlay\` component. |
\| resizeTargetMinimumSize | `{ coarse: number; fine: number; } \| undefined` | 否 | 未指定 | Minimum size of the resizable hit target area (either \`Separator\` or \`Panel\` edge).This threshold ensures targets are large enough to avoid mis-clicks.

- Coarse inputs (typically a finger on a touchscreen) have reduced accuracy;  to ensure accessibility and ease of use, hit targets should be larger to prevent mis-clicks.
- Fine inputs (typically a mouse) can be smaller

ℹ️ [Apple interface guidelines](https://developer.apple.com/design/human-interface-guidelines/accessibility) suggest \`20pt\` (\`27px\`) on desktops and \`28pt\` (\`37px\`) for touch devices.In practice this seems to be much larger than many of their own applications use though. |
\| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
\| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（274 项）](/components/yue/api/native-7bfdf805c11d.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ResizablePanel

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----- | ---------------------------- | -- | --- | --------------- |
| style | `CSSProperties \| undefined` | 否 | 未指定 | CSS properties. |

⚠️ The default inline styles cannot be overridden, except for [\`overflow\`](https://react-resizable-panels.vercel.app/examples/overflow). |
\| disabled | `boolean \| undefined` | 否 | 未指定 | When disabled, a panel cannot be resized either directly or indirectly (by resizing another panel). |
\| id | `string \| undefined` | 否 | 未指定 | Uniquely identifies this panel within the parent group.Falls back to \`useId\` when not provided.

ℹ️ This prop is used to associate persisted group layouts with the original panel.

ℹ️ This value will also be assigned to the \`id\` and \`data-testid\` attributes; (an explicit \`data-testid\` prop will take precedence). |
\| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
\| elementRef | `Ref<HTMLDivElement \| null> \| undefined` | 否 | 未指定 | Ref attached to the root \`HTMLDivElement\`. |
\| onResize | `((panelSize: PanelSize, id: string \| number \| undefined, prevPanelSize: PanelSize \| undefined) => void) \| undefined` | 否 | 未指定 | Called when panel sizes change. |
\| collapsedSize | `string \| number \| undefined` | 否 | 未指定 | Panel size when collapsed; defaults to 0%. |
\| collapsedThreshold | `string \| number \| undefined` | 否 | 未指定 | Distance a collapsible panel must be resized past its \`minSize\` to collapse,or past its \`collapsedSize\` to expand.Defaults to half the distance between \`collapsedSize\` and \`minSize\`.

For example if a panel declares \`collapsedSize="5%"\`, \`collapsedThreshold="5%"\`, and \`minSize="25%"\`,it will collapse when resized below 20% and expand when resized above 10%.

ℹ️ Interpretation rules:

- Numbers are interpreted as pixels (e.g. \`minSize=\{200}\` is 200 pixels)
- Strings without explicit units are interpreted as percentage (e.g. \`minSize="50"\` is 50 percent)
- Use explicit units (e.g. "px", "%", "em", "rem", "vh", or "vw") to change interpretation |
  \| collapsible | `boolean \| undefined` | 否 | 未指定 | This panel can be collapsed.

ℹ️ A collapsible panel will collapse when its size is less than the specified \`minSize\` |
\| defaultSize | `string \| number \| undefined` | 否 | 未指定 | Default size of Panel within its parent group; default is auto-assigned based on the total number of Panels.

ℹ️ Interpretation rules:

- Numbers are interpreted as pixels (e.g. \`defaultSize=\{200}\` is 200 pixels)
- Strings without explicit units are interpreted as percentage (e.g. \`defaultSize="50"\` is 50 percent)
- Use explicit units (e.g. "px", "%", "em", "rem", "vh", or "vw") to change interpretation

⚠️ Percentage based sizes may cause slight layout shift when server-rendering.For more information see the documentation. |
\| groupResizeBehavior | `"preserve-relative-size" \| "preserve-pixel-size" \| undefined` | 否 | 未指定 | How should this Panel behave if the parent Group is resized?Defaults to \`preserve-relative-size\`.

- \`preserve-relative-size\`: Retain the current relative size (as a percentage of the Group)
- \`preserve-pixel-size\`: Retain its current size (in pixels)

ℹ️ Panel min/max size constraints may impact this behavior.

⚠️ A Group must contain at least one Panel with \`preserve-relative-size\` resize behavior. |
\| maxSize | `string \| number \| undefined` | 否 | 未指定 | Maximum size of Panel within its parent group; defaults to \`"100%"\`.

ℹ️ Interpretation rules:

- Numbers are interpreted as pixels (e.g. \`maxSize=\{200}\` is 200 pixels)
- Strings without explicit units are interpreted as percentage (e.g. \`maxSize="50"\` is 50 percent)
- Use explicit units (e.g. "px", "%", "em", "rem", "vh", or "vw") to change interpretation |
  \| minSize | `string \| number \| undefined` | 否 | 未指定 | Minimum size of Panel within its parent group; defaults to 0%.

ℹ️ Interpretation rules:

- Numbers are interpreted as pixels (e.g. \`minSize=\{200}\` is 200 pixels)
- Strings without explicit units are interpreted as percentage (e.g. \`minSize="50"\` is 50 percent)
- Use explicit units (e.g. "px", "%", "em", "rem", "vh", or "vw") to change interpretation |
  \| panelRef | `Ref<PanelImperativeHandle \| null> \| undefined` | 否 | 未指定 | Exposes the following imperative API:
- \`collapse(): void\`
- \`expand(): void\`
- \`getSize(): PanelSize\`
- \`isCollapsed(): boolean\`
- \`resize(size: number | string): void\`

ℹ️ The \`usePanelRef\` and \`usePanelCallbackRef\` hooks are exported for convenience use in TypeScript projects. |
\| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
\| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（274 项）](/components/yue/api/native-7bfdf805c11d.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

### ResizableHandle

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----- | ---------------------------- | -- | --- | --------------- |
| style | `CSSProperties \| undefined` | 否 | 未指定 | CSS properties. |

ℹ️ Use the \`data-separator\` attribute for custom _hover_ and _active_ styles

⚠️ The following properties cannot be overridden: \`flex-grow\`, \`flex-shrink\` |
\| disabled | `boolean \| undefined` | 否 | 未指定 | When disabled, the separator cannot be used to resize its neighboring panels.

ℹ️ The panels may still be resized indirectly (while other panels are being resized).To prevent a panel from being resized at all, it needs to also be disabled. |
\| id | `string \| undefined` | 否 | 未指定 | Uniquely identifies the separator within the parent group.Falls back to \`useId\` when not provided.

ℹ️ This value will also be assigned to the \`id\` and \`data-testid\` attributes; (an explicit \`data-testid\` prop will take precedence). |
\| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
\| elementRef | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Ref attached to the root \`HTMLDivElement\`. |
\| disableDoubleClick | `boolean \| undefined` | 否 | 未指定 | When true, double-clicking this \`Separator\` will not reset its \`Panel\` to its default size. |
\| preview | `ReactNode` | 否 | 未指定 | Overrides the \`Group\` default preview for this \`Separator\` when \`resizePreviewMode\` is "separator". |
\| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
\| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
\| label | `string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |

[继承的 DOM / ARIA / 事件属性（272 项）](/components/yue/api/native-acdf2f814b3e.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

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
