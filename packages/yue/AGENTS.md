# packages/yue · 给编码代理的约定

先读仓库根 `AGENTS.md` 与 [packages/yue/README.md](README.md)。这里只写在 yue 工作区干活时必须遵守的几条。

## 改哪里

- 分层矩阵：`packages/yue/boundaries.config.mjs`（唯一真相源）→ 同步 `docs/prd/specs/yue.md` §3.1 的表。不要在包里加 eslint-disable 绕过 `boundaries/dependencies`。
- 第三方依赖版本：`pnpm-workspace.yaml` 的 `catalog:`；包里只写 `"catalog:"`。不要在单个包里钉版本。
- 新组件：放进归层判定题（规格 §3.1）决定的包；带 `data-slot` 与默认 `data-testid`（`yue-<组件>-<部件>`，可用 `testId` 覆盖）；只接受字符串 `className`；写 `*.browser.test.tsx`（键盘路径 + ARIA + axe critical / serious = 0）与 `*.ssr.test.tsx`；从 `src/index.ts` 导出。不要新建第二套表单 / 表格 / 弹层引擎（规格 D3）。
- 验证门靶场 `apps/yue-playground`：新原语加一节并补 `e2e/gate.spec.ts` 用例；它是 a11y 与 Lighthouse 门禁的对象，不是产品页面。
- 生成物不进 Git：`dist/`、`.turbo/`、`test-results/`、`playwright-report/`、`.lighthouseci/`。

## 提交前

```bash
pnpm yue:lint && pnpm yue:typecheck && pnpm yue:test && pnpm yue:boundaries && pnpm yue:circular
pnpm yue:build && pnpm yue:publint && pnpm yue:a11y && pnpm yue:lighthouse
```

改了包的公开行为再加一个 changeset：`pnpm changeset`（0.x：破坏性 minor、其余 patch）。

## 不要做

- 不要 patch `@base-ui/react`；缺的 ARIA 在封装层补（Tooltip 的做法）。验证门已通过并锁定 Base UI，不要引入 Radix 或 antd。
- 不要把测试文件编进 dist（各包 `tsconfig.build.json` 已排除）。
- 文档源与普通 React 示例放在组件的 `docs/`，允许维护 `website/` 的收集、渲染与测试接线；不要手改 `website/docs/components/` 生成目录。统一 Rspress，测试与演示共用示例；新增场景同时登记 `docs/scenarios.json` 并加入站点 E2E（ADR-022）。
