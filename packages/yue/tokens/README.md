# @idadawn/yue-tokens

三层设计令牌（primitive → semantic → component）、明暗两态、状态色与 `Tone` 类型、浏览器主题控制器及首帧初始化脚本。包不依赖其他 yue 包；规格见 [yue §3.5](../../../docs/prd/specs/yue.md#35-令牌命名)，实现任务为 S1-YUE-004 与 S1-YUE-011。

## 令牌与样式

`src/registry.json` 是名称、角色、配对与品牌映射的唯一源。基础层包含 oklch 色阶、间距、圆角、字号、行高、阴影、动效、层级和断点；语义层包含页面、操作、边界、侧栏、图形及状态角色；组件层只引用前两层。

```ts
import '@idadawn/yue-tokens/index.css';
import { tokenColor, type Tone } from '@idadawn/yue-tokens';

const tone: Tone = 'ok'; // ok / warn / error / info / neutral
const translucentPrimary = tokenColor('--yue-primary', 0.4);
```

`--yue-*` 颜色值是可组合 alpha 的 oklch 三元组，使用 `oklch(var(--yue-primary) / 0.4)`；语义别名（如 `--primary`、`--primary-foreground`、`--background`、`--accent`）已是完整颜色，使用 `var(--primary)`。`--accent` 固定表示低强调交互背景。状态文字 `--yue-ok` 等与对应的 `--yue-ok-bg` 等背景配对。

内置 `default`（蓝色）与 `teal`（青绿色）两套品牌。语义色由 `contrast` 标注类别：文字配对 `text` ≥ 4.5:1，控件边界与信息图形 `boundary` ≥ 3:1，分隔线 `decorative` 不设阈值；缺失分类会报错。透明色先按浏览器的 sRGB 通道合成，再计算 WCAG 亮度，不能直接混合线性亮度。

视觉值以 shadcn base-nova / neutral 为基准，主色保留品牌色。按 R114 与产品负责人裁决，输入边界以 3:1 为先：亮色 neutral-450（L = 0.646）在页面、卡片和 muted 背景上均达标，暗色使用白色 34% alpha；装饰边框仍为亮色 L = 0.922、暗色白色 10% alpha。`muted-foreground` 的亮色使用最近的达标档 L = 0.546，使其在 L = 0.97 的 muted 底色上保持 4.5:1。明暗焦点色与 primary 一致。自定义背景、透明度、品牌后仍需检查实际配对。

Tailwind v4 宿主导入主题入口（已包含 `index.css`），并扫描组件源码：

```css
@import 'tailwindcss';
@import '@idadawn/yue-tokens/theme.css';
@source '../组件源码目录';
```

`theme.css` 将全部语义颜色注册为工具类，支持 `bg-primary/90`、`ring-ring/50`；同时提供 sans / mono 系统字体栈、派生圆角、阴影和动效时长，正文为 14px。没有字体下载。直接消费非 Tailwind CSS 时仍可导入 `index.css`，设置 `font-family: var(--yue-p-font-sans)`。

暗色半透明令牌的通道值含 alpha；使用完整别名（`var(--border)`）、语义工具类或 `tokenColor()` 叠加透明度，不要在已有 alpha 的通道后再追加斜线。

## 首帧与运行时

服务端将 `createThemeInitializationScript()` 返回的脚本放在 `head` 中、样式与应用模块之前，按宿主 CSP 加 nonce 或 hash。静态构建可读取包导出的 `theme-init.js`，内联为普通同步脚本；不能将首帧脚本标成 `async`、`defer` 或模块脚本。靶场的 Vite 配置提供了完整示例。

```ts
import {
  createThemeInitializationScript,
  initializeTheme,
} from '@idadawn/yue-tokens';

// 服务端输出到 head，配置会转义 HTML script 结束标记。
const firstFrameScript = createThemeInitializationScript({ defaultTheme: 'system' });

// 浏览器启动后使用同一套配置。
const theme = initializeTheme({ defaultTheme: 'system' });
theme.setTheme('dark'); // light / dark / system
theme.setBrand('teal');
theme.dispose(); // 宿主释放时移除系统主题监听
```

偏好默认保存在 `localStorage['yue-theme']`；存储不可用或值无效时回退到 `defaultTheme`（默认 `system`）。控制器同步写入 `html.dark`、`data-theme`（解析后的 light / dark）、`data-theme-preference`、`data-brand` 与 `color-scheme`。品牌优先取显式配置，再取服务端 `html` 的 `data-brand`，最后用 `default`；系统明暗变化不改变品牌。首帧与运行时应使用相同的 storageKey、默认主题与品牌配置，避免宿主自行引入二次切换。

自定义品牌可以在 `html[data-brand="品牌标识"]` 下映射 `--yue-p-brand-50` 至 `--yue-p-brand-950` 基础色阶；不要重新定义语义角色。品牌标识为小写字母开头、仅含小写字母/数字/连字符的 1–64 字符字符串。

## 修改与验证

编辑 `src/registry.json` 或 `src/theme.ts` 后执行：

```bash
pnpm --filter @idadawn/yue-tokens tokens:generate
pnpm yue:lint
pnpm yue:test
pnpm yue:build && pnpm yue:a11y && pnpm yue:lighthouse
```

`src/index.css`、`src/theme.css` 与 `src/theme-init.js` 是提交在仓库中的生成资产；`tokens:check` 校验新鲜度、引用层级、角色重名与对比度，并拒绝工作区消费 CSS 重新声明已登记的语义/组件/别名。该检查已接入 `yue:lint`。浏览器测试覆盖第一次动画帧、存储拒绝、服务端品牌、运行时切换及系统主题变化。

靶场启动后可访问 `/tokens.html` 查看品牌、明暗、状态与语义色；它是验证页。Rspress 已统一提供组件交互文档与令牌页；DTCG 导出仍为 P2。当前版本尚未发布到私有 feed，发布验收由发布任务完成。
