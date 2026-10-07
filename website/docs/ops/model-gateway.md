---
title: 模型网关接线
description: 把 GPUStack 注册为 Higress 的 OpenAI 兼容上游，供应商与逻辑模型名映射入 Git，绑定配额、限流与统计插件。
part: ops
project: qian
audience: [operator]
stage: S1
updated: 2026-10-07
---

# 模型网关接线

**阶段**：S1 可选（P1）。开发者怎么调用见[模型网关](/sdk/ai/gateway)。

Qian 的模型网关并入团队现有的 Higress / MaaS 基础设施，复用 `maas-*` 路由、网关专用 Redis HA、MaaS 日志与 AI 大盘，不另起一套（[ADR-015](/design/adr/adr-015-model-gateway)）。

## 接线步骤

1. 推理层使用 GPUStack，作为 OpenAI 兼容的本地上游注册进 Higress。
2. `ai-proxy` 的供应商与逻辑模型名（如 `chat-default`）映射写进 Git，经部署仓库 PR 生效，不直改线上。
3. 绑定 `ai-quota`、`ai-token-ratelimit`、`ai-statistics` 插件；补价格表与账单脚本。
4. 外部供应商的出网走白名单放行。

## qian.model-gateway 做什么

[qian.model-gateway](/components/platform/qian.model-gateway) 是控制面 adapter（可选独立 Pod）：发现 GPUStack 的模型（`/v1/models`）、健康探测与上下线，并填写 `model-capabilities` 契约（模态、工具调用、上下文长度、数据驻留、隐私、时延、成本档）形成逻辑模型目录。

## 边界

- 不自写模型代理层，不绕过 Higress 直连推理层。
- S1 不把 SSO / S2S 交给 Higress；业务 API 的身份边界仍在 [qian.gateway](/components/platform/qian.gateway)。
- 先接 LLM 与 Embedding；ASR / TTS / Rerank 后续按需评估。
