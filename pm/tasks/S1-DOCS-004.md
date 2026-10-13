---
id: S1-DOCS-004
title: 文档站换用 @yue/tokens：替换占位令牌
status: done
stage: S1
area: DOCS
wave: 3
priority: P1
type: infra
owner: "claude"
estimate: 0.5d
depends: [S1-DOCS-001, S1-YUE-004]
refs:
  - docs/prd/specs/docs-site.md
  - docs/prd/specs/yue.md
  - website/README.md
updated: 2026-10-13
---

## 背景

S1-DOCS-001 交付时 `@yue/tokens` 尚未发布：文档站主题已只引用 yue 语义令牌（`--yue-*`），取值来自 `website/theme/yue-tokens.css` 的占位实现（按 specs/yue.md §3.5 命名）。`@yue/tokens`（S1-YUE-004）发布后换成正式包。

## 验收

- [x] 删除 `website/theme/yue-tokens.css`，`website/theme/index.css` 改为导入 `@yue/tokens` 的 CSS，`website/package.json` 加依赖；令牌到 Rspress 变量的映射不改（`index.css` 第一行 `@import '@idadawn/yue-tokens/index.css'`，依赖 `workspace:*`；映射关系不变，但令牌包按 yue 规格把颜色存成 oklch 分量，颜色引用统一写成 `oklch(var(--yue-<role>))`——照搬旧写法时背景等颜色计算为无效值）
- [x] 明暗两态切换无首帧闪烁；若 `@yue/tokens` 最终用 `html.dark` 切换，删掉 `website/rspress.config.ts` 里的 `data-theme` 桥接脚本（令牌包语义层同时认 `html.dark`，桥接脚本已删；产物 `<head>` 保留 Rspress 首帧加 dark 类的内联脚本；浏览器分别模拟浅色 / 深色偏好重载，`html.dark` 与令牌值随之切换，`data-theme` 不再出现）
- [x] `pnpm --filter website build` 与 `pnpm --filter website check` 通过，`website/README.md` §5 同步更新（668 页、63.1 MB，产物样式含令牌包 176 个变量；docs-site 工作流加令牌包路径触发）

## 进度日志

- 2026-10-07 claude：创建任务（从 S1-DOCS-001 拆出：@yue/tokens 未发布，正式包替换留到这里）
- 2026-10-13 claude：开始：S1-YUE-004 的 @idadawn/yue-tokens 已就绪，文档站改为导入包 CSS（S1-DEC-019 复核跟进）
- 2026-10-13 claude：三项验收完成：改为导入 @idadawn/yue-tokens、删除占位与桥接脚本、颜色映射改写成 oklch(var(--yue-<role>))；本机构建、站点检查与浅色 / 深色目测通过。
- 2026-10-13 claude：随 S1-DEC-019 跟进 PR 提交，待 CI 通过后合并
- 2026-10-13 claude：随 idadawn/qian#63 合并完成（合并前提是 PR 全部检查通过）
