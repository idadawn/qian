# 默认组合包与组件配置骨架

本目录承载 S1-RT-002 的本地组件内容。`qian.bundle.*` 只有清单及 patch 表面；尚未交付 Provider 的业务叶目录只有清单、配置 Schema 与说明，带 `x-skeleton: true`，用于审阅默认组合与验证来源，不包含业务 Provider 或虚构二进制入口；Provider 已交付并通过启动或安装验收的组件去掉该标记（如 platform 的六个平台组件，以平台生产宿主的装配与集成测试为证据，R144 ③）。

组合包分别装入 platform、gateway、identity、standalone、minimal，具体清单见 [profiles](../profiles/README.md)。`qian.bundle.minimal` 是已实现的无代码空组合包，不含业务骨架。其它骨架不是新的 seam 设计；叶组件的 Definition、Provider、真实服务键和依赖由各自后续任务提供。待 CLI 安装任务完成后可通过 `qian add` 从目录市场安装对应包，目前仅支持本地消费，不承诺包已发布。
