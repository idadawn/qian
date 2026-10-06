# Qian 文档索引

> 真相源分层：产品规范（PRD）、架构决策（ADR）、研究基线、阶段路线各管一事，互相引用不复制。

| 目录 | 内容 | 状态 |
|---|---|---|
| `research/baseline-digest.md` | 调研基线要点汇编：7 个领域（Kun 能力与差距、插件化与 Agent 架构、租户/身份/权限、在线开发、流程、数据/集成/报表、前端与 yue）的全貌、分级结论、难点与待拍板问题 | 草案 |
| `decisions/key-decisions-v0.md` | 关键决策清单 D1–D18、Q1–Q18 默认与补充裁决；2026-10-06 全部获认可并转为 ADR-001…018，此后只作历史记录 | 已认可（历史记录） |
| `adr/` | 架构决策记录 ADR-001…018 + 索引 `adr/README.md`（Status / Context / Decision / Rationale / Consequences / 各阶段影响 / 待确认） | 草案 v1 |
| `prd/` | 产品需求文档：索引 `prd/README.md` + 17 章（01–17）+ 专题 spec | 草案 v1 |
| `prd/specs/plugin-manifest.md` | 插件清单 `qian-plugin.yaml` 专题 spec：字段、JSON Schema、派生物、签名与 digest、反向权限、`capabilities` 条目 | 草案 v1 |
| `prd/specs/yue.md` | yue 组件库专题 spec：关键决策 D1–D16、六层分层、控件注册表契约、MF2 外壳与插槽、S1 最小集验收 | 草案 v1 |
| `ROADMAP.md` | 阶段路线唯一真相源：S1–S4 目标、退出标准、核心交付、里程碑占位、依赖与风险、跨阶段约束 | 草案 v1 |
| `migration/` | 从参考平台到 Qian 的迁移方案 | 草案 v1 |

命名约定：产品 = Qian（乾），底座框架 = Kun，前端组件库 = yue，插件 ID 前缀 = `qian.`。被借鉴的商业低代码产品一律称「参考平台」。
