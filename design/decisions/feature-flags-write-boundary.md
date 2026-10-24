> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 特性开关写入与发布边界

> 本页由构建脚本镜像自仓库 [`docs/decisions/feature-flags-write-boundary.md`](https://github.com/idadawn/qian/blob/main/docs/decisions/feature-flags-write-boundary.md)，请修改源文件，不要改本页。

对应任务：S1-CFG-004；决策：S1-DEC-031。2026-10-23 产品负责人确认方案 A：保存草稿，由 Portal 人工发布。

## 问题与证据

PRD §7.8 已确定 `IFeatureFlags` 从 `qian.common.flags` 读取 `flag.<name>` 规则，`flags.set` 标为 `write` + `confirm`，但没有确定该命令是保存待发布规则，还是立即发布生效。

- `packages/Qian.Config.Abstractions/ConfigDefinition.cs` 的 Config seam 只有只读读取与变更令牌；对应 AGENTS.md 明确其只读边界。现有 Apollo Provider 没有 Portal 管理写端。
- `tests/Qian.Testing.Containers/RealApolloFixture.cs` 的 `PublishAsync` 直接调用隔离测试环境的 AdminService 写配置并发布。此入口没有 Portal 的 Consumer Token 授权，不能作为生产管理命令的写端。
- [Apollo 2.5.2 官方 OpenAPI 文档](https://github.com/apolloconfig/apollo/blob/v2.5.2/docs/zh/portal/apollo-open-api-platform.md) 将配置项编辑与 Namespace 发布分为两个接口；第三方 Token 必须获授目标 Namespace 权限。发布作用于 Namespace，不能把一次规则编辑自动等同于仅发布该规则。
- PRD 把配置审核/回滚编排留给 S3；不能为补一个能力标记而暗中引入完整配置发布治理，也不能返回成功却不说明规则尚未生效。

## 共同实施边界

求值端继续使用只读 Config 子端口，标准配置源与 Apollo 均可读取规则；默认值、租户白名单与稳定百分比独立于权益和租户 scope 挂载。

写端作为可选的平台管理组件 `qian.config.flags`，有自己的声明与可逆生命周期，可由 `qian add` 安装；宿主用受信任配置固定 Portal 地址、App、环境、Cluster 与 `qian.common.flags`。调用参数仅接受合法开关名与规则，不接受任意地址、Namespace、Token 或发布人。Token 经 secretRef 解析，凭据不写入输出或审计。未装写端时明确不可用，不回退本地临时配置或 AdminService。

写入必须具备平台管理权限、`write` + `confirm` 元数据和可验证审计；审计不含规则明文。沿用 Apollo 自身授权与编辑锁，不伪造操作人或绕过其发布约束。既有业务 profile 不自动获得 Portal 管理权限，真实凭据与环境配置不在本次代码任务中凭空创建。

## 方案

| 方案   | `flags.set` 行为                            | 生效时点                          | 范围与代价                                                    |
| ---- | ----------------------------------------- | ----------------------------- | -------------------------------------------------------- |
| A，推荐 | 经 Portal OpenAPI 保存规则草稿，明确返回待发布状态；不调用发布接口 | 有权限的操作人在 Portal 发布后，由现有长轮询热更新 | 完成受限编辑能力，保留现有人工发布边界；调用成功不表示求值已变化                         |
| B    | 经限定写端编辑并发布 Namespace，返回发布结果               | 发布成功并经客户端热更新后                 | 需额外设计同 Namespace 其他待发布变更、并发写入、编辑/发布身份分离及失败补偿；不得连带发布未批准修改 |

已选择 A。本次用户选择只裁决产品行为和实现边界，不代表向线上 Apollo 写规则或授予新凭据的具体操作请求。

## 验收衔接

等待选择期间，S1-CFG-004 曾标记 blocked，求值、规则验证、热更新和独立测试继续推进；用户确认后恢复 doing。YUE-001、IDP-009 不受影响。

按 A 补真实 Portal 授权写入、无发布副作用、返回待发布状态、人工发布后求值更新、越权/缺配置拒绝、敏感值不泄漏与确认元数据测试。不能只用测试 AdminService 或假写端代替生产路径验收。

PRD §7.8 与任务验收已按选择回写，组件文档随实现交付。权益服务尚未交付，本任务的正交测试明确区分消费侧权益结果 fixture 与真实 Runtime 挂载；网关、SDK、外壳的真实权益/挂载/灰度联调仍由已有 S1-ENT-002 验收，不能提前宣称通过。
