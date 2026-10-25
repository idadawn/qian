> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 平台插件

多租户平台的能力都以组件交付：租户、身份与 SSO、鉴权、配置、事件与消息、调度与实时、权益等。规划中的组件清单来自 PRD §7 与组件模型规格 §7.7；带代码的组件同时可作为 NuGet 包独立使用（ADR-019）。

> 占位页：组件文件夹尚未建立。S1-DOCS-002 起各组件页由收集脚本从组件文件夹生成，按[组件页模板](/pr-preview/pr-122/design/prd/specs/docs-site.md)固定九节：用途、安装、配置、表面、API、示例、模型体验、已知限制与延期项、变更。

| 组件                                                                                 | 名称         | 阶段 | 用途                                                                      |
| ---------------------------------------------------------------------------------- | ---------- | -- | ----------------------------------------------------------------------- |
| [qian.registry](/pr-preview/pr-122/components/platform/qian.registry.md)           | 注册表        | S1 | 平台插件三件套之一：清单解析与校验、注册表、租户 scope patch 持久化、修订保留与回切记录；只进 platform profile。 |
| [qian.lifecycle](/pr-preview/pr-122/components/platform/qian.lifecycle.md)         | 生命周期       | S1 | 平台插件三件套之一：制品 → 修订 → 激活与回切，租户级开通、升级、卸载，强制验签。                             |
| [qian.capabilities](/pr-preview/pr-122/components/platform/qian.capabilities.md)   | 能力目录       | S1 | 平台插件三件套之一：命令 / 查询 / 事件 / 实体四类能力目录、扩展点与契约仓库、OpenAPI 聚合。                  |
| [qian.hooks](/pr-preview/pr-122/components/platform/qian.hooks.md)                 | 钩子         | S1 | 把组件 hooks.json 里的 command / http 监听器转成同模式的进程内监听器。                       |
| [qian.tenancy](/pr-preview/pr-122/components/platform/qian.tenancy.md)             | 租户目录与生命周期  | S1 | 租户主档、隔离策略装配（默认共享库 + tenant_id + RLS）、显式状态机与开通编排。                        |
| [qian.identity](/pr-preview/pr-122/components/platform/qian.identity.md)           | 身份与 SSO    | S1 | OIDC / OAuth2 端点、Person + Membership 双层账号、统一登录流水线，连接器 oidc / wecom。     |
| [qian.authz](/pr-preview/pr-122/components/platform/qian.authz.md)                 | 鉴权引擎       | S1 | 权限点注册表、租户域 RBAC 与插件默认角色模板。                                              |
| [qian.data](/pr-preview/pr-122/components/platform/qian.data.md)                   | 数据存储默认安全装配 | S1 | RLS 拦截器、租户与软删过滤、审计字段、并发令牌与同库 Outbox 的默认装配。                              |
| [qian.files](/pr-preview/pr-122/components/platform/qian.files.md)                 | 文件存储       | S1 | 租户前缀与配额、预签名上传下载、强制附件下载；对象存储 / S3 兼容 / 本地三种 Provider。                    |
| [qian.config](/pr-preview/pr-122/components/platform/qian.config.md)               | 配置中心       | S1 | Apollo Provider（长轮询、访问密钥签名、本地缓存）、配置源顺序与先校验后生效的热更新。                      |
| [qian.gateway](/pr-preview/pr-122/components/platform/qian.gateway.md)             | 业务 API 网关  | S1 | 网关防护：强制认证、路由级限流、可信代理、出网 SSRF 基线。                                        |
| [qian.events](/pr-preview/pr-122/components/platform/qian.events.md)               | 事件总线       | S1 | Outbox 同事务写入与可靠发布、至少一次消费、幂等表、死信与租户回灌。                                   |
| [qian.audit](/pr-preview/pr-122/components/platform/qian.audit.md)                 | 审计         | S1 | 命令级哈希链审计与实体级同事务审计，actor 含 agent。                                        |
| [qian.observability](/pr-preview/pr-122/components/platform/qian.observability.md) | 可观测        | S1 | OTel、租户 / 用户 / 插件 baggage 与计量事件。                                        |
| [qian.notify](/pr-preview/pr-122/components/platform/qian.notify.md)               | 通知         | S1 | 消息类型注册、模板与发送配置、异步投递，首批三个渠道。                                             |
| [qian.scheduler](/pr-preview/pr-122/components/platform/qian.scheduler.md)         | 调度         | S1 | PG 持久化的作业与触发器、集群安全领取、按业务键可取消。                                           |
| [qian.realtime](/pr-preview/pr-122/components/platform/qian.realtime.md)           | 实时推送       | S1 | 可选（P1）：WebSocket / SSE 会话、一次性握手票据、Redis 背板扇出与在线状态。                      |
| [qian.entitlement](/pr-preview/pr-122/components/platform/qian.entitlement.md)     | 授权 v0      | S1 | 「租户 × 插件 × 计划」授权表、运营手工授予与权益查询 API。                                      |
| [qian.shell](/pr-preview/pr-122/components/platform/qian.shell.md)                 | 前端外壳       | S1 | yue 外壳静态制品与外壳 API：MF2 宿主、共享依赖与版本化插槽注册表。                                 |
| [qian.model-gateway](/pr-preview/pr-122/components/platform/qian.model-gateway.md) | 模型服务网关底座   | S1 | 可选（P1）：GPUStack 注册为 Higress 上游、Provider Adapter、逻辑模型目录。                 |
| [qian.docs](/pr-preview/pr-122/components/platform/qian.docs.md)                   | 文档生成器      | S1 | 开发态组件：qian docs generate 从代码生成配置目录、模块图与事件图。                             |
| [qian.workflow](/pr-preview/pr-122/components/platform/qian.workflow.md)           | 流程与自动化     | S2 | 审批内核与触发器：一套 JSON 定义、节点树视图设计器。                                           |
