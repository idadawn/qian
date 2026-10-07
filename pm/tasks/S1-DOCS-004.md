---
id: S1-DOCS-004
title: 文档站换用 @yue/tokens：替换占位令牌
status: todo
stage: S1
area: DOCS
wave: 3
priority: P1
type: infra
owner: ""
estimate: 0.5d
depends: [S1-DOCS-001, S1-YUE-004]
refs:
  - docs/prd/specs/docs-site.md
  - docs/prd/specs/yue.md
  - website/README.md
updated: 2026-10-07
---

## 背景

S1-DOCS-001 交付时 `@yue/tokens` 尚未发布：文档站主题已只引用 yue 语义令牌（`--yue-*`），取值来自 `website/theme/yue-tokens.css` 的占位实现（按 specs/yue.md §3.5 命名）。`@yue/tokens`（S1-YUE-004）发布后换成正式包。

## 验收

- [ ] 删除 `website/theme/yue-tokens.css`，`website/theme/index.css` 改为导入 `@yue/tokens` 的 CSS，`website/package.json` 加依赖；令牌到 Rspress 变量的映射不改
- [ ] 明暗两态切换无首帧闪烁；若 `@yue/tokens` 最终用 `html.dark` 切换，删掉 `website/rspress.config.ts` 里的 `data-theme` 桥接脚本
- [ ] `pnpm --filter website build` 与 `pnpm --filter website check` 通过，`website/README.md` §5 同步更新

## 进度日志

- 2026-10-07 claude：创建任务（从 S1-DOCS-001 拆出：@yue/tokens 未发布，正式包替换留到这里）
