---
id: S1-DATA-002
title: 多副本迁移协调、分布式 ID（Guid v7 / 雪花租约）与序号服务
status: todo
stage: S1
area: DATA
wave: 1
priority: P0
type: feature
owner: ""
estimate: 3d
depends: [S1-DATA-001]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/prd/07-s1-foundation.md#7.17
updated: 2026-10-06
---

## 背景

`MigrateAsync` 前取 `pg_advisory_lock(hash(schema))`，K8s 迁移 Job 先行（`--migrate-only`），多副本启动时只校验不迁移；默认 Guid v7，数值 id 用雪花、WorkerId 经 Redis 租约 `qian:snowflake:worker:{n}` 分配；`ISequenceService.NextAsync(ruleCode, scopeKey)` 规则 DSL `prefix + yyyyMMdd + seq(6)`，PG `UPDATE ... RETURNING` 原子取号。

## 验收

- [ ] IT：两个副本同时启动并执行迁移，只有一个执行 DDL、另一个等待后校验通过
- [ ] IT：100 并发取号同一序号规则无重复无跳号；规则修改后从 1 重新开始
- [ ] IT：雪花 WorkerId 在 3 个副本间互不相同，副本重启后租约回收复用

## 进度日志

- 2026-10-06 planner：创建任务
