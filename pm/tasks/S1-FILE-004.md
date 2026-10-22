---
id: S1-FILE-004
title: qian.files 去骨架：包发布与 qian add files 真实安装、组合包 storage.endpoint、租户配额 IT 与上传策略清单汇总
status: doing
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

- [ ] 发布：Qian.Files.Abstractions / Qian.Files / Qian.Files.AspNetCore 随包族锁步发布到正式私有源（`dotnet-release.yml`），`plugins/qian.files` 的 `runtime.nuget` 钉到该版本并带摘要
- [ ] E2E（S1-DEC-018 真实安装门，PRD 7.7 独立模式）：从正式私有源 `qian add files` 真实安装到空白 ASP.NET Core 项目（显式 `SingleTenant`），完成上传与下载 URL 签发；场景与 S1-QA-005 验收 2 相同，本任务交付后由 QA-005 在样例层回验
- [ ] 清单：两门通过后移除 `x-skeleton`，`scripts/check_plugin_manifests.py`、Profiles 测试计数与 docs/generated 同步；qian.bundle.base 与 qian.bundle.identity-server 的 files 补丁补上 `storage.endpoint`，platform profile 挂载 qian.files 不再报 `INPUT_INVALID`
- [ ] IT：经 S1-TEN-004（`PUT /tenants/{id}/settings/qian.files`）写入的租户配额覆盖组件配置，超限返回 `QUOTA_EXCEEDED`
- [ ] IT：组件清单 `contributes: qian.files.upload-policy/v1` 声明的上传策略由 Registry 汇总后登记进 `IUploadPolicyRegistry`，与组件配置 `policies[]` 同用途时的处理有用例锁定

## 进度日志

- 2026-10-22 claude：创建任务
- 2026-10-22 claude：来源：S1-FILE-001 建议裁决 6、7，用户 2026-10-22 回复「处理掉」按建议执行（经主会话转达）；编号前查过 main、全部远端与本地分支及其他工作区，S1-FILE-004 未被占用。验收 2 与 S1-QA-005 验收 2 是同一场景：本任务先交付真实安装门，QA-005 在 30 分钟样例层回验
- 2026-10-23 claude：开始
