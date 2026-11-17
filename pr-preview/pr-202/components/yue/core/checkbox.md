> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Checkbox 复选框

独立勾选选项，支持键盘和受控状态。


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

export default function Example({ disabled, rootProps }: DemoProps) { return <label className="flex items-center gap-2"><C.Checkbox disabled={disabled} {...rootProps}/>允许通知</label>; }
```

## 组合结构

- `Checkbox`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Checkbox

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| defaultChecked | `boolean \| undefined` | 否 | false | Whether the checkbox is initially ticked.  To render a controlled checkbox, use the \`checked\` prop instead. |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").CheckboxRootState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| form | `string \| undefined` | 否 | 未指定 | Identifies the form that owns the hidden input. Useful when the checkbox is rendered outside the form. |
| ref | `Ref<HTMLElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| name | `string \| undefined` | 否 | undefined | Identifies the field when a form is submitted. |
| value | `string \| undefined` | 否 | 未指定 | The checkbox's value. Identifies it within a [Checkbox Group](https://base-ui.com/react/components/checkbox-group), falling back to \`name\` when omitted. When submitting a form, a checked box submits \`value\`; with no \`value\`, it submits the native "on". |
| id | `string \| undefined` | 否 | 未指定 | The id of the input element. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nativeButton | `boolean \| undefined` | 否 | false | Whether the component renders a native \`\<button>\` element when replacing it via the \`render\` prop. Set to \`true\` if the rendered element is a native button. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").CheckboxRootState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| required | `boolean \| undefined` | 否 | false | Whether the user must tick the checkbox before submitting a form. |
| readOnly | `boolean \| undefined` | 否 | false | Whether the user should be unable to tick or untick the checkbox. |
| inputRef | `React.Ref<HTMLInputElement> \| undefined` | 否 | 未指定 | A ref to access the hidden \`\<input>\` element. |
| checked | `boolean \| undefined` | 否 | undefined | Whether the checkbox is currently ticked.  To render an uncontrolled checkbox, use the \`defaultChecked\` prop instead. |
| onCheckedChange | `((checked: boolean, eventDetails: CheckboxRootChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Event handler called when the checkbox is ticked or unticked. |
| indeterminate | `boolean \| undefined` | 否 | false | Whether the checkbox is in a mixed state: neither ticked, nor unticked. |
| parent | `boolean \| undefined` | 否 | false | Whether the checkbox controls a group of child checkboxes.  Must be used in a [Checkbox Group](https://base-ui.com/react/components/checkbox-group). |
| uncheckedValue | `string \| undefined` | 否 | 未指定 | The value submitted with the form when the checkbox is unchecked. By default, unchecked checkboxes do not submit any value, matching native checkbox behavior. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/pr-preview/pr-202/components/yue/api/native-3eb1d4bd535e.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- Space

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
