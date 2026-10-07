# Qian 文档站

> 对应任务 S1-DOCS-001。规范见 [docs/prd/specs/docs-site.md](../docs/prd/specs/docs-site.md)（七个部分、首页、组件页模板、frontmatter、机器可读产物），本文是它的实施说明：怎么本地运行、目录怎么分、内容从哪来、怎么发布。

Rspress 2.0（`@rspress/core` 2.0.23，React 19，MDX）。内置全文搜索（本地索引）与 Shiki 代码高亮，Mermaid 由 `rspress-plugin-mermaid` 渲染。站点只有中文，目录结构给英文预留 `/en/`（docs-site.md §5）。

## 1 本地运行

需要 Node `^20.19.0 || >=22.12.0` 与 pnpm（版本由根目录 `package.json` 的 `packageManager` 锁定，pnpm 会自动切换）。生成脚本的单测与产物检查直接用 Node 运行 TypeScript，需要 Node ≥ 22.18；CI 用 Node 24。

```bash
pnpm install                       # 仓库根目录，一次即可
pnpm --filter website dev          # 本地预览，改 website/docs 实时刷新
pnpm --filter website build        # 产出 website/doc_build（纯静态）
pnpm --filter website preview      # 本地起服务看构建结果
pnpm --filter website typecheck    # tsc：主题组件与生成脚本
pnpm --filter website test         # 生成脚本单测（node --test）
pnpm --filter website check        # 构建产物检查，见 §7
```

`dev` 与 `build` 启动时先重新生成 `components/`、`cli/`、`design/` 三个目录（§3），改了仓库 `docs/` 之后重启 `dev` 即可看到；也可以单独运行 `pnpm --filter website generate`。

**离线**：产物不引用任何外部脚本、样式、字体或图片，搜索索引在本地，链接带 `.html` 后缀，任意静态服务器都能直接托管（例如 `python3 -m http.server -d website/doc_build`），不依赖 Rspress 的服务端。页面用绝对路径引用资源，所以不能双击 `file://` 打开。

## 2 目录骨架

按 docs-site.md §6 落地；标「生成」的目录不进 Git、不接受手改，标「后续」的由对应任务补上。

```text
website/
  rspress.config.ts            站点配置：中文、根目录 docs/、部署前缀 DOCS_BASE、Shiki 语言、Mermaid、主题桥接脚本
                               （llms 输出与多版本见 §4、S1-DOCS-003；preview / playground / api-docgen / typedoc 插件随 S1-DOCS-002）
  package.json  tsconfig.json
  README.md  AGENTS.md
  docs/
    index.mdx                  首页（docs-site.md §2a）
    sitemap.mdx                站点地图（页脚入口）
    _nav.json                  顶部导航：七个部分 +「产品」
    start/                     1 开始；后续：family.mdx 产品族地图、versions.mdx 版本矩阵（生成）（S1-DOCS-006）
    components/                2 组件目录（生成：scripts/collect-components.ts）
      platform/<id>.md
      yue/
    sdk/                       3 SDK：dotnet/  frontend/  ai/
      kun/                     后续：「Kun 框架」小节，按 KunVersion 镜像 kun 仓库（生成，with-qian.mdx 除外）（S1-DOCS-005）
      api/                     后续：TypeDoc / DocFX 生成物 dotnet/  kun/  frontend/（S1-DOCS-002、S1-DOCS-005）
    cli/                       4 CLI 与规格（生成：scripts/sync-specs.ts，来自 component-model.md）
    ops/                       5 运维
    marketplace/               6 市场与 ISV
    design/                    7 设计与路线（生成：scripts/sync-specs.ts，镜像仓库 docs/）
    public/                    原样拷贝的静态文件（logo）
  scripts/
    collect-components.ts      收集组件文件夹 → docs/components/（S1-DOCS-002 前为规划清单的占位页）
    sync-specs.ts              component-model.md → docs/cli/；docs/ → docs/design/
    sources-plugin.ts          把两个生成器接进 Rspress（config 钩子，在扫描路由之前执行）
    repo.ts                    生成脚本共用：仓库坐标、生成目录写入、链接改写
    check-site.ts              构建产物检查
    *.test.ts                  生成脚本单测
    build-kb-index.ts          后续：产出 kb-index.json（S1-DOCS-003）
    sync-kun-docs.ts           后续：kun 仓库 v<KunVersion> 的 docs/ 白名单 → docs/sdk/kun/（S1-DOCS-005）
    build-versions.ts          后续：仓库钉版文件 → docs/start/versions.mdx（S1-DOCS-006）
  theme/
    index.tsx  index.css       主题覆盖：令牌映射、建设期横幅、版本说明、首页组件
    yue-tokens.css             yue 令牌占位，@yue/tokens 发布后替换（§5）
    components/                首页区块、最新动态、版本说明、站点地图
```

## 3 七个部分与内容来源

| # | 部分 | 路径 | 来源 | 进 Git | 版本化 |
|---|---|---|---|---|---|
| 1 | 开始 | `/start/` | 手写；版本矩阵 `/start/versions` 后续由脚本生成（S1-DOCS-006） | 是 | 否 |
| 2 | 组件目录 | `/components/` | `scripts/collect-components.ts` 生成 | 否 | 是 |
| 3 | SDK | `/sdk/` | 手写；「Kun 框架」小节后续按 `KunVersion` 从 kun 仓库镜像（S1-DOCS-005）；API 参考后续生成 | 是 | 是（Kun 小节随 `KunVersion`） |
| 4 | CLI 与规格 | `/cli/` | 从 `docs/prd/specs/component-model.md` 第 2、3、4、7、8、9、10 节同步 | 否 | 是 |
| 5 | 运维 | `/ops/` | 手写 | 是 | 否 |
| 6 | 市场与 ISV | `/marketplace/` | 手写（S3 前只放规则） | 是 | 否 |
| 7 | 设计与路线 | `/design/` | 镜像仓库 `docs/`（ADR、PRD、路线图、变更日志等） | 否 | 否 |

导航用中文名，路径用英文段，页面 slug 用小写短横线。「组件怎么用」只写在第 2 部分，SDK 只写编程模型与 API，两边互相链接。

**生成目录的规则**（`scripts/sync-specs.ts`、`scripts/collect-components.ts`）：

- `design/`：`docs/` 下每个 `.md` 一页，文件名转小写、`README.md` 转 `index.md`（`docs/adr/ADR-015-model-gateway.md` → `/design/adr/adr-015-model-gateway`）。正文里指向 `docs/` 内的相对链接改写为站内路由，指向仓库其他位置的改写为 GitHub 地址；每页开头注明源文件。侧栏顺序与名称由脚本生成的 `_meta.json` 决定。
- `cli/`：按 `component-model.md` 的二级标题拆页，标题整体升一级；章节与页面的对应写在 `CLI_PAGES`，规格改了章节编号要同步这里（找不到章节时构建失败）。
- `components/`：S1-DOCS-002 之前仓库里还没有组件文件夹，脚本按 PRD §7 与组件模型规格 §7.7 的规划清单生成占位页，让首页与其他部分的链接有落点；S1-DOCS-002 把数据源换成各组件文件夹，页面改为 docs-site.md §3 的九节模板。
- 只重写内容有变化的文件、删除多余的旧文件，`dev` 不会因重新生成而反复重启。

**变更日志**：仓库 `docs/CHANGELOG.md` 是唯一来源，镜像为 `/design/changelog`；首页「最新动态」直接读它，取最近 4 条。每条一个二级标题 `## YYYY-MM-DD 标题`，下面一两行说明。

## 4 版本策略

docs-site.md §5：第 2、3、4 部分随 Qian 包族的 SemVer 锁步版本多版本发布，第 1、5、6、7 部分只有当前版。

- 现在还没有发布任何包，三部分都只有开发版（`main`）；这三部分每页顶部显示版本说明（`theme/components/PartNotice.tsx`），其余部分没有。
- 第 3 部分的「Kun 框架」小节（S1-DOCS-005）不单独选版本：每个 Qian 文档版本内嵌该版本 `KunVersion` 锁定的 Kun 文档（docs-site.md §5、ADR-021 D21.3）。
- Rspress 的 `multiVersion` 是整站多版本，不能只给三个部分分版本。首个包族版本发布时再定实现方式（候选：三部分各自按版本目录生成 + 版本切换组件），届时一并改本节与 PartNotice。

## 5 主题与令牌

- 颜色只引用 yue 的语义令牌（`--yue-primary`、`--yue-background`、`--yue-border` 等），在 `theme/index.css` 里映射到 Rspress 的主题变量；明暗两态由令牌自己在 `html[data-theme="dark"]` 下切换（specs/yue.md §3.5）。
- Rspress 用 `html.dark` 类切换明暗，`rspress.config.ts` 里的一段内联脚本在首帧前把它同步到 `data-theme`，切换时跟随，不闪烁。
- `@yue/tokens`（S1-YUE-004）还没发布，`theme/yue-tokens.css` 是按 §3.5 命名写的占位，取值不代表品牌决定。发布后：删除该文件，`theme/index.css` 第一行改为导入 `@yue/tokens` 的 CSS，`package.json` 加依赖；映射与组件都不用改。若 `@yue/tokens` 最终用 `html.dark` 切换，桥接脚本可以删掉。

## 6 托管与预览：K3s 静态托管

验收给了 Cloudflare Pages 与 K3s 静态托管两个选项，选 **K3s 静态托管**：仓库私有、文档面向内部团队，入口与任务看板一致（WireGuard 与公司内网），不需要外部账号与另一套访问控制。做法与 `deploy/pm-board` 相同，细节见 [deploy/docs-site/README.md](../deploy/docs-site/README.md)。

- CI（`.github/workflows/docs-site.yml`）：PR 与 `main` 上类型检查、单测、构建（含断链检查）、产物检查；同仓库分支的 PR 发布预览并在 PR 里留链接，合并到 `main` 后发布正式站点，PR 关闭后删除预览。只有改到 `website/`、`docs/`、依赖清单、`deploy/docs-site/` 或该工作流时才触发。
- 发布目标是 `docs-site` 分支：根目录是正式站点，`pr-preview/pr-<n>/` 是预览（构建时 `DOCS_BASE=/pr-preview/pr-<n>/`）。分支只保留一个提交（`deploy/docs-site/publish.sh`）。
- 集群里 git-sync 每 60 秒拉取该分支，nginx 提供访问：正式站点 <http://10.78.1.161:30820/>（WireGuard）、<http://192.168.1.211/>（公司内网）；预览在其后加 `pr-preview/pr-<n>/`。

## 7 门禁

| 检查 | 内容 | 在哪跑 |
|---|---|---|
| `scripts/check_docs.py` | 禁用词、表格列数、链接；覆盖 `website/docs`（.md 与 .mdx）、本文件与 `AGENTS.md`，站点源码只查禁用词；站内路由、`/design/` 镜像路由与生成页路由都按规则解析，生成目录本身不查（它们的源已在查） | docs-and-pm 工作流 |
| Rspress 断链检查 | 构建时检查全站（含生成页）的站内链接，断链即构建失败 | docs-site 工作流 |
| `pnpm --filter website check` | 七个部分与首页、404、站点地图齐全；`lang="zh"`；不引用外部资源；站内引用都带部署前缀；搜索索引存在；C# / TypeScript / JSON / YAML / Bash 都有被高亮的代码块 | docs-site 工作流 |
| `pnpm --filter website test` | 路径映射、链接改写、章节拆分、生成目录写入、组件占位页 | docs-site 工作流 |
| `tests/gates/test_check_docs.py` | check_docs.py 的站内路由解析与生成目录跳过 | dotnet 工作流（门禁自测） |

frontmatter 必填与枚举校验、`llms.txt` 与 `kb-index.json` 的新鲜度门禁属于 S1-DOCS-003。

## 8 写页面

- 手写页放在对应部分的目录下，用 `.md`；要用组件（`Badge`、卡片等）时才用 `.mdx`。
- frontmatter 先写 `title`、`description`、`part`、`project`、`audience`、`updated`；手写页的 `project` 都是 `qian`（`kun` 属于 Kun 镜像页，`yue` 属于 yue 组件页）。规划中的能力加 `stage`，S2 及以后的再加 `tag: S2` 等，侧栏会显示阶段标记。完整字段见 docs-site.md §4（校验随 S1-DOCS-003 上线）。
- 站内链接写绝对路由（`/start/hosts`、`/design/adr/adr-016-iclaw-boundary`），不带 `.md` 或 `.html`。
- 设计文档、规格与变更日志只改仓库 `docs/`，不要改 `website/docs/design/`、`cli/`、`components/`。
- 没有的能力不写成已有：S1 建设期的命令与组件按目标形态写，并在页面里说明尚未可用。
