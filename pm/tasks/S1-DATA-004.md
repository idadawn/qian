---
id: S1-DATA-004
title: Filter AST、运算符矩阵与 IRecordHook 契约
status: done
stage: S1
area: DATA
wave: 3
priority: P0
type: feature
owner: "cody"
estimate: 2d
depends: [S1-DATA-001]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/migration/README.md#4.2
  - docs/adr/ADR-011-metadata-dynamic-data.md
  - docs/prd/specs/data-contracts.md
updated: 2026-10-14
---

## 背景

第 3 波 S2 契约冻结：Filter AST `{ logic, groups: [{ logic, conditions: [{ field, op, value | sysVar }] }] }`，运算符 `eq ne like notLike isNull notNull in notIn ge gt le lt between`，矩阵 `{ fieldType: [op] }` 发布为 JSON Schema，五处共用；数据规则策略契约（主键策略、乐观锁、逻辑删、业务主键）；`IRecordHook` before / after 契约与 `qian.record.changed.v1` 信封；S2 SQL 生成只接受参数化 + 标识符白名单。

## 验收

- [x] UT：Filter AST 示例通过 JSON Schema 校验；`between` 用于布尔字段被矩阵拒绝
- [x] UT：`qian.record.changed.v1` 信封与 `IRecordHook` 接口随 `Qian.Data.Abstractions` 发布并通过 PublicAPI 分析器
- [x] 文档：契约写入 specs 并被 7.5 数据权限、§08 高级查询 / 流程条件引用

## 进度日志

- 2026-10-06 planner：创建任务
- 2026-10-14 cody：开始：按用户指派认领；串行在 KUN-010 后完成 Filter AST、运算符矩阵与记录 Hook 契约。
- 2026-10-14 cody：Filter AST、13运算符×10字段类型矩阵、15系统变量、数据规则和Hook/记录信封已交付；专项53项、全仓573项测试零失败零跳过，39项门禁与30包依赖检查通过；等待PR CI与合并。
- 2026-10-14 cody：完成：PR #71 在最终CI全绿后合并（c386261f）；53项专项与573项全仓测试零失败零跳过，39项门禁和实际NuGet打包验证通过。实现分支与worktree已清理。
- 2026-10-14 claude：修正：qian.record.changed.v1 改按 Qian.Events 模型——[IntegrationEventId] 与 RegisterRecordEvents 改为登记载荷 RecordChangedPayload，信封只由写入端生成一层；RecordChangedEvent 改为 after Hook 视图，经 FromEnvelope 从解析后的信封与类型化载荷派生并核对租户、主体，EnsureConsistentContext 移到载荷。线上信封、payload Schema 与 fixture 不变（规格未冻结 C# 登记类型，不另开 DEC）；Qian.Data.Abstractions 新增引用 Qian.Events.Abstractions。专项 75 项、Events 16 项、CRM 28 项、Data 40 项零失败
