> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# 产品族

三个名字、三层，不是一个大单仓（[ADR-021](/pr-preview/pr-50/design/adr/adr-021-product-family-structure.md)）。对外一句话：**Qian 平台基于 Kun 框架构建，前端使用 yue 组件库**。Kun 不是 Qian 的子项目（它有自己的下游与发布节奏），Qian 也不是 Kun 的扩展（它有自己的运行时、插件与产品面）。

```text
yue   前端层   React 组件库与外壳（@yue/*）
Qian  平台层   Qian.Runtime + 平台插件 + profile + qian CLI + 目录市场
Kun   框架层   .NET 10 微服务框架（Kun.*），Qian 的底座
```

| 名字             | 层       | 是什么                                                                                                                                                                  | 仓库                                                                                                                  | 版本 · 许可                        | 文档在哪                                                                                                               |
| -------------- | ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| Kun            | 框架层（底座） | .NET 10 云原生微服务框架，32 个 `Kun.*` 模块：Mediator、Outbox、Saga、多租户与 RLS、网关与 S2S、存储、缓存、可观测                                                                                     | [`idadawn/kun`](https://github.com/idadawn/kun)（2026-10-08 从 CavaiLAB 迁回，旧地址重定向）                                    | CalVer · MIT · 正式包在 nuget.org  | [Kun 框架](/pr-preview/pr-50/sdk/kun/index.md)小节（按锁定版本镜像）与 [Kun API 参考](/pr-preview/pr-50/sdk/api/kun/index.md)      |
| Qian           | 平台层     | 基于 Kun 的多租户 PaaS：`Qian.Runtime` + 平台插件 + profile + `qian` CLI + 目录市场                                                                                                 | [`idadawn/qian`](https://github.com/idadawn/qian)（本仓库）                                                              | SemVer 锁步 · Apache-2.0         | 本站全部七个部分；设计文档在[设计与路线](/pr-preview/pr-50/design/index.md)                                                           |
| yue            | 前端层     | React 组件库与外壳，`@yue/*`                                                                                                                                                | 本仓库（pnpm workspace，S1 第 2 波建立）                                                                                      | SemVer（Changesets）· Apache-2.0 | [组件目录](/pr-preview/pr-50/components/yue/index.md)（活演示 + props 表）与[前端 SDK](/pr-preview/pr-50/sdk/frontend/index.md) |
| `qian-plugins` | 市场      | 官方目录市场 `.qian/marketplace.json`                                                                                                                                      | `idadawn/qian-plugins`（S1 私有，待建）                                                                                    | —                              | [市场与 ISV](/pr-preview/pr-50/marketplace/index.md)                                                                  |
| iClaw          | 兄弟系统    | Agent 运行时与 AI 资产控制面；Qian 不写 Agent loop（[ADR-016](/pr-preview/pr-50/design/adr/adr-016-iclaw-boundary.md)）                                                            | [`idadawn/iclaw`](https://github.com/idadawn/iclaw)                                                                 | —                              | AI SDK 的 [Agent 运行时对接](/pr-preview/pr-50/sdk/ai/agent-runtime.md)                                                  |
| 部署与模型服务        | 运维      | GitOps 部署仓库（[ADR-017](/pr-preview/pr-50/design/adr/adr-017-deployment.md)）；GPUStack + Higress 模型服务（[ADR-015](/pr-preview/pr-50/design/adr/adr-015-model-gateway.md)） | [`idadawn/iclaw-deploy`](https://github.com/idadawn/iclaw-deploy)、[`idadawn/MaaS`](https://github.com/idadawn/MaaS) | —                              | [运维](/pr-preview/pr-50/ops/index.md)                                                                               |

## 四样东西把它们关联起来

不建大单仓、不建 submodule 元仓库（ADR-021 D21.2）：

1. **一个 GitHub 所有者 `idadawn`**：产品族全部仓库同一所有者，一个 GitHub Packages 命名空间、一段源配置。Kun 正式版的主渠道是公开的 nuget.org。
2. **一个文档站**：就是本站。Kun 是第 3 部分 SDK 的第一节，内容按 Qian 锁定的 Kun 版本从 kun 仓库镜像。
3. **一张版本矩阵**：[版本矩阵](/pr-preview/pr-50/start/versions.md)由仓库钉版文件生成——哪个 Qian 配哪个 Kun、.NET SDK、Node、K3s 基线。
4. **一个看板**：跨仓库工作在 Qian 仓库的 `pm/` 跟踪，kun 仓库的 PR 回链到任务。

Kun 的版本只在 `Directory.Packages.props` 的 `KunVersion` 写一次；升级 Kun = 改这一处、跑 TCK、文档自动跟随。要改 Kun 的代码或文档，去 kun 仓库提 PR，Qian 的任务里只回链。本页与仓库 README 的「产品族」表同一内容，改一处要同步另一处（有单测核对）。
