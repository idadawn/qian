# AGENTS.md — 给编码代理的入口说明

这个仓库是 **Qian（乾）** 平台的设计与开发仓库。Qian 是基于 Kun（.NET 10 微服务框架）构建的、以「一切皆插件」组织的 PaaS：先做面向开发人员的技术底座（S1），再做低代码构建（S2）、面向客户的应用市场（S3），终态是 AI 原生 Agent 平台（S4）。

## 先读什么

1. `docs/README.md`：文档总索引。
2. `docs/adr/ADR-020-component-model-v2.md`：组件模型（一切皆插件、声明式表面、`qian add` 一键安装、目录市场）。它取代了 ADR-002/003 中被标记的条目。
3. `docs/prd/specs/component-model.md`：开发者规格，实现时以它为准。
4. `docs/prd/07-s1-foundation.md`：S1 每个组件的范围、技术落点与验收。
5. `pm/README.md`：任务怎么管。

## 任务流程（必须遵守）

- 任务在 `pm/tasks/<id>.md`，一个任务一个文件。开始前认领：
  `python3 scripts/pm.py set <id> --status doing --owner <你的代理名> --note "开始"`
- 结束时把「验收」里完成的项改为 `[x]`，改状态（`review` 或 `done`），并运行
  `python3 scripts/pm.py build`，把 `pm/index.html`、`pm/tasks.json`、`pm/STATUS.md` 一起提交。
- 提交信息末尾带任务 id，如 `feat(runtime): ctx.Effect 逆序释放 [S1-RT-001]`。
- 遇到需要人拍板的问题：不要自作主张改设计。用
  `python3 scripts/pm.py add --stage S1 --area DEC --type decision --wave 0 --title "..."`
  新建决策任务并把当前任务标 `blocked`。
- 不要修改 `pm/index.html`、`pm/tasks.json`、`pm/STATUS.md`（它们是生成物）。

## 写文档与代码的硬规则

- 文档一律中文。文档里不得出现参考平台的品牌名（统一写「参考平台」），也不要写对标项目的名字（写「对标项目」）。`python3 scripts/check_docs.py` 会检查禁用词、表格列数与链接，提交前跑一遍。
- Markdown 表格单元格里的反引号内若有竖线，写成 `\|`。
- 组件设计先回答四个问题：文件夹里有哪些表面、装进哪个 profile、属于哪个 seam 的哪个角色、能否 `qian add`。凡是把能力绑死在特权内核、DI 扩展方法或数据库开关上的写法都是倒退。
- 术语：平台插件（不是「系统插件」）、运行时 `Qian.Runtime`（不是「内核」）、profile（不是「可部署单元」）、`.qian-plugin/plugin.json`（不是 `qian-plugin.yaml`）、租户 scope patch（不是「租户开关」）。
- 三层口径（ADR-021）：Kun = 框架层（底座 SDK）、Qian = 平台层、yue = 前端层；写「Qian 基于 Kun 构建」，不写「Kun 是 Qian 的子项目」或「Qian 是 Kun 的扩展」。Kun 的代码与文档改动去 kun 仓库提 PR，Qian 任务里只回链；文档站的「Kun 框架」小节（`website/docs/sdk/kun/`）是按 `KunVersion` 锁定版本生成的镜像，不要手改（唯一手写页 `with-qian.mdx` 除外）。
- Kun 版本单点：`Directory.Packages.props` 的 `<KunVersion>` 属性（S1-DOCS-005 加入，文档站的「Kun 框架」小节与 Kun API 参考按它生成）。全部 `Kun.*` 的 PackageVersion 引用 `$(KunVersion)`，`NuGet.config` 用 packageSourceMapping 把 `Kun.*` 映射到 nuget.org；Kun 正式版从 nuget.org 还原，只有需要 Kun 预发布版时才加 GitHub Packages 源（文档站的 DocFX 构建也复用此配置，届时 docs-site 工作流也要配该源的读凭据，R62）；之后升级 Kun 只改这一处（ADR-021 D21.3）。
- 代码部分遵循 ADR-019：每个有代码的组件发 `Qian.<X>.Abstractions` + Provider 包；组件库只依赖 `Qian.Runtime`，不依赖 `qian.registry` / `qian.lifecycle`。
- 两道 .NET 门禁会自动拦截（细节见 `tests/README.md`）：
  - 写了 `<IsPackable>true</IsPackable>` 且包名以 `Qian.` 开头的项目就是 Qian 包，必须带 `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`，新公共 API 未登记即编译失败；
  - `scripts/check_deps.py` 要求 Qian 包只依赖 `Qian.Runtime*`、`Qian.Plugin.Sdk*` 与 `*.Abstractions`，依赖 Provider 或 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities` 即失败（组合包 `Qian.Bundle.*` 除外）。
- 错误响应一律是 `contracts/v0/error-detail.schema.json` 的 ErrorDetail：用 `Qian.Runtime.Errors` 定义与登记错误码（大写下划线、全局唯一、在本组件 Definition 包里定义），HTTP 宿主装 `AddQianErrors()` + `UseQianErrors()`；不要自己拼 ProblemDetails 或 `{ error: ... }`。
- 改了 `contracts/v0/` 的 Schema 必须同时改 fixture 与 `Qian.Runtime.Errors`，`dotnet test` 会校验 Schema、fixture 与内置错误码目录 `error-codes.json` 是否一致。
- 集成测试用 `tests/Qian.Testing.Containers` 的 fixture（PostgreSQL 16 / Redis 7 / RabbitMQ / Apollo 替身），不要各自再写一套容器启动代码。
- 已有决策不要重开。D1–D18 已获认可；ADR-019 / 020 / 021 均为 Accepted，ADR-019 / 020 的待拍板项已拍板并记录在各 ADR §7（对应 `S1-DEC-*` 已关闭）；仓库归属已拍板：产品族统一在 `idadawn`，Kun 从 CavaiLAB 迁入（ADR-021 §7，执行见 S1-PM-003）。`S1-DEC-001`–`S1-DEC-009` 已关闭；`S1-DEC-010`（contracts/v0 的 8 条默认裁决）待产品负责人拍板，拍板前按 `contracts/v0/README.md` 第 8 节的默认值实现。文档里标「已裁决（R{n}）」的是复核时的默认裁决，有异议时新建决策任务而不是直接改。

## 仓库布局

```text
docs/        设计文档：decisions（决策清单）、adr、prd（含 specs）、migration、research、ROADMAP.md
contracts/   冻结契约 v0：S2S claim、事件信封、ErrorDetail 与错误码目录、能力条目的 JSON Schema + fixture（见 contracts/v0/README.md）
packages/    .NET 组件库（NuGet 包），如 Qian.Runtime.Errors
pm/          任务与看板（见 pm/README.md）
scripts/     pm.py、check_docs.py、check_deps.py（依赖图门禁）
tests/       Testcontainers fixture、集成测试、门禁自测（见 tests/README.md）
website/     文档站（Rspress 2.0，见 website/README.md 与 website/AGENTS.md）；components/、cli/、design/、sdk/kun/（with-qian.mdx 除外）、sdk/api/ 为生成目录
deploy/      开发期工具的 K3s 清单：pm-board/（看板）、docs-site/（文档站）
.github/     CI：文档门禁 + 任务校验 + 看板新鲜度；dotnet 构建、门禁与测试；文档站构建、预览与发布
Qian.slnx、global.json、Directory.Build.props / .targets、Directory.Packages.props、NuGet.config
             .NET 构建基线：net10.0、警告即错误、中央包版本、PublicAPI 门禁
```

代码目录 `packages/` 已建立（一个包一个目录：`packages/<包名>/<包名>.csproj`，测试在 `tests/<包名>.Tests/`）；`plugins/`、`profiles/`、`samples/` 在 S1 第一波任务中建立，布局以 `docs/prd/specs/component-model.md` §2 与 §7 为准。

## 本地检查清单

```bash
python3 scripts/check_docs.py && python3 scripts/pm.py validate && python3 scripts/pm.py build
```

改了文档站（`website/`）或想看设计文档在站内的效果时再跑（需要 Node、pnpm、.NET SDK 与 kun 仓库的读权限：`KUN_DOCS_PATH` 指向本机 kun 克隆，见 `website/README.md` §1）：

```bash
pnpm install && pnpm --filter website typecheck && pnpm --filter website test && pnpm --filter website build && pnpm --filter website check
python3 scripts/check_docs.py website/docs/sdk/kun website/docs/sdk/api
```

改了 .NET 代码时再跑（集成测试需要 Docker）：

```bash
dotnet build Qian.slnx -c Release && python3 scripts/check_deps.py --no-restore && python3 -m unittest discover -s tests/gates && dotnet test --solution Qian.slnx -c Release --no-build
```
