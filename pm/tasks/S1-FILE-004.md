---
id: S1-FILE-004
title: qian.files 去骨架：包发布与 qian add files 真实安装、组合包 storage.endpoint、租户配额 IT 与上传策略清单汇总
status: done
stage: S1
area: FILE
wave: 2
priority: P0
type: feature
owner: "claude"
estimate: 3d
depends: [S1-FILE-001, S1-RT-011, S1-TEN-004]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/prd/specs/component-model.md#6.2
updated: 2026-10-23
---

## 背景

S1-FILE-001 建议裁决 6、7（用户 2026-10-22 回复「处理掉」，按建议执行）：qian.files 的 Definition、Provider 与 HTTP 表面已随 #208 合入，清单仍标 `x-skeleton`。#207（S1-RT-033）只给已通过发布与真实安装两门的组件去标（R100，S1-DEC-018 口径），qian.files 两门都未过。去标前 `RegistryComponentBinding` 拒绝该清单，S1-TEN-004 的租户配置写不进 qian.files 条目，配额只能读组件配置；qian.bundle.base 与 qian.bundle.identity-server 的 files 补丁缺 `storage.endpoint`，local 后端挂载会报 `INPUT_INVALID #/storage/endpoint`；上传策略只能由组件配置 `policies[]` 登记，清单 `contributes: qian.files.upload-policy/v1` 还没有 Registry 汇总。

## 验收

- [x] 发布：Qian.Files.Abstractions / Qian.Files / Qian.Files.AspNetCore 随包族锁步发布到正式私有源（`dotnet-release.yml`），`plugins/qian.files` 的 `runtime.nuget` 钉到该版本并带摘要
- [x] E2E（S1-DEC-018 真实安装门，按 R100 口径）：私有源 E2E（`packages/qian-cli/test/private-feed.e2e.mjs`）执行 `qian add data files` 装进 standalone profile：表面进 `.qian`，NuGet 按清单钉版且只从正式私有源还原，lock 记录摘要；宿主按清单入口挂载到 Ready；`qian remove` 逆序撤销。完整的上传与下载 URL 签发（显式 `SingleTenant`）留在 S1-QA-005 验收 2
- [x] 清单：两门通过后移除 `x-skeleton`，`scripts/check_plugin_manifests.py`、Profiles 测试计数与 docs/generated 同步；qian.bundle.base 与 qian.bundle.identity-server 的 files 补丁补上 `storage.endpoint`，platform profile 挂载 qian.files 不再报 `INPUT_INVALID`
- [x] IT：经 S1-TEN-004（`PUT /tenants/{id}/settings/qian.files`）写入的租户配额覆盖组件配置，超限返回 `QUOTA_EXCEEDED`
- [x] IT：贡献方组件在 `metadata/upload-policies.json` 声明的上传策略由 qian.files 从 Registry 预装快照汇总后生效（用途须以声明组件 ID 开头）；与组件配置 `policies[]` 同用途时以配置为准，有用例锁定

## 进度日志

- 2026-10-22 claude：创建任务
- 2026-10-22 claude：来源：S1-FILE-001 建议裁决 6、7，用户 2026-10-22 回复「处理掉」按建议执行（经主会话转达）；编号前查过 main、全部远端与本地分支及其他工作区，S1-FILE-004 未被占用。验收 2 与 S1-QA-005 验收 2 是同一场景：本任务先交付真实安装门，QA-005 在 30 分钟样例层回验
- 2026-10-23 claude：开始
- 2026-10-23 claude：用户裁决（本任务会话）：① 批准锁步发布 0.2.0，时机为本任务第一个代码 PR 合入、main 的 dotnet CI 转绿之后从 main 触发，S1-IDP-015 可共用这次发布；② 验收 2 按 R100 门槛改写：私有源 E2E `qian add data files` 装进 standalone profile，验钉版、摘要、lock、挂载 Ready 与 `qian remove` 撤销；完整的上传与下载 URL（显式 SingleTenant）留在 S1-QA-005 验收 2；③ 上传策略声明放在贡献方组件的 `metadata/upload-policies.json`（v2 规格 §3 与 R37 / R101 口径），不在 plugin.json 加 contributes，验收 5 措辞随之改写
- 2026-10-23 claude：发现 #208 遗留缺陷：清单 provides 含 `Files.Backends:local`，FilesPlugin 却只把 local 后端登记进自己的注册表、没有 `ctx.Provide`，按真实清单装载会被 PluginLoader 以「未注册声明的 provides 服务键」拒绝；测试宿主只给 provides `[Files]`，所以没测出来。本任务补上具名 Provide，测试宿主改按仓库清单的 provides 挂载
- 2026-10-23 claude：调研结论：① 私有源只发过 0.1.0（2026-10-14，35 个包），没有 Qian.Files 三包；现在包族 47 个，0.1.0 不能再发。② `runtime.nuget` 的摘要没有自动化，取包源 .nupkg 的 sha256，发布后人工同步（S1-RT-034 待做）。③ `qian add` 对 x-skeleton 只告警，硬拒绝只在 RegistryComponentBinding。④ 单装 files 会撞 profile 白名单（minimal 不在 policy.profiles）、必填 storage（add 写入 `{}`）和 inject Data，所以 E2E 用 `qian add data files` 装进 standalone，并预置 storage 配置。⑤ 只钉 files 新版本会让 data / events / config 仍钉 0.1.0 而混版，钉版时一并升到 0.2.0
- 2026-10-23 claude：路线：第一个 PR（发布前，代码进 0.2.0）——local 后端补具名 Provide；`DeclaredUploadPolicies` 从 Registry 预装快照汇总 `metadata/upload-policies.json`（只能声明本组件 ID 开头的用途、同组件取最高版本、已登记的策略优先、Registry 不进 Inject 只 TryInject）；新增 `schemas/v2/files-upload-policies.schema.json`；两个基础组合包补 `storage.endpoint`；文档与 docs/generated 同步。合入且 main CI 转绿后发布 0.2.0。第二个 PR（发布后）——钉版与摘要、去 x-skeleton、Profiles 计数与 docs/generated、私有源 E2E、TEN-004 写入 IT，并在真实 Registry 宿主里复验声明汇总
- 2026-10-23 claude：第一个 PR 本机门禁（基于 main 5accddf5d7）：dotnet build Qian.slnx -c Release 0 警告 0 错误；check_deps --no-restore 47 包通过；tests/gates 94 项通过；`scripts/check_plugin_manifests.py` 16 个组件一致；FilesDbContext 无待生成迁移；`qian docs generate` 后 docs/generated 只有 config-catalog 的预期变化（platform 的 files 配置多出 endpoint、policies 描述更新），已一并提交；check_docs 321 个文件、pm validate / build 210 个任务通过；qian-cli profiles.test.mjs 3/3（骨架计数未变）、@qian/surfaces 17/17；全量 dotnet test（QIAN_REQUIRE_DOCKER=1，带 QIAN_OASDIFF_PATH / QIAN_BUF_PATH，--max-parallel-test-modules 4）1332/1332，其中 Qian.Files.Tests 43 例（新增 15 例）
- 2026-10-23 claude：发布：main 49a4ae4e30 那轮 dotnet CI 被 #223 合入取消，改等新头 187c880189（含 #222）的 push run 39827942443 通过后从 main 触发 `dotnet-release` 0.2.0（run 39829674547）：包族 47 个包（含 Qian.Files.Abstractions / Qian.Files / Qian.Files.AspNetCore）推送到正式私有源，verify-feed 与包源消费者冒烟通过
- 2026-10-23 claude：钉版与去骨架：qian.files 的 `Qian.Files.Abstractions` / `Qian.Files` 钉 0.2.0 并带包源 .nupkg 的 sha256；qian.data / qian.events / qian.config 一并升到 0.2.0（只升 files 会让同一项目里 0.1.0 的 Provider 配 0.2.0 的 Abstractions）。清单移除 x-skeleton，`FilesComponentManifestTests` 改断言无骨架标记、同一锁步版本、带摘要；platform 骨架警告 19 → 18（ProfileTests、profiles.test.mjs）；docs/generated 只有 files 条目的骨架提示与模块图状态列变化
- 2026-10-23 claude：私有源 E2E 拦下的问题：① `qian add files` 被组件自身六个 command 工具判「命令能力未登记」：CLI 离线校验从不给能力目录，带 command 工具的组件都装不上（qian.tenancy 去骨架时同样会撞上）；② files 宿主要显式选择 `ITenantContext` 才能挂载；③ CLI 把 configSchema 缺省值（quota 10 GiB）合并进沿用的预置配置、按包 ID 排列 NuGet 引用、`--purge` 连同被沿用的预置条目一起删除，E2E 断言照实际语义改写。另：发布后本机第一次跑 E2E 秒失败，是本机 NuGet HTTP 缓存里的旧版本列表，换全新 `NUGET_HTTP_CACHE_PATH` 即过，CI 的新 runner 不受影响
- 2026-10-23 claude：建议裁决（待用户确认）：CLI 离线校验（安装、远端解析、doctor）把组件自身命名空间（`<组件ID>/…`）的 command 工具绑定视为该组件已登记的能力。理由：tools 由 `[Capability]` 派生，仓库清单门禁核对与代码一致，平台宿主按制品绑定时仍以完整目录复核；跨组件 command 引用照旧报错。只改 CLI（`component-validation.ts`），C# / TS 校验器语义与一致性夹具不动。备选：在两端校验器里把本组件 tools 的 command 绑定像工具名那样自动加入目录，但会放宽平台宿主的完整目录复核
- 2026-10-23 claude：验收 2 证据：私有源 E2E 两个场景本机通过（QIAN_FEED_TOKEN 用本机 gh 令牌）。standalone 场景预置 disabled 的 files 条目给出 storage，`qian add data files` 后逐包核对钉版、只从私有源还原、摘要与清单和 lock 一致；宿主补 Kun.EventBus 实现、显式 `AsyncLocalTenantContext` 后，两个 Provider 挂载 Ready，local 后端按沿用的配置建好对象根目录；`remove files data --purge` 后项目文件复原、patch 清空。宿主不在构建时校验整棵依赖树：能力管道还要当前主体与数据会话，归 S1-QA-005
- 2026-10-23 claude：验收 4、5：Files 测试宿主加平台模式（`FilesPlatform`）——真实 Registry（pg）按制品字节绑定组件（qian.files 用仓库里已去骨架的文件夹，完整目录校验须给出六个能力 ID），TEN-004 运营端点挂在独立宿主，平台超管经 authz 的平台登录角色授予（Qian.Authz 对 Qian.Files.Tests 开 InternalsVisibleTo）。新用例：平台运营 GET 设置 → 只改 quota → PUT `/api/tenancy/tenants/{A}/settings/qian.files`（整体替换，revision 0 → 1）；Registry 在 A 的 scope 挂 qian.files 实例并接管 A 的请求，A 配额 1 MiB，上传 768 KiB 后再申请 512 KiB 返回 422 `QUOTA_EXCEEDED`（剩余 262144 字节），B 仍按组件配置的 10 MiB。声明策略用例改为替身快照与真实 Registry 两种预装快照各跑一遍。租户 scope 实例与 global 实例可以并存；TEN-004 整体替换，写入的配置要带全 storage 与 policies（已写进 README）
- 2026-10-23 claude：第二个 PR 本机门禁（基于 a85b85230b，推送前同步 main）：dotnet build Qian.slnx -c Release 0 警告 0 错误；check_deps --no-restore 47 包通过；tests/gates 94 项通过；`scripts/check_plugin_manifests.py` 16 个组件一致；`qian docs generate` 后 docs/generated 无漂移；check_docs 322 个文件、pm validate / build 211 个任务通过；qian-cli 单元测试 95/95（含新增离线校验用例与 profiles 计数）；私有源 E2E 两个场景 2/2（55 秒）；全量 dotnet test（QIAN_REQUIRE_DOCKER=1，带 QIAN_OASDIFF_PATH / QIAN_BUF_PATH，--max-parallel-test-modules 4）1334/1334，其中 Qian.Files.Tests 45 例（新增 TEN-004 写入 IT 与声明策略的真实 Registry 变体）
- 2026-10-23 claude：验收 1–5 均有证据（0.2.0 发布、钉版去骨架、私有源 E2E、TEN-004 写入 IT、真实 Registry 声明策略）；CLI 离线校验 command 工具的建议裁决待用户确认
- 2026-10-23 claude：用户在本任务会话直接回复「认可建议裁决，转绿后合并」：CLI 离线校验把组件自身命名空间的 command 工具视为已登记能力的做法定案
- 2026-10-23 claude：合并前 main 前进到 c75486a858（#224、#226–#229，与本 PR 无文件重叠），本地同步后在合并树（与 GitHub 合并结果同为 ad15b253）上重跑门禁：dotnet build -c Release 0 警告 0 错误；全量 dotnet test 1389/1389；qian-cli 95/95、@qian/surfaces 17/17；私有源 E2E 2/2；tests/gates 94 项、清单门禁、check_docs、pm validate、check_deps 通过，docs/generated 无漂移。PR 上 docs-site「构建与检查」两次都在 15 分钟上限被取消：第一次卡在 `playwright install --with-deps` 装系统依赖，第二次装依赖花了近 10 分钟，组件 e2e 跑到 66/161 条（全部通过）时超时；建站与断链检查通过，与本 PR 改动无关
- 2026-10-23 claude：用户 2026-10-23 在本任务会话确认置 done（「合并，然后置 done」）
