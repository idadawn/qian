# 默认组合包与组件配置骨架

本目录承载 S1-RT-002 的本地组件内容。`qian.bundle.*` 只有清单及 patch 表面；叶目录只有清单、配置 Schema 与说明，统一带 `x-skeleton: true`，用于审阅默认组合与验证来源，不包含业务 Provider 或虚构二进制入口。

组合包分别装入 platform、gateway、identity、standalone，具体清单见 [profiles](../profiles/README.md)。这些骨架不是新的 seam 设计；叶组件的 Definition、Provider、真实服务键和依赖由各自后续任务提供。待 CLI 安装任务完成后可通过 `qian add` 从目录市场安装对应包，目前仅支持本地消费，不承诺包已发布。
