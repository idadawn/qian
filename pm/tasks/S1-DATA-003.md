---
id: S1-DATA-003
title: 基准类型与 PG 方言组件、数据连接档案
status: todo
stage: S1
area: DATA
wave: 2
priority: P0
type: feature
owner: ""
estimate: 3d
depends: [S1-DATA-001, S1-CFG-002]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/adr/ADR-019-library-first-packages.md#2
updated: 2026-10-06
---

## 背景

`BaseDataType`（int / bigint / decimal / float / datetime / varchar / text / bool / json / uuid，系统类不可删）、`DbDialect`（postgres）、`DialectTypeMapping`（长度 / 精度 / 区间校验）作为种子数据与 `GET /api/data/meta/types`；方言为独立组件 `Qian.Data.Dialect.Pg`（数据包，`qian.data.dialect/v1` SPI）；`DataConnection`（凭证只存 secretRef、`POST /connections/{id}:test`、详情不回传密钥）；`StandardField` 由基类固化。MySQL 方言本期不做。

## 验收

- [ ] UT：`GET /api/data/meta/types` 返回基准类型、方言与映射，系统类不可删除
- [ ] IT：`POST /connections/{id}:test` 对可达 / 不可达库分别返回成功 / 结构化错误；详情接口不回传密钥
- [ ] UT：`DialectTypeMapping` 对超出长度 / 精度 / 区间的字段定义拒绝

## 进度日志

- 2026-10-06 planner：创建任务
