> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# CLI 与规格

本部分从[组件模型规格](/pr-preview/pr-50/design/prd/specs/component-model.md)同步生成，规格是唯一真相源；运行时模型、seam 规则与安全约束见规格全文。

| 页面                                                                 | 规格章节 | 内容                                                         |
| ------------------------------------------------------------------ | ---- | ---------------------------------------------------------- |
| [qian 命令](/pr-preview/pr-50/cli/commands.md)                       | §9   | qian CLI 的十个命令、npx 一键形式、安装过程、幂等与回滚、管理面端点。                  |
| [组件清单 plugin.json](/pr-preview/pr-50/cli/plugin-json.md)           | §3   | .qian-plugin/plugin.json 的示例、字段逐项表与 JSON Schema 骨架。        |
| [组件文件夹布局](/pr-preview/pr-50/cli/layout.md)                         | §2   | 组件文件夹的目录树、表面一览、无代码组件与带代码组件。                                |
| [声明式表面的文件格式](/pr-preview/pr-50/cli/surfaces.md)                    | §4   | skills、agents、commands、hooks、MCP、tools、ui 与 patch 的文件格式。   |
| [profile、组合包与 patch](/pr-preview/pr-50/cli/profile-patch.md)       | §7   | 分层模型、qian.profile.yml、组合包、qian.patch.yml 与 dump-config 输出。 |
| [目录市场 marketplace.json](/pr-preview/pr-50/cli/marketplace-json.md) | §8   | .qian/marketplace.json 的示例、字段表、多市场优先级与社区发现。                |
| [校验与 TCK](/pr-preview/pr-50/cli/validation.md)                     | §10  | 清单与表面校验器、运行时语义 TCK 与 CI 门禁。                                |
