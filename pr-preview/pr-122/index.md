> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.



# Qian 文档

Qian 平台基于 Kun 框架构建，前端使用 yue 组件库：组件一条命令装进任何项目，组合起来就是为 Agent 时代准备的多租户平台。

## 三条起步命令

命令与「开始」部分同源；S1 建设期内随任务陆续可用。

### ① 装一个组件

```bash
qian add identity authz files
```
在 ASP.NET Core 项目里装身份、鉴权、文件三个组件，不用部署 Qian 平台。[30 分钟上手 →](/pr-preview/pr-122/start/quickstart.md)

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
经模型网关的 OpenAI 兼容端点发一条请求，用逻辑模型名。[模型网关 →](/pr-preview/pr-122/sdk/ai/gateway.md)

## 配置你的编码代理

Agent 原生表面从 S1 起就是一等公民：组件带的技能、约定与 MCP 可以直接交给编码代理。

### Qian 技能

组件的 `skills/` 表面可以直接装进编码代理：`npx qian@latest --skill <组件>`，或用 `<组件>/<技能>` 只装一个。[表面 →](/pr-preview/pr-122/start/concepts/surface.md)

### AGENTS.md 约定

每个仓库与每个包都带 `AGENTS.md`，`qian init` 生成骨架；代理动手前先读它。[ADR-020 D20.9 →](/pr-preview/pr-122/design/adr/adr-020-component-model-v2.md)

### Qian MCP

组件的 `.mcp.json` 表面随组件安装；文档检索 MCP 为 S1 可选；每租户能力门面（S4）。[MCP →](/pr-preview/pr-122/sdk/ai/mcp.md)

### 提示词模板

让代理按任务文件工作：读 `AGENTS.md`、认领任务、按验收逐条完成并开 PR。模板见[仓库 pm/README 第 7 节](https://github.com/idadawn/qian/blob/main/pm/README.md#7-怎么下发任务)。

## 最新动态

- 2026-10-15 [执行入口拒绝超出 decimal 范围的数（S1-RT-024）](/pr-preview/pr-122/design/changelog.md)：通用执行入口的请求体里有绝对值超过 decimal 范围（约 7.9e28）的数时返回 400 BAD_REQUEST，不再返回 500；输入 Schema 的 enum / const 取值含这类数的能力在登记阶段以新规则 unsupported-number 拒绝。详见 Qian.Capabilities。
- 2026-10-15 [执行入口补不可重放码，参考应用能力标为 pii（S1-RT-021）](/pr-preview/pr-122/design/changelog.md)：聚合 OpenAPI 为写类能力列出 409 CAPABILITY_REPLAY_UNAVAILABLE 及其两种 reason。参考应用的 customer.create、customer.list、customer.get 响应含客户名称，dataClassification 改为 pii：经通用执行入口带同一幂等键重放 customer.create 改为返回 409（data_classification）。运维手册新增「部署步骤：平台宿主装配」（platform 宿主必须调用 AddQianCapabilities()）与「过期幂等回执清理」两节。详见 Qian.Capabilities。
- 2026-10-15 [能力登记期契约检查与幂等回执上限（S1-RT-020）](/pr-preview/pr-122/design/changelog.md)：平台宿主调用 AddQianCapabilities() 后，ctx.ProvideCapability 在登记时生成目录条目并预编译输入校验器，不合格的契约（输入含租户字段、Schema 无法可靠校验等）以 CapabilityContractException 拒绝，组件挂载失败。通用执行入口的幂等回执按数据分级与组件配置 receiptMaxBytes（默认 64 KiB）决定是否保存响应体，不保存时同键重放返回 409 CAPABILITY_REPLAY_UNAVAILABLE；Qian.Capabilities 新增迁移 InvokeReceiptLimits。Qian.Data 新增「迁移执行与 RLS 核查」一节（迁移必须经迁移器执行，附核查 SQL），运维手册新增「部署步骤：数据库迁移」。详见组件规格 §9.5 与 Qian.Capabilities。
- 2026-10-15 [数据契约冻结、组件元数据与文档预算整改（S1-QA-010）](/pr-preview/pr-122/design/changelog.md)：data/v1 契约 按 R80 明确 frozen 状态、修订规则和已知例外，统一 $id 前缀并补结构测试；yue manifest 的属性元数据（含嵌套）递归取自 TS 注释，不存在的属性拒绝导出；文档站预算 按 R55 调为 120 MB，#95 合并时实测 96.2 MB。

[完整变更日志 →](/pr-preview/pr-122/design/changelog.md)

## 产品能力

按用途找能力：每项一句话，正文只在一个部分写一次。

### 用 AI 构建

- [模型网关](/pr-preview/pr-122/sdk/ai/gateway.md)：一个端点调所有模型，配额、回退、计量（S1·P1）
- [AI SDK](/pr-preview/pr-122/sdk/ai/index.md)：调模型与 Embedding，写 Agent 表面，声明工具副作用与策略
- [Agent 运行时对接](/pr-preview/pr-122/sdk/ai/agent-runtime.md)：Qian 不写 Agent loop，运行时为 iClaw
- [MCP](/pr-preview/pr-122/sdk/ai/mcp.md)：组件的 `.mcp.json` 表面；每租户能力门面（S4）
- [流程与自动化](/pr-preview/pr-122/components/platform/qian.workflow.md)：审批内核与触发器（S2）
- [沙箱](/pr-preview/pr-122/sdk/ai/sandbox.md)：隔离执行不可信或 Agent 生成的代码（S4）

### 构建应用

- [宿主与框架](/pr-preview/pr-122/start/hosts.md)：独立模式、platform profile、React + yue、[与 Kun 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)
- [Kun 框架](/pr-preview/pr-122/sdk/kun/index.md)：Qian 的底座，.NET 10 微服务框架的模块地图、指南、安全与 [API](/pr-preview/pr-122/sdk/api/kun/index.md)
- [组件与 qian add](/pr-preview/pr-122/components/index.md)：一条命令安装，装上即可用
- [能力与函数](/pr-preview/pr-122/sdk/dotnet/capabilities.md)：能力目录；`function` 表面（S2）
- [事件与中间件](/pr-preview/pr-122/sdk/dotnet/events.md)：五种分发模式，`waterfall` 即环绕中间件
- [profile 与 patch](/pr-preview/pr-122/start/concepts/profile.md)：环境 = profile + 分层 patch
- [yue 与微前端](/pr-preview/pr-122/sdk/frontend/index.md)：组件库、外壳与远程模块
- [存储](/pr-preview/pr-122/components/platform/qian.files.md)：文件与数据，租户隔离默认开启
- [低代码](/pr-preview/pr-122/design/prd/08-s2-lowcode.md)：表单与列表（S2）

### 多租户平台

- [租户](/pr-preview/pr-122/components/platform/qian.tenancy.md)：租户目录、隔离策略与生命周期
- [身份与 SSO](/pr-preview/pr-122/components/platform/qian.identity.md)：OIDC 端点与双层账号
- [鉴权](/pr-preview/pr-122/components/platform/qian.authz.md)：权限点注册表与租户域 RBAC
- [权益](/pr-preview/pr-122/components/platform/qian.entitlement.md)：授权 v0；计费（S3）
- [配置中心](/pr-preview/pr-122/components/platform/qian.config.md)：Apollo 与热更新
- [事件与消息](/pr-preview/pr-122/components/platform/qian.events.md)：Outbox 可靠发布
- [调度与实时](/pr-preview/pr-122/components/platform/qian.scheduler.md)：持久化作业与实时推送
- [市场](/pr-preview/pr-122/marketplace/index.md)：上架、信任等级与签名（S3）

### 保护应用

- [网关防护](/pr-preview/pr-122/components/platform/qian.gateway.md)：强制认证、路由级限流、可信代理、出网 SSRF 基线
- [发布保护](/pr-preview/pr-122/ops/release.md)：制品验签、修订回切、灰度开关
- [审计](/pr-preview/pr-122/components/platform/qian.audit.md)：命令级哈希链与实体级同事务审计
- [密钥](/pr-preview/pr-122/ops/secrets.md)：`secretRef`、按范围授权、轮换

### 部署与运维

- [GitOps 部署](/pr-preview/pr-122/ops/deploy.md)：Argo CD app-of-apps、Kustomize、digest 写回
- [灰度发布](/pr-preview/pr-122/ops/release.md)：灰度开关 + profile 滚动更新
- [修订回切](/pr-preview/pr-122/ops/release.md)：回到任一保留的修订
- [可观测](/pr-preview/pr-122/components/platform/qian.observability.md)：OTel、租户 / 用户 / 插件 baggage、计量事件
- [模型网关接线](/pr-preview/pr-122/ops/model-gateway.md)：GPUStack → Higress（S1·P1）

## 指南与教程

### Agent 栈

Qian 提供能力与主数据，iClaw 跑 Agent，模型与 MCP 流量走 Higress。[阅读 →](/pr-preview/pr-122/start/guides/index.md#agent-stack)

### 后端模式

seam 三角色、只经 Abstractions 引用、五种事件分发模式。[阅读 →](/pr-preview/pr-122/start/guides/index.md#backend)

### 多 profile 组合

gateway、identity、platform 等 profile 各自部署、各自升级。[阅读 →](/pr-preview/pr-122/start/guides/index.md#profiles)

### 安全

RLS 租户隔离、默认拒绝匿名、`secretRef`、信任等级。[阅读 →](/pr-preview/pr-122/start/guides/index.md#security)

### 连接器与集成

为 seam 提供 Provider、声明出网目标的连接器组件。[阅读 →](/pr-preview/pr-122/start/guides/index.md#integrations)

### 从参考平台迁移

租户与权限、表单与列表、流程、数据集成、前端五个领域。[阅读 →](/pr-preview/pr-122/design/migration/index.md)

- [站点地图](/pr-preview/pr-122/sitemap.md)
- 产品族仓库：[Qian](https://github.com/idadawn/qian)、[Kun](https://github.com/idadawn/kun)、[yue（Qian 仓库内）](https://github.com/idadawn/qian)、qian-plugins（待建）
- 许可证：[Qian Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0)、[Kun MIT](https://opensource.org/license/mit)

