> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.



# Qian 文档

Qian 平台基于 Kun 框架构建，前端使用 yue 组件库：组件一条命令装进任何项目，组合起来就是为 Agent 时代准备的多租户平台。

## 三条起步命令

命令与「开始」部分同源；S1 建设期内随任务陆续可用。

### ① 装一个组件

```bash
qian add identity authz files
```
在 ASP.NET Core 项目里装身份、鉴权、文件三个组件，不用部署 Qian 平台。[30 分钟上手 →](/start/quickstart.md)

### ② 配置你的编码代理

```bash
npx qian@latest --skill files
```
把组件的技能装进 Claude Code、Codex 等编码代理；代理先读仓库里的 `AGENTS.md`。[详见下一节 ↓](#agents)

### ③ 调一个模型 S1·P1

```bash
curl "$MODEL_GATEWAY/v1/chat/completions" \
  -H "Authorization: Bearer $MODEL_GATEWAY_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model": "chat-default", "messages": [{"role": "user", "content": "你好"}]}'
```
经模型网关的 OpenAI 兼容端点发一条请求，用逻辑模型名。[模型网关 →](/sdk/ai/gateway.md)

## 配置你的编码代理

Agent 原生表面从 S1 起就是一等公民：组件带的技能、约定与 MCP 可以直接交给编码代理。

### Qian 技能

组件的 `skills/` 表面可以直接装进编码代理：`npx qian@latest --skill <组件>`，或用 `<组件>/<技能>` 只装一个。[表面 →](/start/concepts/surface.md)

### AGENTS.md 约定

每个仓库与每个包都带 `AGENTS.md`，`qian init` 生成骨架；代理动手前先读它。[ADR-020 D20.9 →](/design/adr/adr-020-component-model-v2.md)

### Qian MCP

组件的 `.mcp.json` 表面随组件安装；文档检索 MCP 为 S1 可选；每租户能力门面（S4）。[MCP →](/sdk/ai/mcp.md)

### 提示词模板

让代理按任务文件工作：读 `AGENTS.md`、认领任务、按验收逐条完成并开 PR。模板见[仓库 pm/README 第 7 节](https://github.com/idadawn/qian/blob/main/pm/README.md#7-怎么下发任务)。

## 最新动态

- 2026-10-25 [身份适配器与数据组件的登记先后不再影响能力执行（S1-KUN-017）](/design/changelog.md)：Kun 升到 2026.10.25：数据约定与实体审计不再以匿名兜底占住框架当前主体端口，宿主先登记数据组件、后登记身份适配器时能力照常执行（此前一律 403 AUTHZ_DENIED），参考应用、测试宿主与联调栈的绕开写法已撤销。能力宿主的框架当前主体端口若仍解析为匿名兜底，启动即失败并写明改法；只装数据组件、没有身份适配器的宿主照常可用，审计与删除主体写 null。详见 Qian.Plugin.Sdk、Qian.Data。
- 2026-10-24 [网关路由共享限流（S1-GW-002）](/design/changelog.md)：网关 支持 IP、租户和用户三策略及 Redis 共享固定窗口，超限返回带 Retry-After 的 429，Redis 故障返回 503。路由现在必须显式填写 rateLimiterPolicy；仓库配置已迁移，自定义宿主与 profile 需补充该字段。
- 2026-10-24 [身份客户端可从私有源安装（S1-IDP-015）](/design/changelog.md)：qian.identity.client 钉定已发布的 Qian.Identity.Client 0.3.0 及归档摘要，移除骨架标记。真实 qian add identity 安装门校验包源、版本、摘要、表面与 lock，再由宿主按清单挂载 Identity 服务并撤销；发行方与客户端标识由宿主显式配置。
- 2026-10-24 [通用契约 v0 冻结（S1-AGT-005）](/design/changelog.md)：contracts/v0 的六份 Schema 按 R53 / R80 / R119 统一标记为 frozen，后续只接受向后兼容增量，破坏性变更进入 v1。现有字段与约束保持不变；data/v1 引用草稿信封的例外随之解除。

[完整变更日志 →](/design/changelog.md)

## 产品能力

按用途找能力：每项一句话，正文只在一个部分写一次。

### 用 AI 构建

- [模型网关](/sdk/ai/gateway.md)：一个端点调所有模型，配额、回退、计量（S1·P1）
- [AI SDK](/sdk/ai/index.md)：调模型与 Embedding，写 Agent 表面，声明工具副作用与策略
- [Agent 运行时对接](/sdk/ai/agent-runtime.md)：Qian 不写 Agent loop，运行时为 iClaw
- [MCP](/sdk/ai/mcp.md)：组件的 `.mcp.json` 表面；每租户能力门面（S4）
- [流程与自动化](/components/platform/qian.workflow.md)：审批内核与触发器（S2）
- [沙箱](/sdk/ai/sandbox.md)：隔离执行不可信或 Agent 生成的代码（S4）

### 构建应用

- [宿主与框架](/start/hosts.md)：独立模式、platform profile、React + yue、[与 Kun 的关系](/sdk/kun/with-qian.md)
- [Kun 框架](/sdk/kun/index.md)：Qian 的底座，.NET 10 微服务框架的模块地图、指南、安全与 [API](/sdk/api/kun/index.md)
- [组件与 qian add](/components/index.md)：一条命令安装，装上即可用
- [能力与函数](/sdk/dotnet/capabilities.md)：能力目录；`function` 表面（S2）
- [事件与中间件](/sdk/dotnet/events.md)：五种分发模式，`waterfall` 即环绕中间件
- [profile 与 patch](/start/concepts/profile.md)：环境 = profile + 分层 patch
- [yue 与微前端](/sdk/frontend/index.md)：组件库、外壳与远程模块
- [存储](/components/platform/qian.files.md)：文件与数据，租户隔离默认开启
- [低代码](/design/prd/08-s2-lowcode.md)：表单与列表（S2）

### 多租户平台

- [租户](/components/platform/qian.tenancy.md)：租户目录、隔离策略与生命周期
- [身份与 SSO](/components/platform/qian.identity.md)：OIDC 端点与双层账号
- [鉴权](/components/platform/qian.authz.md)：权限点注册表与租户域 RBAC
- [权益](/components/platform/qian.entitlement.md)：授权 v0；计费（S3）
- [配置中心](/components/platform/qian.config.md)：Apollo 与热更新
- [事件与消息](/components/platform/qian.events.md)：Outbox 可靠发布
- [调度与实时](/components/platform/qian.scheduler.md)：持久化作业与实时推送
- [市场](/marketplace/index.md)：上架、信任等级与签名（S3）

### 保护应用

- [网关防护](/components/platform/qian.gateway.md)：强制认证、路由级限流、可信代理、出网 SSRF 基线
- [发布保护](/ops/release.md)：制品验签、修订回切、灰度开关
- [审计](/components/platform/qian.audit.md)：命令级哈希链与实体级同事务审计
- [密钥](/ops/secrets.md)：`secretRef`、按范围授权、轮换

### 部署与运维

- [GitOps 部署](/ops/deploy.md)：Argo CD app-of-apps、Kustomize、digest 写回
- [灰度发布](/ops/release.md)：灰度开关 + profile 滚动更新
- [修订回切](/ops/release.md)：回到任一保留的修订
- [可观测](/components/platform/qian.observability.md)：OTel、租户 / 用户 / 插件 baggage、计量事件
- [模型网关接线](/ops/model-gateway.md)：GPUStack → Higress（S1·P1）

## 指南与教程

### Agent 栈

Qian 提供能力与主数据，iClaw 跑 Agent，模型与 MCP 流量走 Higress。[阅读 →](/start/guides/index.md#agent-stack)

### 后端模式

seam 三角色、只经 Abstractions 引用、五种事件分发模式。[阅读 →](/start/guides/index.md#backend)

### 多 profile 组合

gateway、identity、platform 等 profile 各自部署、各自升级。[阅读 →](/start/guides/index.md#profiles)

### 安全

RLS 租户隔离、默认拒绝匿名、`secretRef`、信任等级。[阅读 →](/start/guides/index.md#security)

### 连接器与集成

为 seam 提供 Provider、声明出网目标的连接器组件。[阅读 →](/start/guides/index.md#integrations)

### 从参考平台迁移

租户与权限、表单与列表、流程、数据集成、前端五个领域。[阅读 →](/design/migration/index.md)

- [站点地图](/sitemap.md)
- 产品族仓库：[Qian](https://github.com/idadawn/qian)、[Kun](https://github.com/idadawn/kun)、[yue（Qian 仓库内）](https://github.com/idadawn/qian)、qian-plugins（待建）
- 许可证：[Qian Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0)、[Kun MIT](https://opensource.org/license/mit)

