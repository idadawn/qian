> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# InputNumber 数字输入

通过键盘或增减按钮输入数字，并遵守范围限制。


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

export default function Example({ disabled, rootProps }: DemoProps) { return <C.InputNumber label="数量" decreaseLabel="减少数量" increaseLabel="增加数量" defaultValue={2} min={0} max={10} disabled={disabled} {...rootProps}/>; }
```

## 组合结构

- `InputNumber`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### InputNumber

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| defaultValue | `number \| undefined` | 否 | 未指定 | The uncontrolled value of the field when it's initially rendered.  To render a controlled number field, use the \`value\` prop instead. |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").NumberFieldRootState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| form | `string \| undefined` | 否 | 未指定 | Identifies the form that owns the hidden input. Useful when the number field is rendered outside the form. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| name | `string \| undefined` | 否 | 未指定 | Identifies the field when a form is submitted. |
| value | `number \| null \| undefined` | 否 | 未指定 | The raw numeric value of the field. |
| id | `string \| undefined` | 否 | 未指定 | The id of the input element. |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").NumberFieldRootState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| step | `number \| "any" \| undefined` | 否 | 1 | Amount to increment and decrement with the buttons and arrow keys, or to scrub with pointer movement in the scrub area. To always enable step validation on form submission, specify the \`min\` prop explicitly in conjunction with this prop. Specify \`step="any"\` to always disable step validation; interactive stepping then uses a base amount of \`1\`, while the alt and shift keys still step by \`smallStep\` and \`largeStep\`. |
| required | `boolean \| undefined` | 否 | false | Whether the user must enter a value before submitting a form. |
| readOnly | `boolean \| undefined` | 否 | false | Whether the user should be unable to change the field value. |
| inputRef | `React.Ref<HTMLInputElement> \| undefined` | 否 | 未指定 | A ref to access the hidden input element. |
| locale | `Intl.LocalesArgument` | 否 | 未指定 | The locale of the input element. Defaults to the user's runtime locale. |
| max | `number \| undefined` | 否 | 未指定 | The maximum value of the input element. |
| min | `number \| undefined` | 否 | 未指定 | The minimum value of the input element. |
| onValueChange | `((value: number \| null, eventDetails: NumberFieldRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Callback fired when the number value changes.  The \`eventDetails.reason\` indicates what triggered the change: - \`'input-change'\` for parseable typing or programmatic text updates - \`'input-clear'\` when the field becomes empty - \`'input-blur'\` when formatting (and clamping, if enabled) occurs on blur - \`'input-paste'\` for paste interactions - \`'keyboard'\` for arrow-key/Home/End stepping (typing digits uses \`'input-change'\`/\`'input-clear'\`) - \`'increment-press'\` / \`'decrement-press'\` for button presses on the increment and decrement controls - \`'wheel'\` for wheel-based scrubbing - \`'scrub'\` for scrub area drags |
| format | `Intl.NumberFormatOptions \| undefined` | 否 | 未指定 | Options to format the input value. |
| allowOutOfRange | `boolean \| undefined` | 否 | false | When true, direct text entry may be outside the \`min\`/\`max\` range without clamping, so native range underflow/overflow validation can occur. Step-based interactions (keyboard arrows, buttons, wheel, scrub) still clamp. |
| smallStep | `number \| undefined` | 否 | 0.1 | The small step value of the input element when incrementing while the alt key is held. Snaps to multiples of this value when \`snapOnStep\` is enabled. |
| largeStep | `number \| undefined` | 否 | 10 | The large step value of the input element when incrementing while the shift key is held. Snaps to multiples of this value when \`snapOnStep\` is enabled. |
| allowWheelScrub | `boolean \| undefined` | 否 | false | Whether to allow the user to scrub the input value with the mouse wheel while focused and hovering over the input. |
| snapOnStep | `boolean \| undefined` | 否 | false | Whether the value should snap to the nearest step when incrementing or decrementing. |
| onValueCommitted | `((value: number \| null, eventDetails: NumberFieldRoot.CommitEventDetails) => void) \| undefined` | 否 | 未指定 | Callback function that is fired when the value is committed. It runs later than \`onValueChange\`, when: - The input is blurred after typing a value. - The pointer is released after scrubbing or pressing the increment/decrement buttons.  It runs simultaneously with \`onValueChange\` when interacting with the keyboard or the mouse wheel.  **Warning**: This is a generic event not a change event. |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
| label | `string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |
| decreaseLabel | `string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |
| increaseLabel | `string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |

[继承的 DOM / ARIA / 事件属性（271 项）](/pr-preview/pr-122/components/yue/api/native-0ab372837fc1.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

- ArrowUp
- ArrowDown

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
