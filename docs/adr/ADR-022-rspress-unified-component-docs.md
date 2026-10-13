---
title: 统一 Rspress 组件文档与验收
status: Accepted
date: 2026-10-14
decision_id: D22
supersedes: ADR-013 D13.1 的 Storybook 选型；修订 D13.18
---

# ADR-022 统一 Rspress 组件文档与验收

## 1 背景与问题（Context）

S1-YUE-005 按原验收交付了 51 个组件入口、manifest 和 Storybook；平台文档已使用 Rspress 2.0.23。两套文档入口使使用者需要切换导航、搜索和预览。2026-10-14 产品负责人明确要求「统一使用 Rspress，不再 Storybook」，并要求先评估能力。本 ADR 记录这次已授权修订，不重新讨论其它前端选型。

## 2 决策（Decision）

- **D22.1 唯一文档与演示体系**：Qian、Kun、yue 使用现有 `website/` Rspress 站点。yue 页面归 `/components/yue/`；按产品负责人补充的组件页参考组织预览、源码、安装、用法、组合结构、场景示例与 API，全部在现有 Rspress 一个入口中完成；移除 Storybook 应用、CSF、依赖、命令与 CI 入口，不保留内部 Storybook。组件文档源仍在组件目录，不手写生成目录。
- **D22.2 测试独立**：Rspress 承担文档和演示；Vitest 浏览器模式、SSR 测试、Playwright、axe 与 Lighthouse 承担原有质量门禁。共用普通 React 示例，迁移 Storybook 状态与交互覆盖；可交互组件必须在 Rspress 实际构建页面上完成操作与结果断言，发布后复验代表性交互，不把删除测试或仅渲染视为完成替换。
- **D22.3 完整 API**：React Props 从真实 TypeScript 类型生成，清单的 JSON Schema 继续负责机器可序列化契约。使用插件或底层解析器时都必须有属性完整性检查，缺失函数、继承属性或组合部件即失败。配置、示例与迁移验收以 [文档站规范 §3.1](../prd/specs/docs-site.md#31-yue-组件文档与演示) 为准。
- **D22.4 任务边界**：重新打开 S1-YUE-005，负责 core 文档接入、测试迁移与 Storybook 清理。S1-DOCS-002 继续依赖它，复用接线并完成通用收集器及平台、TS、.NET、HTTP API 文档；不得反向新增依赖造成循环。本次交付为评估与任务/规格修订，替换代码尚待实施。

## 3 理由与能力评估（Rationale）

**结论：Rspress 能满足目标，但默认 API 插件和在线编辑器不能不经适配就作为验收结果。** 无需为了替换文档工具重写组件或更换现有测试框架。

评估日期为 2026-10-14，基线是本仓库 Rspress 2.0.23、React 19.3.0、TypeScript 6.0.3。npm 公开元数据确认 `plugin-preview`、`plugin-api-docgen`、`plugin-playground` 均存在 2.0.23；preview/playground 的 React peer 为 ≥18，core peer 为 ^2.0.10；api-docgen 的 TypeScript peer 为 ^6.0.3，与当前工作区匹配。

| 需求 | 证据与结论 | 实施约束 |
|---|---|---|
| React 组件活演示 | 官方 preview 支持 MDX 与本地 TSX 文件；隔离试构建直接引用现有 Button、Dialog 成功，浏览器按钮计数 0→1、弹层打开/关闭通过 | 全量组件、Tailwind v4、Portal、明暗隔离仍需迁移验收；iframe 模式需独立配置样式与构建 |
| 完整 Props 表 | 2.0.23 默认 API 表只显示 Button 的 className/testId，Dialog 根表为空；底层 react-docgen-typescript 2.4.0 能解析 variant/size/loading/disabled 与 open/defaultOpen/onOpenChange | 默认过滤会排除 node_modules 声明，并隐藏多数无注释属性；调整过滤、补源码 JSDoc，必要时直接用解析结果生成表，逐组件核对完整性 |
| 文档与机器输出 | 同一试构建成功生成 index.html、index.md、llms.txt、llms-full.txt；现有主站已有导航、搜索与主题 | 对 API、代码块、演示说明检查 HTML/SSG-MD 一致；纯交互控件不得污染 Markdown |
| 在线编辑 | 官方 playground 支持浏览器编译，可预打包依赖；不支持任意本地模块导入，也不执行 TS 类型检查 | 示例独立 typecheck；显式登记允许的依赖，不能把任意源路径当成运行时模块 |
| 离线部署 | 官方 playground 默认 Babel 与 Monaco 使用 CDN，支持配置资源地址 | 编译器、编辑器、worker 资源同源托管并懒加载；断外网后实测编辑；本次未把该项记为已通过 |
| a11y、交互、视觉测试 | 仓库已有独立 Vitest、Playwright、axe 和 Lighthouse；这些门禁不依赖文档框架 | 迁移原 126 条 Storybook 用例的语义与状态覆盖，保留现有 SSR/键盘/E2E；Rspress 构建页面及预览 iframe 加入测试 |

**试验方法**：在仓库外的临时 npm 工程安装上述固定版本的 core、preview、api-docgen；MDX 以 `file` preview 引用仓库真实 Button/Dialog 组合示例，API entries 指向组件源码并使用 core 的 tsconfig；执行 `rspress build`，静态服务器加载产物，检查点击/弹层及生成 Markdown；再直接调用相同版本 `react-docgen-typescript.withCustomConfig(...).parse(...)` 对比解析属性与默认表。检查发布包 `plugin-api-docgen/dist/index.js` 的 propFilter 和 generateTable 确认过滤原因。试验未修改组件源码，未验证全量 51 入口、最终主题样式、离线编辑器及部署；这些保持为任务未勾选项。

## 4 备选方案（Alternatives）

- **Rspress + Storybook 双入口**：原实现可复用测试，但不符合此次产品负责人要求，不再采用。
- **只用 Rspress、删除独立测试**：文档构建不能证明键盘、焦点、SSR 与可访问性，拒绝降低门禁。
- **由 manifest 直接代替 Props 表**：manifest 只描述可序列化属性，不能覆盖函数、ref 与 ReactNode，不能满足完整 API 文档要求。

## 5 后果与代价（Consequences）

统一读者入口与版本、搜索、主题、llms 产物。新增工作集中在示例接线、Props 完整性、在线编辑器本地资源、测试迁移和旧工具清理。全站仍遵守 100 MB 产物预算，组件靶场的 Lighthouse 预算保持原口径；不能把编辑器打入每页首屏。

## 6 对各阶段的影响

- S1：YUE-005 回到 todo，保留已合并组件与 manifest 的验收记录；新增 Rspress 接入与清理验收，全部通过后才恢复 done。DOCS-002 复用成果，继续完成更广的文档管线。
- S2–S4：新增组件沿用普通 React 示例、Rspress 文档与独立测试，不再引入 CSF 或 Storybook。

## 7 待确认事项

工具统一方向已由产品负责人明确授权。Props 适配、离线编辑器和全量页面属于实施验收，不另设选型审批；若实测发现必须改变产品或安全边界的问题，再按任务流程记录具体决策。

## 8 参考

- [产品负责人指定的组件页参考](https://ui.shadcn.com/docs/components/aria/alert-dialog)：借鉴页面编排、可操作预览、对应源码与场景示例，示例实现使用本仓库 API。

- [Rspress 官方 preview](https://rspress.rs/zh/plugin/official-plugins/preview)：本地文件预览、iframe 独立构建与样式。
- [Rspress 官方 playground](https://rspress.rs/zh/plugin/official-plugins/playground)：编辑器、模块限制及 Babel/Monaco 配置。
- [Rspress 官方 api-docgen](https://rspress.rs/zh/plugin/official-plugins/api-docgen)：解析入口与 TypeScript 配置。
- [Rspress api-docgen 2.0.23 发布源码](https://unpkg.com/@rspress/plugin-api-docgen@2.0.23/dist/index.js)：本次验证的过滤行为。
- [原前端决策](ADR-013-frontend-yue.md)、[文档站规范](../prd/specs/docs-site.md)、[YUE-005](../../pm/tasks/S1-YUE-005.md)、[DOCS-002](../../pm/tasks/S1-DOCS-002.md)。
