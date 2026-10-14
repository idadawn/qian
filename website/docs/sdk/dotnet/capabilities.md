---
title: 能力与函数
description: 能力目录收录命令、查询、事件、实体四类能力，按调用者权限与 scope 过滤；function 表面（Wasm）为 S2 可选。
part: sdk
project: qian
audience: [developer, agent]
updated: 2026-10-14
---

# 能力与函数

## 能力目录

组件把自己的能力登记进**能力目录**（[qian.capabilities](/components/platform/qian.capabilities)），分四类：命令、查询、事件、实体。

- 组件在 `Apply` 中用 `ctx.ProvideCapability<TRequest, TResponse>()` 登记能力，绑定携带源生成的契约，请求与响应的 JSON Schema 由类型信息派生；目录只列这些绑定里、当前 scope 已挂载且 Ready 的能力。
- `GET /api/capabilities?kind=command|query|event|entity&q=` 按调用者权限与当前 scope 的 restrict 过滤，被过滤的项不出现；响应信封 `{ items, ttlMs, cacheScope: "private" }` 的冻结 Schema 是 `contracts/v0/capability-catalog.schema.json`。
- `POST /api/capabilities/{plugin}/{name}/invoke` 是通用执行入口：先重做同样的过滤，再按条目 `inputSchema` 校验请求体（不符返回 400 `INPUT_INVALID`，`errors[]` 带 JSON Pointer），然后走组件自己的命令管道；写类能力可带 `Idempotency-Key`（UUID）让重复请求返回首次成功响应而不再执行（同键请求仍在执行时返回 409，失败不留回执，只读能力忽略该键）。
- `GET /api/capabilities/openapi.json` 聚合全部已挂载组件的 OpenAPI 3.1（需认证，不等于公开 OpenAPI）。
- Agent 用的工具**定义**由能力目录机械生成，保证 schema 不漂移；**暴露**哪些、怎么描述，经 `agent.expose` 单独策划（[ADR-016 D16.10](/design/adr/adr-016-iclaw-boundary)）。

## 函数

`function` 表面（Wasm 沙箱内运行的函数）是 S2 可选能力；Agent 生成的代码只落在 `function` 沙箱里（ADR-016 D16.11）。

详见 [PRD §7.2](/design/prd/07-s1-foundation)。
