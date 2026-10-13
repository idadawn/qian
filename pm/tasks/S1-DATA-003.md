---
id: S1-DATA-003
title: 基准类型与 PG 方言组件、数据连接档案
status: doing
stage: S1
area: DATA
wave: 2
priority: P0
type: feature
owner: "claude"
estimate: 3d
depends: [S1-DATA-001, S1-CFG-002]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/adr/ADR-019-library-first-packages.md#2
updated: 2026-10-14
---

## 背景

`BaseDataType`（int / bigint / decimal / float / datetime / varchar / text / bool / json / uuid，系统类不可删）、`DbDialect`（postgres）、`DialectTypeMapping`（长度 / 精度 / 区间校验）作为种子数据与 `GET /api/data/meta/types`；方言为独立组件 `Qian.Data.Dialect.Pg`（数据包，`qian.data.dialect/v1` SPI）；`DataConnection`（凭证只存 secretRef、`POST /connections/{id}:test`、详情不回传密钥）；`StandardField` 由基类固化。MySQL 方言本期不做。

## 验收

- [x] UT：`GET /api/data/meta/types` 返回基准类型、方言与映射，系统类不可删除
- [x] IT：`POST /connections/{id}:test` 对可达 / 不可达库分别返回成功 / 结构化错误；详情接口不回传密钥
- [x] UT：`DialectTypeMapping` 对超出长度 / 精度 / 区间的字段定义拒绝

## 进度日志

- 2026-10-06 planner：创建任务
- 2026-10-14 claude：开始
- 2026-10-14 claude：实现 10 个系统基准类型种子与目录（IDataTypeCatalog）、方言 SPI qian.data.dialect/v1 与 Data.Dialects 注册表、PostgreSQL 方言包 Qian.Data.Dialect.Pg（映射数据 + Npgsql 探测）、按租户隔离的 IDataConnectionRegistry（data_connections 迁移、口令只存租户 secretRef、使用点解析）与 Qian.Data.AspNetCore 端点。验收证据：MetaTypesEndpointTests 返回 10 个类型与 pg 方言映射并通过目录 Schema、删除 int/varchar/uuid 均 409 DATA_TYPE_PROTECTED；ConnectionEndpointTests 在真实 PostgreSQL 上可达库 200、端口关闭 422 UNREACHABLE、错误口令/缺失库/缺失 schema 422 REJECTED、越权密钥 403，详情与列表不含口令且无 password 成员；DataTypeContractTests 与 PG 映射用例拒绝超出长度/精度/小数位/取值区间的字段定义
