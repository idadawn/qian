> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Result 结果页

展示操作结果及下一步入口。


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

export default function Example({ rootProps }: DemoProps) { const [returned, setReturned] = useState(false); if (returned)
    return <p>记录列表</p>; return <C.Result title="操作成功" description="更改已经保存。" action={<C.Button onClick={() => setReturned(true)}>返回列表</C.Button>} {...rootProps}/>; }
```

## 组合结构

- `Result`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Result

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -- | ------- | -------------------- |
| tone | `Tone \| undefined` | 否 | 'ok' | 以 TypeScript 类型约束为准。 |
| title | `ReactNode` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |
| description | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| action | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| icon | `"alert" \| "arrowUp" \| "check" \| "chevronDown" \| "chevronLeft" \| "chevronRight" \| "eye" \| "eyeOff" \| "info" \| "spinner" \| "maximize" \| "minimize" \| "minus" \| "close" \| undefined` | 否 | 'check' | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
