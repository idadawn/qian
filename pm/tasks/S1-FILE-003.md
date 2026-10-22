---
id: S1-FILE-003
title: 分片上传、MD5 秒传与软删保留期清理作业
status: todo
stage: S1
area: FILE
wave: 2
priority: P1
type: feature
owner: ""
estimate: 3d
depends: [S1-FILE-001, S1-SCH-001, S1-YUE-008]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/prd/07-s1-foundation.md#7.15.2
  - docs/decisions/key-decisions-v0.md
updated: 2026-10-22
---

## 背景

`file_uploads` 分片会话：`POST /uploads:init`、`PUT /uploads/{id}/parts/{n}`、`POST /uploads/{id}:complete`、`GET /uploads:lookup?sha256=`（秒传）；yue `Upload` 组件消费其契约；软删后保留期满经 7.13 调度物理删除。

## 验收

- [ ] E2E（R108：与原 S1-YUE-008 验收 2 同义，合并于此）：yue `Upload` 以分片 + MD5 上传 200 MB 文件至 OSS provider，秒传命中返回已有 id
- [ ] IT：分片会话中断后可续传；`complete` 校验全部分片与总 sha256
- [ ] IT：软删文件在保留期满后由调度作业物理删除并发出 `files.object.deleted.v1`
- [ ] IT（S1-FILE-001 建议裁决 4 并入）：租户级文件数上限与字节配额同一来源（7.3 租户配置覆盖优先、组件配置兜底），达到上限后再申请上传返回 `QUOTA_EXCEEDED`；软删后释放

## 进度日志

- 2026-10-06 planner：创建任务
- 2026-10-16 claude：S1-DEC-024 / R108：原 S1-YUE-008 验收 2（Upload 分片上传经 7.7、秒传命中返回已有 id）与本任务验收 1 同义，合并于此；depends 加 S1-YUE-008（Upload 组件按本任务背景的分片契约对 mock 先行交付）。PRD 7.7 与题名写「MD5 秒传」而契约以 sha256 查询，实现时统一口径
- 2026-10-17 cody：S1-YUE-008 对接登记：Upload 按本任务四个端点以 SHA-256 完成组件 mock 验收；夹具 OpenAPI 位于 packages/yue/data/contracts/upload.openapi.json，init 响应 uploadId/partSize/已确认 uploadedParts（序号从 1 开始），complete/lookup 返回文件 id/name/size/contentType。该文件是组件夹具草案，真实响应接线与契约冻结由本任务复核；未宣称真实上传 E2E 完成。
- 2026-10-22 claude：S1-FILE-001 建议裁决 4（用户 2026-10-22 回复「处理掉」，按建议执行）：PRD 7.7「配额（字节数、文件数）」里的租户级文件数上限 FILE-001 未做（只有租户字节配额与用途级 `maxCount`），并入本任务，新增验收第 4 条；实现时沿用 FILE-001 字节配额的来源顺序与计数口径（申请即预占、过期与软删释放、隔离计入）。经 TEN-004 写入 qian.files 条目要等 S1-FILE-004 去骨架，此前 IT 可按组件配置验收
