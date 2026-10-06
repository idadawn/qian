---
id: S1-FILE-002
title: 文件 Provider：oss / s3 / local 与 patch 切换
status: todo
stage: S1
area: FILE
wave: 2
priority: P0
type: feature
owner: ""
estimate: 3d
depends: [S1-FILE-001, S1-CFG-002, S1-RT-002]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/adr/ADR-020-component-model-v2.md#2
updated: 2026-10-06
---

## 背景

ADR-020 D20.4 的样例 seam：`Qian.Files.Oss`（现网对象存储）/ `Qian.Files.S3`（MinIO / S3 兼容）/ `Qian.Files.Local`（仅开发与单节点）三个 Provider 只依赖 `Qian.Files.Abstractions`，装载其一；Provider 选择 = profile patch 替换 `qian.files` 条目的 `provider` 配置，切换不影响已有键；凭据经 secretRef；`qian.files.provider/v1` 扩展点。

## 验收

- [ ] TCK：三个 Provider 对同一组文件操作 fixture（上传、预签名、下载、删除）结果一致
- [ ] E2E：用 `--patch` 把 `provider` 从 `local` 换成 `oss` 后所有消费者不变，已有存储键仍可读
- [ ] CI：Provider 包的传递依赖中不出现其他 Provider 与 `Qian.Files` 以外的实现包；凭据只以 secretRef 出现

## 进度日志

- 2026-10-06 planner：创建任务
