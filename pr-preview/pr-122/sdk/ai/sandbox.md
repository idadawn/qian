> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 沙箱

**阶段**：S4，尚未开始。

- Qian 不写沙箱。S4 对接 CubeSandbox（E2B 兼容 API）隔离执行不可信或 Agent 生成的代码（[ADR-016 D16.2](/pr-preview/pr-122/design/adr/adr-016-iclaw-boundary.md)）。
- 在此之前，第三方代码不进共享进程：按信任等级限定运行形态，函数类代码走 Wasm 沙箱（`function` 表面，S2 起），界面走浏览器隔离（[组件模型规格](/pr-preview/pr-122/design/prd/specs/component-model.md) §11）。
- Agent 生成的代码只落在 `function` 沙箱里（ADR-016 D16.11）。
