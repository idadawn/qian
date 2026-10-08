---
id: S1-DOCS-003
title: llms 输出、页面元数据规范与知识库导入契约
status: doing
stage: S1
area: DOCS
wave: 3
priority: P0
type: feature
owner: "claude"
estimate: 2d
depends: [S1-DOCS-001]
refs:
  - docs/prd/specs/docs-site.md#4
  - docs/adr/ADR-013-frontend-yue.md#D13.18
  - docs/adr/ADR-020-component-model-v2.md#D20.9
  - docs/prd/10-s4-agent.md#10.1
updated: 2026-10-09
---

## 背景

产品负责人规划了一个知识库，后期要读取文档站的内容，并要求文档站具备 AI 能力。S1 先把「机器可读」做扎实：开启 Rspress 的 `llms` 输出（`llms.txt`、`llms-full.txt`、每页一份 Markdown，SSG-MD 在构建期直接渲染，不是 HTML 回转），定义页面 frontmatter 元数据规范，并产出一份知识库导入契约，供 S2 的知识库与站内「问 AI」（S2-DOCS-001）消费。

## 验收

- [ ] `rspress.config.ts` 开启 `llms: true`，构建产物含 `llms.txt`、`llms-full.txt` 与每页 `.md`；CI 校验三者与源文档同步（过期即失败）
- [ ] 站点自定义 MDX 组件在 `import.meta.env.SSG_MD` 下输出语义文本：CI 抽检生成的 `.md` 无 JSX 残留、无空白段落、演示代码块保留
- [ ] 页面 frontmatter 按 `docs/prd/specs/docs-site.md` §4 实现（`part`、`audience`、`component`、`version`、`profile`、`surface`、`stage`、`updated` 等），校验器纳入 `scripts/check_docs.py`
- [ ] 构建产出 `kb-index.json`（每页：url、md url、标题、frontmatter 元数据、内容 sha256、更新时间）与 `sitemap.xml`，作为知识库导入契约并写进规格
- [ ] 文档站首页与每页提供「复制为 Markdown」入口，指向该页的 `.md`

## 进度日志

- 2026-10-07 claude：创建任务
- 2026-10-07 claude：信息架构（七个部分）经产品负责人确认，写入 specs/docs-site.md 并对齐验收
- 2026-10-09 someone：开始；产品负责人 2026-10-09 让 Claude 自选任务，选定 DOCS 领域：S1-DOCS-003 → S1-DOCS-006，之后 S1-YUE-003
