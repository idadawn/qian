---
id: S1-DOCS-003
title: llms 输出、页面元数据规范与知识库导入契约
status: done
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

- [x] `rspress.config.ts` 开启 `llms: true`，构建产物含 `llms.txt`、`llms-full.txt` 与每页 `.md`；CI 校验三者与源文档同步（过期即失败）——`pnpm --filter website check`：索引里每页的 `.md` 与 HTML 都在、sha256 一致，`llms.txt` / `llms-full.txt` / `sitemap.xml` 收录每页，没有索引外的 `.md`
- [x] 站点自定义 MDX 组件在 `import.meta.env.SSG_MD` 下输出语义文本：CI 抽检生成的 `.md` 无 JSX 残留、无空白段落、演示代码块保留——全部 614 页都查，不只抽检；「空白段落」按空标题 / 空列表项判，代码块按围栏不少于源页判（DocFX 的 HTML 代码块会多出围栏）
- [x] 页面 frontmatter 按 `docs/prd/specs/docs-site.md` §4 实现（`part`、`audience`、`component`、`version`、`profile`、`surface`、`stage`、`updated` 等），校验器纳入 `scripts/check_docs.py`——生成页（组件、CLI、设计镜像）补齐 `project` / `audience` / `updated` / `source`；`version` 对组件页与 SDK 页的强制等多版本开启时启用（§4）
- [x] 构建产出 `kb-index.json`（每页：url、md url、标题、frontmatter 元数据、内容 sha256、更新时间）与 `sitemap.xml`，作为知识库导入契约并写进规格——`website/scripts/build-kb-index.ts`，契约在 docs-site.md §5.1
- [x] 文档站首页与每页提供「复制为 Markdown」入口，指向该页的 `.md`——文档页由 `themeConfig.llmsUI` 注入标题下，首页在 `HomeHero` 里手动放；只留「复制 Markdown 链接」，不放外部 AI 服务入口

## 进度日志

- 2026-10-07 claude：创建任务
- 2026-10-07 claude：信息架构（七个部分）经产品负责人确认，写入 specs/docs-site.md 并对齐验收
- 2026-10-09 claude：开始；产品负责人 2026-10-09 让 Claude 自选任务，选定 DOCS 领域：S1-DOCS-003 → S1-DOCS-006，之后 S1-YUE-003
- 2026-10-09 claude：Rspress 2.0.23 的 `llms: true` 自带 SSG-MD（每页 .md + llms.txt / llms-full.txt），不用 plugin-llms；主题组件补 SSG_MD 分支，首页卡片正文里的行内 Badge 改成文字（JSX 容器内的行内组件会原样残留）；新增 build-kb-index.ts（kb-index.json + sitemap.xml，lastmod 取 updated）与 check-site 的同步 / 质量检查；check_docs.py 加 frontmatter 校验，生成页补齐字段；规格 §4、§5.1、§6、§7 与 README / AGENTS 同步。本地：614 页（qian 116、kun 497、yue 1）全部有 .md，产物 57.9 MB；网站单测 58、门禁自测 32、check_docs 217 + 生成页 587 通过
- 2026-10-09 claude：完成：SSG-MD 每页 Markdown、llms.txt / llms-full.txt、kb-index.json 与 sitemap.xml 契约、frontmatter 校验器与「复制为 Markdown」入口，五项验收全部满足，提交 PR 评审
- 2026-10-09 claude：已合并 PR #12
