# @idadawn/yue-core

Qian 的基础组件层，基于 Base UI 1.8、统一 yue 令牌与 Lucide 图标。S1-YUE-005 提供 51 个组件入口及配套部件、机器可读 manifest、Rspress 组件文档、SSR/键盘与无障碍验收。设计文档中的 `@yue/core` 是本包简称。

## 边界与安装

本层只依赖 tokens、contracts 和基础原语，不连接业务 API、不提供 SchemaForm 或命令式弹层引擎。Toast 与 Notification 共用 Base UI 的同一消息队列；上层 form 的统一消息出口可复用它。消费者安装 `@idadawn/yue-core`、React 19 与 React DOM，并引入 `@idadawn/yue-tokens/index.css`，以 Tailwind v4 扫描本包源码/发布代码中的工具类。

所有 DOM 部件具有固定 `data-slot` 和默认 `data-testid="yue-<组件>-<部件>"`；`testId` 可覆盖根部件测试标识。Root/Provider 等不产生 DOM 的状态容器通过 Trigger、Content 等部件定位。className 只接受字符串，ref 通过 React 19 的属性透传。

## 组件目录

| 类别 | 入口 |
|---|---|
| 按钮 | Button、IconButton、ButtonGroup |
| 输入 | Input、Textarea、InputNumber、InputPassword、PinInput |
| 选择 | Checkbox、Radio、RadioGroup、Switch、Toggle、Segmented、Select、Combobox |
| 字段 | Label、Field（Label/Description/Error/Control/Item 部件） |
| 弹层 | Dialog、AlertDialog、Sheet、Drawer、Popover、Tooltip、HoverCard |
| 菜单 | Menu、DropdownMenu（Menu 的兼容名称）、ContextMenu |
| 布局 | Tabs、Accordion、Card、Separator、ScrollArea、Resizable、Descriptions |
| 反馈 | Badge、Tag、Avatar、Skeleton、Spinner、Progress、Alert、Toast、Notification、Empty、Result |
| 导航与工具 | Breadcrumb、Pagination、BackTop、FullScreen、Icon |

Button 支持 primary/secondary/outline/ghost/destructive、尺寸、loading；状态色采用 Tone。输入、选择和弹层沿用 Base UI 的受控/非受控协议。Resizable 复用 react-resizable-panels 的指针与键盘布局原语；FullScreen 使用浏览器原生 API，失败交给必传的 onError 回调。

## 用法与本地化

```tsx
import { Field, FieldLabel, FieldDescription, Input, Button } from '@idadawn/yue-core';

<Field name="displayName">
  <FieldLabel>{t('profile.name')}</FieldLabel>
  <Input required />
  <FieldDescription>{t('profile.nameHint')}</FieldDescription>
</Field>
<Button loading={saving}>{t('common.save')}</Button>
```

core 不反向依赖 sdk。label、关闭/翻页/密码显隐文案、PinInput 的 getSlotLabel、Pagination 的 getPageLabel 均由宿主在本地化后传入；上层可以使用 sdk 的 i18n。图标没有 label 时视为装饰，有 label 时提供独立名称。可移除 Tag 必须提供 removeLabel，InputNumber 必须提供增减按钮名称，PinInput 的 label 标记整个验证码组及首位输入。

可搜索选择器推荐把输入框放在弹层内部：

```tsx
<Combobox items={options}>
  <ComboboxTrigger aria-label={t('city.choose')}>
    <ComboboxValue placeholder={t('city.choose')} />
  </ComboboxTrigger>
  <ComboboxContent aria-label={t('city.options')}>
    <ComboboxInput aria-label={t('city.search')} />
    <ComboboxList>{item => <ComboboxItem value={item}>{item}</ComboboxItem>}</ComboboxList>
  </ComboboxContent>
</Combobox>
```

该形态解决 Base UI 外置输入框导致其它页面内容 aria-hidden 的问题，页面门禁已取消原 Combobox 整条规则豁免。旧的外置输入写法保留兼容，但组合到包含其它焦点目标的页面时应迁移为上面的内置搜索形态。

## 机器目录与模型体验

`CORE_COMPONENT_MANIFESTS` 与逐个导出的 `ButtonManifest`、`DialogManifest` 等清单来自 `src/manifest-catalog.json`，版本自动取本包 package.json。`@idadawn/yue-contracts` 提供 JSON Schema 与由它生成的 TS 类型；每个示例必须通过对应 props Schema，令牌必须在统一目录登记。

`pnpm --filter @idadawn/yue-core build` 生成 ESM、声明文件和 `dist/yue-manifest.json`；后者通过包子路径 `@idadawn/yue-core/manifest.json` 分发。目录面向可序列化 props，ReactNode 组合使用 slots，事件回调使用 events；getSlotLabel/getPageLabel、渲染函数、ref 和 FullScreen.onError 等函数/宿主对象仍由 TypeScript API 与宿主提供，不能从 JSON 执行任意代码。模型只能选择已登记组件和属性，不能注入脚本或 dangerouslySetInnerHTML。

## Rspress 文档与验证

组件文档统一在现有站点 `/components/yue/`：每个入口有用途、真实交互、源码、组合部件、完整 Props、键盘说明与限制。`docs/catalog.json` 提供中文用途，`docs/examples/catalog.tsx` 是文档/浏览器测试/原靶场共用的普通 React 示例，`docs/scenarios.json` 保留迁移前 126 个状态场景的逐条 id，逐条由 `website/e2e/components.spec.ts` 验收。

```bash
KUN_DOCS_PATH=/本机/kun pnpm --filter website dev
KUN_DOCS_PATH=/本机/kun pnpm yue:docs
pnpm yue:gates
```

页面内 iframe 由站点构建时统一生成，隔离 Portal、焦点与 Tailwind 样式；不增加独立应用或部署入口。编辑器点击后才加载同源 Babel/Monaco，允许的模块在构建时固定；错误只显示在该演示内，重置可恢复原始示例。站点源码或示例变动后重新执行 dev/build 生成资源。

Props 从真实 TypeScript checker 提取；相同的 DOM/ARIA/事件声明按完整类型去重到站内通用属性页，保留完整的字段、必填、默认值和回调。单元测试检查 Button、Dialog、FullScreen 的关键契约和所有公开部件，生成目录不手改。构建靶场 `/core.html` 继续承担原有独立 E2E 与 Lighthouse 预算。

机器清单的属性形状维护在 `src/manifest-catalog.json`；属性 title 固定为属性名，description 由 `pnpm --filter @idadawn/yue-core manifest:generate` 从真实 TypeScript 注释生成，无注释则省略。生成器递归处理 properties、items（含元组）、prefixItems、anyOf / oneOf / allOf，嵌套属性沿真实 TS 类型取注释；数组元素与组合分支没有独立属性符号，清除其自身的 title / description，仅保留内部属性的元数据。联合分支以鉴别字段定位，TS 中不存在的属性或有歧义的注释会携带完整路径报错。修改注释后重新生成并提交目录；导出和单测会检查漂移及嵌套反例。`docs/catalog.json` 只维护组件用途，没有第二份 props 元数据。

组件级测试覆盖 SSR、键盘与 ARIA、模态焦点约束、移除与翻页、消息关闭、验证码移焦、密码显隐和缩放把手。页面级 Select 检查只排除 Base UI 的内部 Tab 焦点哨兵（data-base-ui-focus-guard + aria-hidden + data-type），保留 aria-hidden-focus 规则；另有真实 Tab/Shift+Tab 焦点测试及故意隐藏按钮的负例，确保排除不掩盖真实可访问性错误。未修改 Base UI 源码。

## 视觉配方与回归（S1-YUE-011）

基础配方以官方 shadcn CLI 4.21.5 的 `base-nova` / `neutral` / `cssVariables: true` 为来源；[逐组件对应与来源摘要](docs/visual-reference.json) 覆盖全部 51 个入口。复合入口使用对应基础组件组合，保留 yue 公开 API、标识、国际化与受控协议。品牌蓝、系统字体、Button 的 32 / 36 / 40px 尺寸，以及优先满足 4.5:1 / 3:1 的颜色是已说明的差异。

宿主导入 `@idadawn/yue-tokens/theme.css`。颜色使用 `bg-primary`、`text-muted-foreground`、`border-input` 等语义工具类，透明度使用 `/90`、`/50`；尺寸与位置变量用 Tailwind v4 的括号语法。门禁拒绝变量颜色任意值和 `brightness-*` 配方。弹层以 Base UI 的 `data-starting-style` / `data-ending-style` 过渡，尊重 reduced-motion；无障碍测试等待有限动画和焦点安排完成后检查完整规则。

Dialog / AlertDialog / Sheet / Drawer / Popover 的 Trigger 由宿主决定视觉：

```tsx
<DialogTrigger render={<Button variant="outline" />}>打开对话框</DialogTrigger>
```

`DialogContent` / `AlertDialogContent` / `SheetContent` 提供 `closeLabel` 后默认显示右上角关闭图标；标签由宿主本地化，`showCloseButton={false}` 可隐藏。旧调用不传标签时保持原有结构。图标有独立的 `*-close-button` 标识，既有 Close 部件的标识不变。

组合后仍保留 Trigger 的 `data-slot`、默认或自定义 `data-testid`，不会产生嵌套按钮。直接传 `data-slot` 给 Button 仍不能改写组件身份。

视觉基线位于 `apps/yue-playground/visual/__snapshots__/`，覆盖全部入口、DataTable / SchemaForm 以及状态清单在明暗两态的截图，弹层拍摄打开态。基线使用固定 Playwright Linux 镜像和系统字体环境，本机需要 Docker；不引入 webfont。`pnpm yue:visual` 校验现有基线，`pnpm yue:visual --update-snapshots` 仅在确认预期样式变化后更新。样式 PR 应提交变化的基线并列出变化页面；CI 的「视觉回归」检查上传实际图、期望图、差异图和追踪。

## 已知限制与延期项

- 元数据驱动表单、控件注册、Field Type 配对、业务远程选项与条件构建器分别由 form/data/sdk/widgets 任务交付。
- 本包提供展示与交互，不在前端执行权限判定；a11y 依赖宿主传入有意义的本地化名称和合法组合。
- FullScreen 的实际可用性由浏览器与嵌入策略决定；上层通过 onError 给出反馈。
- 私有 npm 首次发布与版本推进遵循工作区 Changesets 流程，本地文档构建本身不代表已部署正式站点。
