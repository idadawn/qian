> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 模型网关

**阶段**：S1 可选（P1），由 [qian.model-gateway](/pr-preview/pr-122/components/platform/qian.model-gateway.md) 交付；接线与运维见[模型网关接线](/pr-preview/pr-122/ops/model-gateway.md)。

## 是什么

- 一个 OpenAI 兼容端点调所有模型：推理层 GPUStack 作为本地上游注册进 Higress，外部供应商同样注册为上游；Qian 不自写模型代理层，也不绕过 Higress 直连推理层（[ADR-015](/pr-preview/pr-122/design/adr/adr-015-model-gateway.md)）。
- 调用方用**逻辑模型名**（如 `chat-default`），映射到具体供应商与模型的配置放在 Git 里。
- 按租户的限流与计量经 Higress 的 consumer 与统计插件完成；计量表从第一天带 `tenant_id + project_id` 双键。
- 租户级模型路由是「产品默认 + 租户覆盖」，租户的坏配置回退到默认（fail-open）；自带模型端点只能注册为 Higress 上游，应用不能直连。

## 发一条请求

```bash
curl "$MODEL_GATEWAY/v1/chat/completions" \
  -H "Authorization: Bearer $MODEL_GATEWAY_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model": "chat-default", "messages": [{"role": "user", "content": "用一句话介绍 Qian"}]}'
```

`MODEL_GATEWAY` 与 `MODEL_GATEWAY_KEY` 由运维提供（见[模型网关接线](/pr-preview/pr-122/ops/model-gateway.md)）。应用经租户 API Key 调用、计量进统一用量账本的方案待 S3 定（ADR-015 D15.9）；S1–S2 只保证契约形状与双键约定一致。

## 分阶段

| 阶段     | 内容                           |
| ------ | ---------------------------- |
| S1（可选） | 上游注册、Provider Adapter、逻辑模型目录 |
| S2     | 模型目录与按用途路由                   |
| S3     | 租户可见的模型门户、成本与配额预算告警          |
| S4     | Higress 托管每租户 MCP 逻辑门面       |
