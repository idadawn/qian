> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# PinInput 验证码输入

分格录入验证码，完成一格后自动移动焦点。


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

export default function Example({ disabled, rootProps }: DemoProps) { return <C.PinInput label="验证码" length={4} getSlotLabel={index => `验证码第 ${index + 1} 位`} disabled={disabled} {...rootProps}/>; }
```

## 组合结构

- `PinInput`

## 状态与交互

预览工具栏可切换默认、暗色、禁用场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### PinInput

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| defaultValue | `string \| undefined` | 否 | 未指定 | The uncontrolled OTP value when the component is initially rendered. |
| style | `React.CSSProperties \| ((state: import("@base-ui/react").OTPFieldRootState) => React.CSSProperties \| undefined) \| undefined` | 否 | 未指定 | Style applied to the element, or a function that returns a style object based on the component's state. |
| form | `string \| undefined` | 否 | 未指定 | A string specifying the \`form\` element with which the hidden input is associated. This string's value must match the id of a \`form\` element in the same document. |
| mask | `boolean \| undefined` | 否 | false | Whether the slot inputs should mask entered characters. Pass \`type\` directly to individual \`\<OTPField.Input>\` parts to use a custom input type. |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| disabled | `boolean \| undefined` | 否 | false | Whether the component should ignore user interaction. |
| name | `string \| undefined` | 否 | 未指定 | Identifies the field when a form is submitted. |
| value | `string \| undefined` | 否 | 未指定 | The OTP value. |
| id | `string \| undefined` | 否 | 未指定 | The id of the first input element. Subsequent inputs derive their ids from it (\`\{id}-2\`, \`\{id}-3\`, and so on). |
| inputMode | `"none" \| "search" \| "text" \| "tel" \| "url" \| "email" \| "numeric" \| "decimal" \| undefined` | 否 | 未指定 | The virtual keyboard hint applied to the slot inputs and hidden validation input.  Built-in validation modes provide sensible defaults, but you can override them when needed. |
| render | `React.ReactElement<unknown, string \| React.JSXElementConstructor<any>> \| ComponentRenderFn<HTMLProps, import("@base-ui/react").OTPFieldRootState> \| undefined` | 否 | 未指定 | Allows you to replace the component's HTML element with a different tag, or compose it with another component.  Accepts a \`ReactElement\` or a function that returns the element to render. |
| required | `boolean \| undefined` | 否 | false | Whether the user must enter a value before submitting a form. |
| length | `number` | 是 | 未指定 | The number of OTP input slots. Required so the root can clamp values, detect completion, and generate consistent validation markup before all slots hydrate. |
| autoComplete | `string \| undefined` | 否 | 'one-time-code' | The input autocomplete attribute. Applied to the first slot and hidden validation input. |
| readOnly | `boolean \| undefined` | 否 | false | Whether the user should be unable to change the field value. |
| onValueChange | `((value: string, eventDetails: OTPFieldRoot.ChangeEventDetails) => void) \| undefined` | 否 | 未指定 | Callback fired when the OTP value changes.  The \`eventDetails.reason\` indicates what triggered the change: - \`'input-change'\` for typing or autofill - \`'input-clear'\` when a character is removed by text input - \`'input-paste'\` for paste interactions - \`'keyboard'\` for keyboard interactions that change the value |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
| autoSubmit | `boolean \| undefined` | 否 | false | Whether to submit the owning form when the OTP becomes complete. |
| validationType | `OTPValidationType \| undefined` | 否 | 'numeric' | The type of input validation to apply to the OTP value. |
| normalizeValue | `((value: string) => string) \| undefined` | 否 | 未指定 | Function that normalizes the OTP value after whitespace and \`validationType\` filtering. It runs whenever OTP Field normalizes a value, including initial/default values, controlled values, and user edits.  The returned value is filtered by \`validationType\` again, then clamped to \`length\`. It should be idempotent because OTP Field may normalize the same value more than once while handling edits, storing state, and rendering controlled or uncontrolled values. Non-idempotent normalizers can compound across those normalization passes. Characters rejected while normalizing typed or pasted text are reported through \`onValueInvalid\`. |
| onValueInvalid | `((value: string, eventDetails: OTPFieldRoot.InvalidEventDetails) => void) \| undefined` | 否 | 未指定 | Callback fired when entered text contains characters that are rejected by validation or normalization before the OTP value updates.  The \`value\` argument is the attempted user-entered string before normalization. |
| onValueComplete | `((value: string, eventDetails: OTPFieldRoot.CompleteEventDetails) => void) \| undefined` | 否 | 未指定 | Callback function that is fired when the OTP value becomes complete, or when a complete value is pasted while the OTP is already complete.  When the value changes, it runs later than \`onValueChange\`, after the internal value update is applied. If a complete pasted value matches the current value, \`onValueChange\` does not fire.  If \`autoSubmit\` is enabled, it runs immediately before the owning form is submitted. |
| label | `string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |
| getSlotLabel | `(index: number) => string` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |

[继承的 DOM / ARIA / 事件属性（270 项）](/pr-preview/pr-202/components/yue/api/native-eb5a8698a849.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
