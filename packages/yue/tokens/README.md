# @idadawn/yue-tokens

三层设计令牌（primitive → semantic → component）、明暗两态、状态色与 `Tone` 类型、浏览器主题控制器及首帧初始化脚本。包不依赖其他 yue 包；规格见 [yue §3.5](../../../docs/prd/specs/yue.md#35-令牌命名)，实现任务为 S1-YUE-004。

## 令牌与样式

`src/registry.json` 是名称、角色、配对与品牌映射的唯一源。基础层包含 oklch 色阶、间距、圆角、字号、行高、阴影、动效、层级和断点；语义层包含页面、操作、边界、侧栏、图形及状态角色；组件层只引用前两层。

```ts
import '@idadawn/yue-tokens/index.css';
import { tokenColor, type Tone } from '@idadawn/yue-tokens';

const tone: Tone = 'ok'; // ok / warn / error / info / neutral
const translucentPrimary = tokenColor('--yue-primary', 0.4);
```

`--yue-*` 颜色值是可组合 alpha 的 oklch 三元组，使用 `oklch(var(--yue-primary) / 0.4)`；语义别名（如 `--primary`、`--primary-foreground`、`--background`、`--accent`）已是完整颜色，使用 `var(--primary)`。`--accent` 固定表示低强调交互背景。状态文字 `--yue-ok` 等与对应的 `--yue-ok-bg` 等背景配对。

内置 `default`（蓝色）与 `teal`（青绿色）两套品牌。42 个语义色在两种主题和两套品牌中的 168 个指定配对均通过 4.5:1 检查，最低约 4.674:1。这个保证针对登记的配对与不透明颜色；改变 alpha、搭配其他背景或注入自定义品牌色阶后，消费方须重新校验对比度。

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

`src/index.css` 与 `src/theme-init.js` 是提交在仓库中的生成资产；`tokens:check` 校验新鲜度、引用层级、角色重名与对比度，并拒绝工作区消费 CSS 重新声明已登记的语义/组件/别名。该检查已接入 `yue:lint`。浏览器测试覆盖第一次动画帧、存储拒绝、服务端品牌、运行时切换及系统主题变化。

靶场启动后可访问 `/tokens.html` 查看品牌、明暗、状态与语义色；它是验证页。Rspress 已统一提供组件交互文档与令牌页；DTCG 导出仍为 P2。当前版本尚未发布到私有 feed，发布验收由发布任务完成。
