---
id: S1-DOCS-005
title: Kun 框架小节：按锁定版本镜像 kun 仓库文档并生成 API 参考
status: todo
stage: S1
area: DOCS
wave: 2
priority: P0
type: feature
owner: ""
estimate: 3d
depends: [S1-DOCS-001]
refs:
  - docs/adr/ADR-021-product-family-structure.md#D21.4
  - docs/prd/specs/docs-site.md#2b
  - docs/adr/ADR-001-kun-relationship.md#D1.8
  - docs/prd/07-s1-foundation.md#7.1
updated: 2026-10-07
---

## 背景

产品负责人要求 Kun 体现在文档中。首版规格写的「Kun 外链不重写」作废：Kun 没有自己的文档站，读者与 SDK 读者重叠。按 ADR-021 D21.4，真相源仍在 kun 仓库 `docs/`，Qian 文档站按 `Directory.Packages.props` 的 `KunVersion` 锁定版本把白名单内容镜像为第 3 部分第一节「Kun 框架」`/sdk/kun/`，并用 DocFX 生成 `Kun.*` API 参考；Qian 只手写一页「与 Qian 的关系」。S1-DOCS-001（PR #3）里 `sdk/index.md` 与 `start/hosts.md` 的「Kun 的 API 本站不重写」措辞一并改掉。

## 验收

- [ ] `website/scripts/sync-kun-docs.ts`：读 `KunVersion`（缺失即失败，不回退 `main`；属性尚不存在时由本任务先加入，取当前锁定的 Kun 版本）→ 以只读凭据取 kun 仓库 `v<KunVersion>` → 按 docs-site.md §2b.2 白名单生成 `website/docs/sdk/kun/`；白名单外文件不进站；slug 小写短横线
- [ ] 每页横幅（来源路径 + 版本 + 「以代码为准（ADR-001 D1.8）」 + 指向 `/sdk/kun/with-qian`）与 frontmatter（`project: kun`、`part: sdk`、`version`、`source`、`updated` 取源文件最后提交日期、`title` / `description` 自动提取）；站内相对链接按 §2b.3 改写
- [ ] DocFX 从锁定版本 `Kun.*` 包生成 API 参考挂 `/sdk/api/kun/`，与 `/sdk/api/dotnet/` 并列（与 S1-DOCS-002 共用 DocFX 流水线，谁先做谁搭）
- [ ] 手写 `/sdk/kun/with-qian`：Kun 模块 ↔ 平台插件 / seam 映射表、canonical API 选择（§07 7.1「插件化定位」）、Qian 的强制装配与禁用项、补丁包状态
- [ ] 导航「SDK」下第一项为「Kun 框架」；首页「构建应用」栏新增 Kun 条目；`sdk/index.md`、`start/hosts.md` 不再写「本站不重写」
- [ ] 门禁：`sdk/kun/`（除 `with-qian.mdx`）手改即失败；镜像 tag ≠ `KunVersion` 即失败；镜像页过 `scripts/check_docs.py`
- [ ] CI 凭据：对 kun 仓库的只读 deploy key 或细粒度 token 以 Secret 提供，本地可用 `KUN_DOCS_PATH` 指向本机克隆；README「托管与预览」写明
- [ ] UT：同步脚本对「`KunVersion` 缺失」「tag 不存在」「白名单外文件」各有一个失败用例

## 进度日志

- 2026-10-07 claude：创建任务（ADR-021 D21.4，docs-site.md §2b）
