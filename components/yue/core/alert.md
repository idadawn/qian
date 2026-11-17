> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Alert 提示信息

用语义色和文本展示需要用户注意的信息。


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

export default function Example({ rootProps }: DemoProps) { return <div className="grid gap-3"><C.Alert tone="info" {...rootProps}>配置已保存，稍后生效。</C.Alert><C.Alert tone="error" title="驳回原因">企业名称与申请说明中的单位不一致，请填写营业执照上的全称后重新提交。</C.Alert><C.Alert tone="warn" size="sm" action={<C.Button size="xs" variant="outline">查看</C.Button>}>同名企业已存在，请核对是否重复申请。</C.Alert><C.Alert tone="ok" size="sm">手机号已验证。</C.Alert></div>; }
```

## 组合结构

- `Alert`

## 状态与交互

预览工具栏可切换默认、暗色场景；切换场景会重置示例。悬停状态需将指针实际移入目标；弹层通过示例里的触发器打开与关闭。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

### Alert

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------- | ---------------------------------- | -- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| className | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| ref | `Ref<HTMLDivElement> \| undefined` | 否 | 未指定 | Allows getting a ref to the component instance. Once the component unmounts, React will set \`ref.current\` to \`null\` (or call the ref with \`null\` if you passed a callback ref). |
| children | `ReactNode` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| testId | `string \| undefined` | 否 | 未指定 | 覆盖默认的 \`data-testid\`（默认 \`yue-\<组件>-\<部件>\`）。 |
| tone | `Tone \| undefined` | 否 | 'info' | 以 TypeScript 类型约束为准。 |
| title | `ReactNode` | 否 | 未指定 | 加粗的标题行；有标题时正文在其下方。 |
| icon | `boolean \| undefined` | 否 | true | 是否在左侧显示按语气选择的图标。 |
| action | `ReactNode` | 否 | 未指定 | 右侧操作，例如「重试」「刷新页面」按钮；窄屏时换到正文下方。 |
| size | `"sm" \| "md" \| undefined` | 否 | 'md' | 尺寸：sm 用在表单内（13px、紧凑内边距），md 用在页面与卡片。 |

[继承的 DOM / ARIA / 事件属性（276 项）](/components/yue/api/native-7b107593e0a4.md)：从相同的 TypeScript 声明提取，完整类型与说明只存一份。

## 键盘与无障碍

展示内容不接管键盘焦点。

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 `@idadawn/yue-core/manifest.json` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 `@idadawn/yue-core@0.1.0`；组件行为与示例以源码版本为准。
