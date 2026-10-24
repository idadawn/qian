---
id: S1-DATA-006
title: PG 方言的组件形态：plugins/qian.data.dialect.pg 与 Data.Dialects:pg 派生
status: doing
stage: S1
area: DATA
wave: 2
priority: P1
type: feature
owner: "cody"
estimate: 1d
depends: [S1-DATA-003, S1-RT-014, S1-RT-033]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/prd/specs/component-model.md#6.1
updated: 2026-10-24
---

## 背景

S1-DATA-003 以宿主装配交付 PG 方言（AddQianPgDialect）。规格要求方言是独立组件 qian.data.dialect.pg，向 Data seam 的注册表子服务按名称注册（R38），清单 provides 写 Data.Dialects:pg 并由源生成器派生。当前源生成器遇到具名 Provide(key, name, …) 直接报「具名 seam 注册须由独立声明支持」，派生能力归 S1-RT-014（[ExtensionPoint] / [Contributes]），故本任务依赖它。

## 验收

- [ ] 组件文件夹 `plugins/qian.data.dialect.pg`：清单的 `provides` 由源生成器从 `Apply` 里的注册派生为 `Data.Dialects:pg`，Plugin.Tool check 无漂移
- [ ] IT：平台模式下挂载 / 卸载该组件，`GET /api/data/meta/types` 的方言列表随之出现 / 消失；注册句柄撤销即注销（R38）
- [ ] UT：宿主装配 `AddQianPgDialect()` 的行为不变（库优先，ADR-019）

## 进度日志

- 2026-10-15 claude：创建任务
- 2026-10-16 claude：R94：具名 Provide 的 provides 派生未随 S1-RT-014 交付（生成器仍报 QIANSDK002），转 S1-RT-033，depends 补上
- 2026-10-24 cody：开始：三个前置均已完成；核验 PG 方言的具名派生、组件挂卸与宿主装配兼容性。
