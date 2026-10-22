# Qian 文档站

> 对应任务 S1-DOCS-001（脚手架）、S1-DOCS-005（「Kun 框架」小节与 Kun API 参考）与 S1-DOCS-003（机器可读产物与 frontmatter 校验）。规范见 [docs/prd/specs/docs-site.md](../docs/prd/specs/docs-site.md)（七个部分、首页、组件页模板、frontmatter、机器可读产物），本文是它的实施说明：怎么本地运行、目录怎么分、内容从哪来、怎么发布。

Rspress 2.0（`@rspress/core` 2.0.23，React 19，MDX）。内置全文搜索（本地索引）与 Shiki 代码高亮，Mermaid 由 `rspress-plugin-mermaid` 渲染。站点只有中文，目录结构给英文预留 `/en/`（docs-site.md §5）。

## 1 本地运行

需要 Node `^20.19.0 || >=22.12.0` 与 pnpm（版本由根目录 `package.json` 的 `packageManager` 锁定，pnpm 会自动切换）。生成脚本的单测与产物检查直接用 Node 运行 TypeScript，需要 Node ≥ 22.18；CI 用 Node 24。

「Kun 框架」小节与 Kun API 参考（§3）另外需要：

- **kun 仓库**（私有，`idadawn/kun`）：环境变量 `KUN_DOCS_PATH` 指向本机克隆（要有 tag `v<KunVersion>`，没有就先 `git -C <克隆> fetch --tags`）；不设时脚本用 SSH 克隆到 `website/.cache/kun-repo`，需要对该仓库有读权限。
- **.NET SDK**（版本见根目录 `global.json`）：DocFX 是 `website/.config/dotnet-tools.json` 锁定的本地工具，首次构建时自动 `dotnet tool restore`，并从 nuget.org 还原同版本的 `Kun.*` 包。

两者缺一构建即失败，不跳过、不回退 `main`（docs-site.md §2b.1）。

```bash
pnpm install                       # 仓库根目录，一次即可
pnpm --filter website dev          # 本地预览，改 website/docs 实时刷新
pnpm --filter website build        # 产出 website/doc_build（纯静态；末尾产出 kb-index.json 与 sitemap.xml）
pnpm --filter website kb-index     # 只重新产出 kb-index.json 与 sitemap.xml（需先 build）
pnpm --filter website preview      # 本地起服务看构建结果
pnpm --filter website typecheck    # tsc：主题组件与生成脚本
pnpm --filter website test         # 生成脚本单测（node --test）
pnpm --filter website check        # 构建产物检查，见 §7
```

`dev` 与 `build` 启动时先重新生成 `components/`、`cli/`、`design/`、`sdk/kun/`、`sdk/api/` 五个目录（§3），改了仓库 `docs/` 之后重启 `dev` 即可看到；也可以单独运行 `pnpm --filter website generate`。Kun API 参考的结果按 Kun 版本与提交、DocFX 版本、转换脚本缓存在 `website/.cache/kun-api/`，缓存未命中时要重新跑 DocFX（本机 NuGet 缓存里已有 `Kun.*` 包时约 6 秒）。

**离线**：产物不引用任何外部脚本、样式、字体或图片，搜索索引在本地，链接带 `.html` 后缀，任意静态服务器都能直接托管（例如 `python3 -m http.server -d website/doc_build`），不依赖 Rspress 的服务端。页面用绝对路径引用资源，所以不能双击 `file://` 打开。

## 2 目录骨架

按 docs-site.md §6 落地；标「生成」的目录不进 Git、不接受手改（进了 Git 文档门禁即失败），标「后续」的由对应任务补上。

```text
website/
  rspress.config.ts            站点配置：中文、根目录 docs/、部署前缀 DOCS_BASE、Shiki 语言、Mermaid、主题桥接脚本、
                               llms: true（SSG-MD，见 §4a）与 themeConfig.llmsUI（复制为 Markdown）；多版本见 §4；
                               core 使用自研 iframe 预览与 TypeScript checker；其它 API 随 S1-DOCS-002
  package.json  tsconfig.json
  README.md  AGENTS.md
  .config/dotnet-tools.json    DocFX 版本（本地 dotnet 工具）
  .cache/                      生成缓存，不进 Git：kun-repo/（未设 KUN_DOCS_PATH 时的 kun 克隆）、kun-api/（DocFX 构建与结果）
  docs/
    index.mdx                  首页（docs-site.md §2a）
    sitemap.mdx                站点地图（页脚入口）
    _nav.json                  顶部导航：七个部分 +「产品」
    start/                     1 开始：family.md 产品族地图（手写，与仓库 README 的表同内容）、versions.md 版本矩阵（生成：scripts/build-versions.ts，不进 Git）
    components/                2 组件目录（生成：scripts/collect-components.ts）
      platform/<id>.md
      yue/
    sdk/                       3 SDK：dotnet/  frontend/  ai/
      kun/                     「Kun 框架」小节（生成：scripts/sync-kun-docs.ts，按 KunVersion 镜像 kun 仓库）
        with-qian.mdx          唯一的手写页：Qian 对 Kun 的选用与约束
      api/                     API 参考（生成，不进侧栏）：index.md 落地页、kun/（scripts/build-kun-api.ts，DocFX）；
                               后续：dotnet/  frontend/（S1-DOCS-002）
    cli/                       4 CLI 与规格（生成：scripts/sync-specs.ts，来自 component-model.md）
    ops/                       5 运维
    marketplace/               6 市场与 ISV
    design/                    7 设计与路线（生成：scripts/sync-specs.ts，镜像仓库 docs/）
    public/                    原样拷贝的静态文件（logo）
  scripts/
    collect-components.ts      收集组件文件夹 → docs/components/（S1-DOCS-002 前为规划清单的占位页）
    sync-specs.ts              component-model.md → docs/cli/；docs/ → docs/design/
    kun.ts                     读 KunVersion、打开 kun 仓库的 tag v<KunVersion>（KUN_DOCS_PATH 或缓存克隆）
    sync-kun-docs.ts           kun 仓库 v<KunVersion> 的白名单文档 → docs/sdk/kun/（docs-site.md §2b）
    build-kun-api.ts           同版本 Kun.* 包 → DocFX → docs/sdk/api/kun/ 与 docs/sdk/api/index.md
    build-kb-index.ts          构建后：页面源 + .md 产物 → doc_build/kb-index.json 与 sitemap.xml（§4a）
    sources-plugin.ts          把生成器接进 Rspress（config 钩子，在扫描路由之前执行）
    generate.ts                单独运行全部生成器（pnpm --filter website generate）
    repo.ts                    生成脚本共用：仓库坐标、生成目录写入、链接改写
    check-site.ts              构建产物检查
    *.test.ts  fixtures.ts     生成脚本单测；fixtures.ts 在临时目录里搭样例 kun 仓库
    build-kb-index.ts          后续：产出 kb-index.json（S1-DOCS-003）
    build-versions.ts          仓库钉版文件（Directory.Packages.props、csproj、global.json、package.json、pnpm 工作区、deploy/baseline.json）→ docs/start/versions.md
    llms.ts                    llms.txt 的排版：先按 project（Qian → Kun → yue）再按部分分节
  theme/
    index.tsx  index.css       主题覆盖：令牌映射、建设期横幅、版本说明、首页组件
    components/                首页区块、最新动态、版本说明、站点地图
```

## 3 七个部分与内容来源

| # | 部分 | 路径 | 来源 | 进 Git | 版本化 |
|---|---|---|---|---|---|
| 1 | 开始 | `/start/` | 手写；版本矩阵 `/start/versions` 由 `scripts/build-versions.ts` 从钉版文件生成（不进 Git） | 是（`versions.md` 除外） | 否 |
| 2 | 组件目录 | `/components/` | `scripts/collect-components.ts` 生成 | 否 | 是 |
| 3 | SDK | `/sdk/` | 手写；「Kun 框架」小节 `/sdk/kun/` 按 `KunVersion` 从 kun 仓库镜像（`with-qian` 手写）；API 参考 `/sdk/api/` 生成（Kun 已有，`Qian.*` 与前端随 S1-DOCS-002） | 手写页是；`sdk/kun/`（`with-qian.mdx` 除外）与 `sdk/api/` 否 | 是（Kun 小节随 `KunVersion`） |
| 4 | CLI 与规格 | `/cli/` | 从 `docs/prd/specs/component-model.md` 第 2、3、4、7、8、9、10 节同步 | 否 | 是 |
| 5 | 运维 | `/ops/` | 手写 | 是 | 否 |
| 6 | 市场与 ISV | `/marketplace/` | 手写（S3 前只放规则） | 是 | 否 |
| 7 | 设计与路线 | `/design/` | 镜像仓库 `docs/`（ADR、PRD、路线图、变更日志等） | 否 | 否 |

导航用中文名，路径用英文段，页面 slug 用小写短横线。「组件怎么用」只写在第 2 部分，SDK 只写编程模型与 API，两边互相链接。

**生成目录的规则**（`scripts/sync-specs.ts`、`scripts/collect-components.ts`）：

- `design/`：`docs/` 下每个 `.md` 一页，文件名转小写、`README.md` 转 `index.md`（`docs/adr/ADR-015-model-gateway.md` → `/design/adr/adr-015-model-gateway`）。正文里指向 `docs/` 内的相对链接改写为站内路由，指向仓库其他位置的改写为 GitHub 地址；每页开头注明源文件。侧栏顺序与名称由脚本生成的 `_meta.json` 决定。
- `cli/`：按 `component-model.md` 的二级标题拆页，标题整体升一级；章节与页面的对应写在 `CLI_PAGES`，规格改了章节编号要同步这里（找不到章节时构建失败）。
- `components/`：S1-DOCS-002 之前仓库里还没有组件文件夹，脚本按 PRD §7 与组件模型规格 §7.7 的规划清单生成占位页，让首页与其他部分的链接有落点；S1-DOCS-002 把数据源换成各组件文件夹，页面改为 docs-site.md §3 的九节模板。
- `sdk/kun/`（docs-site.md §2b、ADR-021）：读根目录 `Directory.Packages.props` 的 `KunVersion`（缺失、为空或不是 CalVer 都直接失败），从 kun 仓库的 tag `v<KunVersion>` 按白名单（`sync-kun-docs.ts` 的 `KUN_WHITELIST`，即 §2b.2 的表）取文件（Kun 的 ADR 文件名大小写不统一，`ADR-*.md` 与 `adr-*.md` 都收，R58），`AGENTS.md`、`CLAUDE.md` 永不进站。**只镜像、不改写**：每页加镜像横幅（源文件与 tag、「Kun 文档与代码不一致处以代码为准」、改动提到 kun 仓库）与 frontmatter（`project: kun`、`source: idadawn/kun@v<版本>:<路径>`、`updated` 取源文件在该 tag 上的最后提交日期）；slug 一律小写、`.` 与空格转短横线；指向白名单内文件的相对链接改写为站内路由，其余改写为该 tag 的 GitHub 地址。为让 MDX 能编译只做两处机械处理并打警告：正文里不是 HTML 标签的尖括号（`Result<T>`）转义，列数不足的表格行补空单元格。上游问题只告警不失败，源头修复提到 kun 仓库（R59）。侧栏顺序与名称写在脚本里（概览 → 与 Qian 的关系 → 上手 → 模块地图 → 指南 → 安全 → 设计说明与 ADR → 白皮书 → 变更日志与发布说明 → API 参考）。白名单要改，docs-site.md §2b.2 与脚本两处一起改。
- `sdk/api/`：DocFX 从 nuget.org 上同版本的 `Kun.*` 包（tag 上 `src/<名>/<名>.csproj` 的全部项目）的 DLL 与 XML 注释生成 Markdown（`docfx metadata`，`outputFormat: markdown`），脚本再转成站内页面：每个命名空间一页、每个类型一页，xref 改写为站内路由与成员锚点，BCL 类型链到 learn.microsoft.com。DocFX 在 `website/.cache/kun-api/build` 的隔离目录里构建，用存根的 `Directory.Build.*` 与关掉中央包管理的 `Directory.Packages.props` 隔开仓库根的 .NET 构建基线（`NuGet.config` 与 `global.json` 仍用仓库根的）。API 页**不进侧栏**，命名空间页与类型页也不渲染侧栏（frontmatter `sidebar: false`），只有 `/sdk/api/` 与 `/sdk/api/kun/` 两个概览页保留侧栏（R54）：Rspress 把整棵侧栏渲染进每一页，几百个类型进侧栏会让产物膨胀数倍；入口是 SDK 侧栏末尾的「API 参考」、Kun 小节概览与各页之间的链接。将来的 `Qian.*`、前端包与平台 HTTP API 的参考同样放在 `/sdk/api/` 下（R56）。
- 只重写内容有变化的文件、删除多余的旧文件，`dev` 不会因重新生成而反复重启。

**变更日志**：仓库 `docs/CHANGELOG.md` 是唯一来源，镜像为 `/design/changelog`；首页「最新动态」直接读它，取最近 4 条。每条一个二级标题 `## YYYY-MM-DD 标题`，下面一两行说明。

## 4 版本策略

docs-site.md §5：第 2、3、4 部分随 Qian 包族的 SemVer 锁步版本多版本发布，第 1、5、6、7 部分只有当前版。

- 现在还没有发布任何包，三部分都只有开发版（`main`）；这三部分每页顶部显示版本说明（`theme/components/PartNotice.tsx`），其余部分没有。
- 第 3 部分的「Kun 框架」小节与 Kun API 参考不单独选版本：每个 Qian 文档版本内嵌该版本 `KunVersion` 锁定的 Kun 文档（docs-site.md §5、ADR-021 D21.3）。这些页面（`project: kun`）不显示版本说明，版本写在每页横幅里（`v<KunVersion>`），产物检查核对它与 `KunVersion` 一致。升级 Kun 只改 `KunVersion`，文档随下一次构建切换。
- Rspress 的 `multiVersion` 是整站多版本，不能只给三个部分分版本。首个包族版本发布时再定实现方式（候选：三部分各自按版本目录生成 + 版本切换组件），届时一并改本节与 PartNotice。

## 4a 机器可读产物（S1-DOCS-003）

docs-site.md §5.1 是契约，这里只说实现：

- `rspress.config.ts` 的 `llms: true` 开启 Rspress 的 SSG-MD：构建时多跑一遍 `node_md` 环境，用 `react-render-to-markdown` 把每个路由渲染成 Markdown（`<路由>.md`，目录首页为 `index.md`），再按顶部导航分组写出 `llms.txt`（每页一行，摘要取 frontmatter `description`）与 `llms-full.txt`（每页正文）。这不是 HTML 回转，也不是 remark 转换：组件在 `import.meta.env.SSG_MD` 为 true 时可以返回另一份输出，我们的主题组件（首页卡片与页脚、最新动态、站点地图、版本说明）都带这个分支，只输出标题、段落、列表与链接；站内链接指向 `.md`。
- JSX 容器（`Card` 等）内部的 Markdown 里的行内组件不会被渲染，会原样进 `.md`（Rspress `remarkSplitMdx` 只处理顶层段落里的行内 JSX），所以首页卡片正文里的阶段标记写成文字，`Badge` 只放在 JSX 属性里。
- `themeConfig.llmsUI` 在每个文档页标题下放「复制为 Markdown」与「复制 Markdown 链接」；首页是 `pageType: custom`，`HomeHero` 里手动放同一组按钮。`viewOptions` 只留 `markdownLink`，不放外部 AI 服务入口（站内「问 AI」是 S2-DOCS-001）。
- `scripts/build-kb-index.ts` 在 `rspress build` 之后运行（`build` 脚本末尾）：遍历 `website/docs` 的页面源（含生成页），解析 frontmatter（`scripts/repo.ts` 的 `parseFrontmatter`，与 `scripts/check_docs.py` 同一套 YAML 子集），读对应 `.md` 算 sha256，写 `kb-index.json`；再按 frontmatter `updated` 写 `sitemap.xml`（绝对地址 = `DOCS_SITE_URL`（默认 `theme/site.ts` 的 `DEFAULT_SITE_URL`）+ `DOCS_BASE` + 路由）。缺 `.md`、frontmatter 不合法、产物里有没来源的 `.md` 都让构建失败。
- 生成页的 frontmatter 由各生成器补齐：组件占位页与 CLI 页写 `project` / `audience` / `updated`（取源文件最后提交日期，`repoFileDate`），设计文档镜像页合成 `title`（源 frontmatter 或一级标题）、`description`（首段去标记截到 120 字）、`updated`、`source`（`idadawn/qian@main:<路径>`）并保留源文件自带的其它字段（ADR 的 `status`、`date` 等）。

## 5 主题与令牌

- 令牌取值来自工作区包 `@idadawn/yue-tokens`（文档简称 `@yue/tokens`，S1-YUE-004）：`theme/index.css` 第一行导入它的 CSS，其余只把语义令牌（`--yue-primary`、`--yue-background`、`--yue-border` 等）映射到 Rspress 的主题变量；颜色令牌按 yue 规格存成可加透明度的 oklch 分量，映射时写成 `oklch(var(--yue-<role>))`。开发期导入的是包里提交的 `src/index.css`，不需要先构建令牌包。
- 明暗两态：Rspress 在首帧前给 `html` 加 `dark` 类，令牌包的语义层同时认 `html.dark` 与 `html[data-theme="dark"]`（specs/yue.md §3.5），不闪烁，也不需要桥接脚本（S1-DOCS-004 删除）。
- 改令牌值去改 `packages/yue/tokens`（`pnpm --filter @idadawn/yue-tokens tokens:generate` 重新生成 CSS）；docs-site 工作流在令牌包变化时同样触发。

## 6 托管与预览：K3s 静态托管

验收给了 Cloudflare Pages 与 K3s 静态托管两个选项，选 **K3s 静态托管**：仓库私有、文档面向内部团队，入口与任务看板一致（WireGuard 与公司内网），不需要外部账号与另一套访问控制。做法与 `deploy/pm-board` 相同，细节见 [deploy/docs-site/README.md](../deploy/docs-site/README.md)。

- CI（`.github/workflows/docs-site.yml`）：PR 与 `main` 上类型检查、单测、构建（含断链检查）、产物检查、Kun 镜像页的文档门禁；同仓库分支的 PR 发布预览并在 PR 里留链接，合并到 `main` 后发布正式站点，PR 关闭后删除预览。只有改到 `website/`、`docs/`、依赖清单、`Directory.Packages.props`（`KunVersion`）、`deploy/docs-site/` 或该工作流时才触发。
- CI 读 kun 仓库用**只读 deploy key**：私钥存在本仓库的 Actions Secret `KUN_DOCS_DEPLOY_KEY`，公钥加在 `idadawn/kun` 的 Deploy keys（不给写权限）。工作流按 `KunVersion` 检出 tag `v<KunVersion>` 的全部历史到 `.kun/`（镜像页的 `updated` 取每个文件的最后提交日期），检出后不保留凭据。Secret 缺失时构建直接失败；fork 来的 PR 与 Dependabot 拿不到 Actions Secret，文档站不在 fork 上构建，Dependabot 接入时在 Dependabot secrets 里再配一份（R60）。由仓库管理员在本机执行一次：

  ```bash
  ssh-keygen -t ed25519 -N '' -C 'qian docs-site CI (read-only)' -f kun-docs-key
  gh repo deploy-key add kun-docs-key.pub --repo idadawn/kun --title 'qian docs-site CI (read-only)'
  gh secret set KUN_DOCS_DEPLOY_KEY --repo idadawn/qian < kun-docs-key
  rm kun-docs-key kun-docs-key.pub
  ```

- 发布目标是 `docs-site` 分支：根目录是正式站点，`pr-preview/pr-<n>/` 是预览（构建时 `DOCS_BASE=/pr-preview/pr-<n>/`）。分支只保留一个提交（`deploy/docs-site/publish.sh`）。
- 集群里 git-sync 每 60 秒拉取该分支，nginx 提供访问：正式站点 <http://10.78.1.161:30820/>（WireGuard）、<http://192.168.1.211/>（公司内网）；预览在其后加 `pr-preview/pr-<n>/`。

## 7 门禁

CI 的真实浏览器用例在固定摘要的官方 Playwright Linux 镜像内运行，与 yue 视觉门使用同一浏览器和系统字体；源码只读挂载，只有测试报告目录可写。这样避免每次执行都从系统镜像源下载字体和浏览器依赖而耗尽作业时限。全部用例、断言和 15 分钟作业上限保留；升级 Playwright 时须同步文档浏览器与视觉镜像版本。

| 检查 | 内容 | 在哪跑 |
|---|---|---|
| `scripts/check_docs.py` | 禁用词（整词匹配）、表格列数、链接、frontmatter（docs-site.md §4：必填、枚举、长度、日期、镜像页的 `source` 与 `version`、组件页的 `component`）；覆盖 `website/docs`（.md 与 .mdx）、本文件与 `AGENTS.md`，站点源码只查禁用词；站内路由、`/design/` 镜像路由与生成页路由都按规则解析，生成目录本身默认不查（它们的源已在查）；生成目录里的文件进了 Git 即失败。`/sdk/kun/`、`/sdk/api/` 下的路由在生成之前交给构建时的断链检查（手写页 `with-qian` 除外） | docs-and-pm 工作流 |
| `scripts/check_docs.py website/docs/components website/docs/cli website/docs/design website/docs/sdk/kun website/docs/sdk/api website/docs/start/versions.md` | 生成页（含源不在本仓库的 Kun 镜像页与 API 参考、版本矩阵）构建后显式检查一遍，frontmatter 也查（显式给出的生成目录与文件照查） | docs-site 工作流 |
| Rspress 断链检查 | 构建时检查全站（含生成页）的站内链接，断链即构建失败 | docs-site 工作流 |
| `pnpm --filter website check` | 七个部分与首页、404、站点地图齐全；`lang="zh"`；不引用外部资源；站内引用都带部署前缀；搜索索引存在；C# / TypeScript / JSON / YAML / Bash 都有被高亮的代码块；`/sdk/kun/` 与 `/sdk/api/kun/` 下除手写页外每页都有镜像或生成横幅，横幅里的 tag 等于 `v<KunVersion>`（白名单外的页面混进来即失败）；产物总大小不超过预算（`SITE_SIZE_BUDGET_MB`，120 MB，R55），超出时报出最大的目录；机器可读产物（§4a）：`llms.txt`、`llms-full.txt`、`kb-index.json`、`sitemap.xml` 齐全，索引里每页的 `.md` 与 HTML 都在且 sha256 一致、三个清单都收录每页、没有索引外的 `.md`，每页 `.md` 无 JSX / ESM 残留、无空标题空列表项、代码块不少于源页 | docs-site 工作流 |
| `pnpm --filter website test` | 路径映射、链接改写、章节拆分、生成目录写入、组件占位页；Kun：`KunVersion` 缺失或无效、tag 不存在即失败，白名单与排除、slug 与路由、横幅与 frontmatter（`updated` 取文件日期）、链接改写、DocFX 输出转换（含 API 页 `sidebar: false`）；产物体积预算；frontmatter 子集解析、知识库索引与 sitemap、Markdown 产物质量与同步检查；版本矩阵的读取与渲染、llms.txt 分节、产品族页与 README 一致 | docs-site 工作流 |
| `tests/gates/test_check_docs.py` | check_docs.py 的站内路由解析、生成目录跳过、Kun 路由延后解析、禁用词整词匹配、生成文件进 Git 即失败、frontmatter 解析与校验 | dotnet 工作流（门禁自测） |

## 8 写页面

- 手写页放在对应部分的目录下，用 `.md`；要用组件（`Badge`、卡片等）时才用 `.mdx`。
- frontmatter 先写 `title`、`description`（≤ 120 字）、`part`（与目录一致）、`project`、`audience`、`updated`，`scripts/check_docs.py` 会查（docs-site.md §4）；只用 `key: 值`、`key: [a, b]`、缩进 `- 项` 与引号字符串几种写法。手写页的 `project` 都是 `qian`（`kun` 属于 Kun 镜像页，`yue` 属于 yue 组件页）。规划中的能力加 `stage`，S2 及以后的再加 `tag: S2` 等，侧栏会显示阶段标记。完整字段见 docs-site.md §4（校验随 S1-DOCS-003 上线）。
- 站内链接写绝对路由（`/start/hosts`、`/design/adr/adr-016-iclaw-boundary`），不带 `.md` 或 `.html`。
- 卡片等 JSX 容器内部的 Markdown 里不放行内组件（`Badge` 等），阶段标记写成「（S2）」这样的文字；否则 `.md` 产物里会原样残留，产物检查会拦（§4a）。
- 设计文档、规格与变更日志只改仓库 `docs/`，不要改 `website/docs/design/`、`cli/`、`components/`。
- Kun 的文档只改 kun 仓库（提 PR），站内随 `KunVersion` 升级更新；`website/docs/sdk/kun/` 只有 `with-qian.mdx` 是手写的，`sdk/api/` 全部生成。
- 没有的能力不写成已有：S1 建设期的命令与组件按目标形态写，并在页面里说明尚未可用。

## yue 组件文档与交互（S1-YUE-005）

唯一使用入口是本站 `/components/yue/`，不是另一个应用。`packages/yue/core/docs/` 保存用途说明、普通 React 示例与 126 条状态清单；`scripts/yue-docs.ts` 收集页面并从 TypeScript checker 提取全部 141 个公开组件/组合部件。DOM 属性按完整声明去重到站内通用属性页，不能因缺注释或继承来源而遗漏。默认值只取源码与 JSDoc。

`pnpm --filter website build` 先执行 `demos:build`：Rsbuild 将同一份真实组件编译为页面内 iframe，输出到 `docs/public/yue-demo/`；Babel、Monaco、字体与 worker 复制到 `docs/public/yue-vendor/`，仅打开编辑器时加载，随相同站点前缀部署。两目录均为忽略的生成物；不引入第二个服务、文档入口或产品身份。浏览器编辑仅允许已预打包的 React/core/tokens 模块，异常在演示区域显示，重置恢复原始例子。

SSR/SSG-MD 保留用途、源码和完整 API，纯交互 UI 在 Markdown 中只输出说明。SSG-MD 表格的对齐空格在生成后收敛，单元格与 fenced code 内容不变，再生成知识库哈希；全站保持 120 MB 预算（S1-QA-010 按 R55 调整，构成与原因见 docs-site.md §7）。

`pnpm --filter website e2e` 在真实静态产物上逐条执行 126 个场景，包含交互结果、明暗、iframe、打开态 axe、无页面异常，并检查断外网编辑与 HTML/Markdown Props 一致。默认启动本地静态服务；`DOCS_BASE=/pr-preview/pr-N/` 验证前缀，`DOCS_URL=http://目标站点/` 验证已发布站点（`--grep '正式页面交互 smoke'` 可聚焦代表性交互）。CI 在发布前跑完整套件，新增 core 源码也会触发 docs-site 工作流。
