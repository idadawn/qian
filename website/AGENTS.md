# AGENTS.md — 文档站（website/）

仓库级约定见根目录 [AGENTS.md](../AGENTS.md)，本文件只补文档站的部分。站点怎么分、内容从哪来、怎么发布见 [README.md](README.md)；规范是 [docs/prd/specs/docs-site.md](../docs/prd/specs/docs-site.md)。

## 改哪里

| 想改的内容 | 改这里 | 不要改 |
|---|---|---|
| 开始、SDK、运维、市场的页面 | `website/docs/{start,sdk,ops,marketplace}/` | — |
| ADR、PRD、规格、路线图、变更日志 | 仓库 `docs/`（站内 `/design/` 自动镜像） | `website/docs/design/` |
| Kun 的文档 | kun 仓库（提 PR）；站内「Kun 框架」小节与 Kun API 参考按 `KunVersion` 生成，升级只改根目录 `Directory.Packages.props` 的 `KunVersion` | `website/docs/sdk/kun/`（`with-qian.mdx` 除外）、`website/docs/sdk/api/` |
| Qian 对 Kun 的选用与约束 | `website/docs/sdk/kun/with-qian.mdx`（只转述 ADR-001 / ADR-021 与 PRD §07，不另立规则） | — |
| Kun 镜像的白名单、侧栏与横幅 | `docs/prd/specs/docs-site.md` §2b 与 `website/scripts/sync-kun-docs.ts` 一起改 | 生成出来的页面 |
| CLI 命令与清单字段 | `docs/prd/specs/component-model.md`（站内 `/cli/` 自动同步） | `website/docs/cli/` |
| 组件页 | yue core：`packages/yue/core/docs/` 与 `website/scripts/yue-docs.ts`；其它组件：`collect-components.ts` 的规划清单，S1-DOCS-002 后改为组件目录 | `website/docs/components/` |
| 首页 | `website/docs/index.mdx`（结构按 docs-site.md §2a） | — |
| 产品族地图 | `website/docs/start/family.md` 与仓库 `README.md` 的「产品族」表一起改（单测核对前五列一致） | — |
| 版本矩阵 | 对应的钉版文件（`Directory.Packages.props`、`global.json`、`package.json`、`deploy/baseline.json` 等） | `website/docs/start/versions.md`（生成，不进 Git） |
| 颜色与样式 | `website/theme/index.css`，只引用 `--yue-*` 语义令牌 | 在组件里写死颜色 |

`components/`、`cli/`、`design/`、`sdk/kun/`（`with-qian.mdx` 除外）、`sdk/api/` 与 `start/versions.md` 由构建前的脚本生成，不进 Git（进了文档门禁即失败）；手改会在下一次构建时被覆盖。

## 写页面

- 中文；不写参考平台与对标项目的名字。
- 站内链接写绝对路由，如 `/start/hosts`、`/design/adr/adr-020-component-model-v2`，不带扩展名。
- frontmatter 至少写 `title`、`description`（≤ 120 字）、`part`（与目录一致）、`project`（手写页为 `qian`）、`audience`、`updated`；规划中的能力写 `stage`，S2 及以后再加 `tag`。字段与枚举由 `scripts/check_docs.py` 校验（docs-site.md §4），只用 `key: 值`、`key: [a, b]`、缩进 `- 项` 与引号字符串这几种写法。
- 卡片等 JSX 容器内部的 Markdown 里不要放行内组件（`Badge` 等）：SSG-MD 产出的 `.md` 会原样残留，产物检查会拦；阶段标记写成「（S2）」这样的文字，组件只放在 JSX 属性里（如 `Card` 的 `title`）。
- 没有的能力不写成已有；S1 建设期的命令按目标形态写，并注明尚未可用。

## 写脚本与主题

- `website/scripts/*.ts` 由 Node 直接运行（类型擦除），也被 Rspress 加载配置时导入：只用可擦除的 TS 语法，类型导入写 `import type`，相对导入带 `.ts` 扩展名。
- 主题组件放 `website/theme/components/`，从 `website/theme/index.tsx` 导出后可在 `.mdx` 里 `import { … } from '@theme'`。
- 自定义组件要带 `import.meta.env.SSG_MD` 分支：那一遍只输出标题、段落、列表与链接（按钮、分隔符、提示条不输出），站内链接指向 `.md`（`routePathToMdPath` 或 `withBase('/x.md')`）。

## 提交前

构建要读 kun 仓库并跑 DocFX：需要 .NET SDK，并让 `KUN_DOCS_PATH` 指向本机 kun 克隆（或能用 SSH 克隆 `idadawn/kun`），见 [README.md](README.md) §1。

```bash
python3 scripts/check_docs.py
pnpm --filter website typecheck && pnpm --filter website test
pnpm --filter website build && pnpm --filter website check && pnpm --filter website e2e
python3 scripts/check_docs.py website/docs/components website/docs/cli website/docs/design website/docs/sdk/kun website/docs/sdk/api website/docs/start/versions.md
```
