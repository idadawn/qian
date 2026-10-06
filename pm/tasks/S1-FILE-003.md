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
depends: [S1-FILE-001, S1-SCH-001]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/prd/07-s1-foundation.md#7.15.2
updated: 2026-10-06
---

## 背景

`file_uploads` 分片会话：`POST /uploads:init`、`PUT /uploads/{id}/parts/{n}`、`POST /uploads/{id}:complete`、`GET /uploads:lookup?sha256=`（秒传）；yue `Upload` 组件消费其契约；软删后保留期满经 7.13 调度物理删除。

## 验收

- [ ] E2E：yue `Upload` 以分片 + MD5 上传 200 MB 文件至 OSS provider，秒传命中返回已有 id
- [ ] IT：分片会话中断后可续传；`complete` 校验全部分片与总 sha256
- [ ] IT：软删文件在保留期满后由调度作业物理删除并发出 `files.object.deleted.v1`

## 进度日志

- 2026-10-06 planner：创建任务
