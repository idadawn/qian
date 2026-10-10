---
id: S1-FILE-001
title: Qian.Files 核心：租户前缀键、配额、上传策略与预签名 API
status: todo
stage: S1
area: FILE
wave: 2
priority: P0
type: feature
owner: ""
estimate: 5d
depends: [S1-RT-001, S1-TEN-004, S1-AZ-004, S1-AUD-001, S1-EVT-001, S1-KUN-006, S1-KUN-009]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/prd/specs/component-model.md#6.2
  - docs/adr/ADR-019-library-first-packages.md#2
updated: 2026-10-10
---

## 背景

组件 `qian.files`（`Qian.Files.Abstractions` / `ctx.Files`）基于 Kun 新线 `IFileStorage`：`IQianFileService`、`file_objects` 表、存储键 `tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}` 服务端生成、配额读 7.3（`QUOTA_EXCEEDED`）、`UploadPolicy` 由清单 `contributes: qian.files.upload-policy/v1` 声明、MIME 服务端嗅探、`ContentDispositionPolicy = attachment` 不可关闭；`POST /api/files/uploads:presign`（TTL ≤ 15 分钟）、`POST /files/{id}:complete`、`GET /files/{id}/download-url`（TTL ≤ 5 分钟、经 7.5）、`DELETE`（软删）；事件 `files.object.created/deleted.v1`；下载审计；旧线以 Banned API 禁用。退出标准点名能力。

## 验收

- [ ] IT：租户 A 的下载 URL 对 B 的文件 id 返回 404；任何文件的 `storage_key` 都以 `tenants/{tenant_id}/` 开头
- [ ] IT：上传 `text/html` 文件后下载响应头为 `Content-Disposition: attachment`，且无配置项可改变
- [ ] IT：配额 10 MB 时第 11 MB 上传返回 `QUOTA_EXCEEDED`；软删后配额释放
- [ ] IT：预签名 URL 过期后 PUT 失败；`complete` 时 sha256 不符返回错误并标记 `quarantined`
- [ ] IT：`files.presign-upload` 调用产生审计与 `files.object.created.v1` 事件；`files.delete` 标 `destructive` + `confirm`

## 进度日志

- 2026-10-06 planner：创建任务
