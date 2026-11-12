> For AI agents: the complete documentation index is available at /pr-preview/pr-310/llms.txt, the full documentation bundle is available at /pr-preview/pr-310/llms-full.txt.

# Steps 步骤条

展示多步流程的进度：已完成、进行中、未通过与未开始。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 安装

```bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
```

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/pr-preview/pr-310/components/yue/index.md)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

```tsx
import { useId, useState } from 'react';
import * as C from '@idadawn/yue-core';

interface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }

export default function Example({ rootProps }: DemoProps) {
    return <div className="grid w-full max-w-2xl gap-8"><C.Steps label="开通进度" statusLabels={stepLabels} items={[{ key: 'submit', title: '提交申请', description: '10-20 14:32', status: 'done' }, { key: 'review', title: '平台审核', description: '进行中', status: 'current' }, { key: 'provision', title: '开通企业', description: '自动完成', status: 'todo' }, { key: 'use', title: '登录使用', status: 'todo' }]} {...rootProps}/>
  <C.Steps label="驳回的申请" statusLabels={stepLabels} items={[{ key: 'submit', title: '提交申请', status: 'done' }, { key: 'review', title: '平台审核', description: '已驳回', status: 'failed' }, { key: 'provision', title: '开通企业', status: 'todo' }]}/>
  <div className="rounded-xl bg-inverse p-6"><C.Steps label="申请流程" orientation="vertical" tone="inverse" size="lg" statusLabels={stepLabels} items={[{ key: 'verify', title: '验证手机号', description: '短信验证码', status: 'done' }, { key: 'fill', title: '填写企业信息', description: '企业与管理员', status: 'current' }, { key: 'review', title: '平台审核', description: '1 个工作日内', status: 'todo' }]}/></div></div>;
}
```

## 组合结构

- `Steps`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Steps

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------ | ----------------------------------------- | -- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| ref | `Ref<HTMLOListElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
| items | `readonly StepItem[]` | 是 | 未指定 | 以 TypeScript 类型约束为准。 |
| label | `string` | 是 | 未指定 | 列表的可访问名称，例如「开通进度」。 |
| statusLabels | `Readonly<Record<StepStatus, string>>` | 是 | 未指定 | 每种状态读给辅助技术的文字，由宿主本地化。 |
| orientation | `"horizontal" \| "vertical" \| undefined` | 否 | 'horizontal' | 以 TypeScript 类型约束为准。 |
| tone | `"default" \| "inverse" \| undefined` | 否 | 'default' | 配色：inverse 用在品牌深色面上（认证与申请页的侧栏）。 |
| size | `"md" \| "lg" \| undefined` | 否 | 'md' | 标记尺寸：md 为 28px、说明 12px；lg 为 32px、说明 13px。 |

[继承的 DOM / ARIA / 事件属性（280 项）](/pr-preview/pr-310/components/yue/api/native-f87b51b4e8c2.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
