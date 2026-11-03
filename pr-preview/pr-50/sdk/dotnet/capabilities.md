> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# 能力与函数

## 能力目录

组件把自己的能力登记进**能力目录**（[qian.capabilities](/pr-preview/pr-50/components/platform/qian.capabilities.md)），分四类：命令、查询、事件、实体。

- 注册时提供类型信息，目录据此生成请求与响应的 JSON Schema。
- `GET /api/capabilities?kind=command|query|event|entity&q=` 按调用者权限与当前 scope 的 restrict 过滤，被过滤的项不出现。
- `GET /api/capabilities/openapi.json` 聚合全部已挂载组件的 OpenAPI 3.1（需认证，不等于公开 OpenAPI）。
- Agent 用的工具**定义**由能力目录机械生成，保证 schema 不漂移；**暴露**哪些、怎么描述，经 `agent.expose` 单独策划（[ADR-016 D16.10](/pr-preview/pr-50/design/adr/adr-016-iclaw-boundary.md)）。

## 函数

`function` 表面（Wasm 沙箱内运行的函数）是 S2 可选能力；Agent 生成的代码只落在 `function` 沙箱里（ADR-016 D16.11）。

详见 [PRD §7.2](/pr-preview/pr-50/design/prd/07-s1-foundation.md)。
