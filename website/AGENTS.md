# AGENTS.md — 文档站（website/）

仓库级约定见根目录 [AGENTS.md](../AGENTS.md)，本文件只补文档站的部分。站点怎么分、内容从哪来、怎么发布见 [README.md](README.md)；规范是 [docs/prd/specs/docs-site.md](../docs/prd/specs/docs-site.md)。

## 改哪里

| 想改的内容 | 改这里 | 不要改 |
|---|---|---|
| 开始、SDK、运维、市场的页面 | `website/docs/{start,sdk,ops,marketplace}/` | — |
| ADR、PRD、规格、路线图、变更日志 | 仓库 `docs/`（站内 `/design/` 自动镜像） | `website/docs/design/` |
| CLI 命令与清单字段 | `docs/prd/specs/component-model.md`（站内 `/cli/` 自动同步） | `website/docs/cli/` |
| 组件页 | S1-DOCS-002 前：`website/scripts/collect-components.ts` 的规划清单；之后：组件文件夹的 README 与 `docs/*.mdx` | `website/docs/components/` |
| 首页 | `website/docs/index.mdx`（结构按 docs-site.md §2a） | — |
| 颜色与样式 | `website/theme/index.css`，只引用 `--yue-*` 语义令牌 | 在组件里写死颜色 |

`components/`、`cli/`、`design/` 三个目录由构建前的脚本生成，不进 Git；手改会在下一次构建时被覆盖。

## 写页面

- 中文；不写参考平台与对标项目的名字。
- 站内链接写绝对路由，如 `/start/hosts`、`/design/adr/adr-020-component-model-v2`，不带扩展名。
- frontmatter 至少写 `title`、`description`、`part`、`audience`、`updated`；规划中的能力写 `stage`，S2 及以后再加 `tag`。
- 没有的能力不写成已有；S1 建设期的命令按目标形态写，并注明尚未可用。

## 写脚本与主题

- `website/scripts/*.ts` 由 Node 直接运行（类型擦除），也被 Rspress 加载配置时导入：只用可擦除的 TS 语法，类型导入写 `import type`，相对导入带 `.ts` 扩展名。
- 主题组件放 `website/theme/components/`，从 `website/theme/index.tsx` 导出后可在 `.mdx` 里 `import { … } from '@theme'`。

## 提交前

```bash
python3 scripts/check_docs.py
pnpm --filter website typecheck && pnpm --filter website test
pnpm --filter website build && pnpm --filter website check
```
