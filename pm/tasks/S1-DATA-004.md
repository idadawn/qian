---
id: S1-DATA-004
title: Filter AST、运算符矩阵与 IRecordHook 契约
status: todo
stage: S1
area: DATA
wave: 3
priority: P0
type: feature
owner: ""
estimate: 2d
depends: [S1-DATA-001]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/migration/README.md#4.2
  - docs/adr/ADR-011-metadata-dynamic-data.md
updated: 2026-10-06
---

## 背景

第 3 波 S2 契约冻结：Filter AST `{ logic, groups: [{ logic, conditions: [{ field, op, value | sysVar }] }] }`，运算符 `eq ne like notLike isNull notNull in notIn ge gt le lt between`，矩阵 `{ fieldType: [op] }` 发布为 JSON Schema，五处共用；数据规则策略契约（主键策略、乐观锁、逻辑删、业务主键）；`IRecordHook` before / after 契约与 `qian.record.changed.v1` 信封；S2 SQL 生成只接受参数化 + 标识符白名单。

## 验收

- [ ] UT：Filter AST 示例通过 JSON Schema 校验；`between` 用于布尔字段被矩阵拒绝
- [ ] UT：`qian.record.changed.v1` 信封与 `IRecordHook` 接口随 `Qian.Data.Abstractions` 发布并通过 PublicAPI 分析器
- [ ] 文档：契约写入 specs 并被 7.5 数据权限、§08 高级查询 / 流程条件引用

## 进度日志

- 2026-10-06 planner：创建任务
