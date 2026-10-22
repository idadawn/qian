# yue 前端工作区

yue 是 Qian 的 React 组件库与外壳（产品族的前端层，[ADR-021](../../docs/adr/ADR-021-product-family-structure.md)），随 Qian 单仓以 pnpm workspace 交付，发 GitHub Packages。**包名**：`@idadawn/yue-<包>`（目录 `packages/yue/<包>`）；设计文档里写的 `@yue/<包>` 是同一个包的简称（规格 §3.1）。本目录是 S1-YUE-003 建立的工程化基线：包骨架、分层边界、八项 CI 门禁与 Base UI 验证门。依据：[PRD §07 7.15.2](../../docs/prd/07-s1-foundation.md)、[yue 专题规格](../../docs/prd/specs/yue.md)（§3.1 分层、§4.1 流程、§4.4 验证门、§8 验收）、[ADR-013](../../docs/adr/ADR-013-frontend-yue.md)。

## 1 布局

```text
pnpm-workspace.yaml        工作区与版本目录（catalog:）：前端依赖只在这里钉一次
turbo.json                 Turborepo：build / typecheck / publint 跨包编排与缓存
eslint.config.mjs          代码规则 + 分层边界；eslint.boundaries.config.mjs 只跑边界
.dependency-cruiser.cjs    循环依赖与不可解析 import
vitest.config.ts           测试入口：门禁自测、SSR、各包浏览器测试
.changeset/                Changesets 配置（@idadawn/yue-* 固定同一版本号）
packages/yue/
  boundaries.config.mjs    六层分层矩阵——唯一真相源，新包先来这里登记
  tsconfig.base.json       各包共用的编译选项
  tokens/ contracts/ core/ sdk/ shell/ data/ form/ biz/
                           @idadawn/yue-<目录名>；每个包：package.json、tsconfig.json、rslib.config.ts、src/index.ts、README.md
apps/yue-playground/       验证门与门禁靶场：六个原语各一节；Playwright + axe（e2e/）与 Lighthouse（lighthouserc.cjs）对它跑
tests/gates/yue/           门禁自测：分层边界、循环依赖样例、包形状
.github/workflows/yue.yml  八项门禁矩阵
```

## 2 分层与依赖方向

包结构与允许依赖以 [规格 §3.1](../../docs/prd/specs/yue.md#31-包结构与依赖方向) 为准，矩阵写在 `boundaries.config.mjs`：下层不得 import 上层；shell / data / form / charts / code-editor 同层互不依赖（shell → form 的登录表单是唯一例外）；横切包 sdk / agent / mobile 只被上层消费；tokens / contracts 不依赖任何 `@yue` 包。违规在 lint 阶段直接失败，不接受「临时引用、下个迭代修」。

新建一个包：在 `boundaries.config.mjs` 的 `DIRECT` 登记目录名与允许依赖（没登记的包名什么 `@yue` 包都不许引）→ 同步规格 §3.1 的表 → 复制一个现有包的四个文件改名 → `pnpm install`。`tests/gates/yue/packages.test.ts` 会核对 package.json 的依赖不超出矩阵。

## 3 门禁与命令

| 门禁 | 命令 | 工具 | 查什么 |
|---|---|---|---|
| lint | `pnpm yue:lint` | ESLint 10 + typescript-eslint + react-hooks | 代码规则（含边界） |
| typecheck | `pnpm yue:typecheck` | tsc（Turborepo 编排） | 各包、靶场、门禁自测 |
| unit | `pnpm yue:test` | Vitest 5：Node 项目 + 浏览器模式（Playwright Chromium）| 门禁自测、SSR、组件键盘 / ARIA / axe 用例 |
| a11y | `pnpm yue:a11y` | Playwright + `@axe-core/playwright` | 靶场每个原语打开态 critical / serious = 0（先 build 靶场） |
| lighthouse | `pnpm yue:lighthouse` | Lighthouse CI | 首屏 script 压缩后 ≤ 250 KB（PRD §14），a11y 分数 = 1 |
| boundaries | `pnpm yue:boundaries` | eslint-plugin-boundaries | 六层依赖方向 |
| publint | `pnpm yue:publint` | publint（`pnpm pack` 后检查） | 发布形状：exports / types / files |
| circular | `pnpm yue:circular` | dependency-cruiser | 循环依赖、不可解析 import |

一把全跑：`pnpm yue:gates`（含文档站与视觉回归）。首次需要 `pnpm --filter yue-playground exec playwright install chromium`；Lighthouse 需要本机有 Chrome。CI 工作流 `yue` 把八项各作一个检查在 PR 上显示。

## 4 约定

- **依赖**：第三方版本只写在 `pnpm-workspace.yaml` 的 `catalog:`，包里写 `"catalog:"`；`@yue` 之间用 `workspace:^`。`typescript` 钉 6.0.x（typescript-eslint 8 只支持 < 6.1）。
- **包形状**：开发期 `exports["."]` 直指 `./src/index.ts`（tsc、Vite、Vitest 都不用先 build）；`publishConfig.exports` 指 `dist/`，`pnpm pack` 时替换；Rslib 输出 ESM + d.ts，CSS 以 `./index.css` 这类子路径单独导出（见 `@idadawn/yue-tokens`）。测试文件只参与类型检查，不进 dist（`tsconfig.build.json`）。
- **可寻址属性**：`data-slot="<组件>-<部件>"` 固定；`data-testid` 默认 `yue-<组件>-<部件>`，消费方用 `testId` 覆盖成业务无关的稳定标识，不含用户数据（规格 §7 第 8 条）。
- **封装层**：只接受字符串 `className`（交给 `cn` 合并，不透传 Base UI 的函数式 className）；Base UI 缺的 ARIA 在封装层补（Tooltip 的 `role` / `aria-describedby`）；默认值向 WAI-ARIA 靠（Tabs 自动激活）。
- **测试**：`*.browser.test.tsx` 在真浏览器跑（键盘用 `vitest/browser` 的 `userEvent`，查询用 Testing Library，axe 用 `axe-core`，critical + serious = 0）；`*.ssr.test.tsx` 在 Node 跑 `renderToString`。
- **样式**：Tailwind v4 由消费方扫描 `packages/yue` 源码生成（靶场 `src/index.css` 的 `@source`），包本身不打包工具类；令牌由 `@idadawn/yue-tokens` 提供（S1-YUE-004）。
- **文案**：组件内不写死面向用户的文案，由宿主使用 `@idadawn/yue-sdk` 的 i18n 后传入 label 等属性，core 保持不依赖 sdk；靶场里的演示文案不受此限。

## 5 版本策略

- `@idadawn/yue-*` 全部包固定同一版本号（`.changeset/config.json` 的 `fixed`），对应插件清单的 `engines.yue`（ADR-013 D13.4）。
- `0.x` 期间：破坏性变更记 `minor`，修复与兼容新增记 `patch`，每个 minor 都可能破坏；`1.0` 之后只在大版本破坏，废弃的 API 至少保留一个 minor 并给出迁移说明；主版本升级有双版本并存期（规格 §4.3）。
- 流程：改了包的公开行为就在 PR 里 `pnpm changeset` 选包与级别；合并到 main 后由发布流程 `changeset version` + `changeset publish`。
- **发布**：GitHub Packages（规格 D14，2026-10-09 拍板）。GitHub Packages 要求 scope 与所有者同名，所以包名是 `@idadawn/yue-<包>`；每个包的 `publishConfig.registry` 已指 `https://npm.pkg.github.com`。发布步骤：合并到 main 后 `pnpm changeset version` → `pnpm -r --filter './packages/yue/*' publish`（环境变量 `NODE_AUTH_TOKEN` 用带 `write:packages` 的令牌或 Actions 的 `GITHUB_TOKEN`）；首次发布与发布工作流留给发布任务（规格 §8「Changesets 发包到私有 npm 成功一次」）。消费方在项目 `.npmrc` 写：

  ```ini
  @idadawn:registry=https://npm.pkg.github.com
  //npm.pkg.github.com/:_authToken=${GITHUB_TOKEN}
  ```

## 6 Base UI 验证门

2026-10-09 通过，锁定 `@base-ui/react` 1.8，不切 Radix、不混用；五项发现与封装层处理见 [ADR-013 §9](../../docs/adr/ADR-013-frontend-yue.md#9-状态历史status-history)。`@idadawn/yue-core` 已由 S1-YUE-005 扩展为 51 个组件入口、配套 manifest 与 Rspress 交互文档；S1-YUE-008 在同一 Tree 上补齐虚拟化，并交付表格、树选择、日期、Upload 与 ImagePreview。

## 7 已知限制与延期项

- tokens 的品牌、明暗与语义色验证页已在靶场 `/tokens.html` 建立；Rspress 组件页与站内交互编辑器由 S1-YUE-005 提供，执行 `pnpm yue:docs` 验证生成、真实交互、离线编辑及静态构建；sdk / shell / data / form / biz 的组件文案门禁已随 SDK 接入 lint，资源包、示例与测试除外，core 继续由宿主传 label；视觉回归已随 S1-YUE-011 接入 `pnpm yue:visual`，采用固定 Linux 镜像，本机需 Docker。
- tokens 已实现三层令牌和首帧主题控制；sdk 已交付类型化客户端、注册表与三语言 i18n；form 已交付 SchemaForm 与统一弹层；data 已交付 S1 基础组件，真实上传 E2E 仍归 S1-FILE-003；shell 已交付页面骨架、菜单/偏好与注入式认证页族（R111）；biz 仍是骨架；版本 0.1.0 未发布。
- Lighthouse 同时覆盖靶场各页与 `/shell.html` 的真实 AppShell 首屏；认证表单按需懒加载，脚本压缩后 ≤ 250 KiB、a11y = 100 的预算不变（PRD §14）。真实 qian.shell 部署接线由 S1-YUE-001/002 继续验收。
