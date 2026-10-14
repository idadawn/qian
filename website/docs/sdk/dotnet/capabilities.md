---
title: 能力与函数
description: 能力目录收录命令、查询、事件、实体四类能力，按调用者权限与 scope 过滤；function 表面（Wasm）为 S2 可选。
part: sdk
project: qian
audience: [developer, agent]
updated: 2026-10-15
---

# 能力与函数

## 能力目录

组件把自己的能力登记进**能力目录**（[qian.capabilities](/components/platform/qian.capabilities)），分四类：命令、查询、事件、实体。

- 组件在 `Apply` 中用 `ctx.ProvideCapability<TRequest, TResponse>()` 登记能力，绑定携带源生成的契约，请求与响应的 JSON Schema 由类型信息派生；目录只列这些绑定里、当前 scope 已挂载且 Ready 的能力。
- 平台宿主调用 `services.AddQianCapabilities()` 后，登记时就生成目录条目并预编译输入校验器。不合格的契约（输入含 `tenantId` 等租户字段、Schema 无法可靠校验）抛 `CapabilityContractException`，带组件 ID、能力短名与 JSON Pointer（如 `qian.sample.crm/customer.export#/inputSchema/properties/tenantId`），组件挂载失败；问题在登记时暴露，不会留到第一次调用。
- `GET /api/capabilities?kind=command|query|event|entity&q=` 按调用者权限与当前 scope 的 restrict 过滤，被过滤的项不出现；响应信封 `{ items, ttlMs, cacheScope: "private" }` 的冻结 Schema 是 `contracts/v0/capability-catalog.schema.json`。
- `POST /api/capabilities/{plugin}/{name}/invoke` 是通用执行入口，规则见下节。
- `GET /api/capabilities/openapi.json` 聚合全部已挂载组件的 OpenAPI 3.1（需认证，不等于公开 OpenAPI）；写类能力的路径列出 `Idempotency-Key` 参数与 `Idempotent-Replayed` 响应头。
- Agent 用的工具**定义**由能力目录机械生成，保证 schema 不漂移；**暴露**哪些、怎么描述，经 `agent.expose` 单独策划（[ADR-016 D16.10](/design/adr/adr-016-iclaw-boundary)）。

## 通用执行入口

`POST /api/capabilities/{plugin}/{name}/invoke` 先重做与目录相同的过滤（不可见与不存在一样 404），再按条目 `inputSchema` 校验请求体（不符返回 400 `INPUT_INVALID`，`errors[]` 带 JSON Pointer），然后走组件自己的命令管道，租户、授权与审计照常生效。

写类能力可以带幂等键，只读能力（`sideEffect: read`）忽略它、每次都执行：

| 头 | 方向 | 含义 |
|---|---|---|
| `Idempotency-Key` | 请求 | UUID 文本，可带引号；多值、空值、全零或非 UUID 返回 400 `BAD_REQUEST` |
| `Idempotent-Replayed: true` | 响应 | 这次是回放：响应体与首次成功响应逐字节相同，没有再执行 |

同一租户、同一主体、同一能力、同一键在 24 小时内的重复请求都不会再执行。409 分两种：

- `CONFLICT`：同键而输入不同，或同键请求仍在执行。改用新的键，或等前一个请求结束再重试。
- `CAPABILITY_REPLAY_UNAVAILABLE`：同键请求已经执行成功，但回执没有保存响应体，无法回放。能力的 `dataClassification` 为 `pii`、`finance` 或 `secret` 时不保存，响应超过组件配置 `receiptMaxBytes`（默认 64 KiB）时也不保存。应通过查询确认结果，不要换新键重试，换键会再执行一次。

执行失败不留回执，同一个键可以带正确的输入重试。服务端在执行后、写回执前崩溃时，5 分钟租约到期后的重试会再执行一次，所以写类能力仍要在 `inputSchema` 里带业务幂等键（`idempotencyKey`）。

## 函数

`function` 表面（Wasm 沙箱内运行的函数）是 S2 可选能力；Agent 生成的代码只落在 `function` 沙箱里（ADR-016 D16.11）。

详见 [PRD §7.2](/design/prd/07-s1-foundation)。
