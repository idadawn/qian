# 4. 形态与目标市场

> 状态：草案 v1 · 更新日期：2026-10-06 · 决策引用：ADR-003、ADR-004、ADR-007、ADR-010、ADR-013、ADR-014、ADR-015、ADR-017、ADR-018 · 上级索引：[README](README.md)

本章回答「Qian 长什么样、卖给谁、怎么交付」。架构图只到可部署单元与共享组件一级，内部结构见 §07–§10；部署细节见 ADR-017；市场数字一律写「待定」或区间，不编造。

## 4.1 架构总图（S1 目标态）

```text
   浏览器 / 移动 H5 / Agent 客户端（MCP）/ ISV 后端（client_credentials）
                     │
                     ▼
        ┌───────────────────────────┐
        │ Traefik（北向公网入口）     │  TLS 终止 · 不做鉴权 · 不感知租户
        └──────┬──────────┬─────────┘
   业务 API    │          │  模型 / MCP 流量（S4 起 MCP 门面经此）
               ▼          ▼
┌────────────────────┐  ┌──────────────────────────────┐
│ qian-gateway       │  │ Higress（AI 网关）             │
│ Kun.Gateway 加固    │  │ ai-proxy · 逻辑模型名映射      │
│ 强制认证 · 路由级限流│  │ ai-quota · token 限流 · 统计   │
│ 密钥热轮换 · 可信代理│  │ MCP 路由（Mcp-Method/Mcp-Name）│
│ Cookie 会话 ⇄ S2S  │  └──────────────┬───────────────┘
│ S3：租户×应用动态路由│                 │
└───────┬────────────┘                 ▼
        │                     ┌──────────────────┐
        │                     │ GPUStack          │  OpenAI 兼容上游 · 模型推理
        │                     └────────▲─────────┘
        │                              │ 发现 / 健康 / 生命周期
        │                     ┌────────┴──────────────────┐
        │                     │ qian-model-gateway-adapter │（可选）Provider Adapter
        │                     └───────────────────────────┘
        ▼
┌─────────────────────────────────────────────────────────────┐
│ qian-identity（OpenIddict，独立安全边界）                      │
│   唯一 issuer · Person + Membership · 会话中心 · 连接器插件     │
├─────────────────────────────────────────────────────────────┤
│ qian-platform（内核宿主 + module 型系统插件）                   │
│   内核：租户上下文 · 主体与 S2S · PDP/PEP · 插件注册表与生命周期  │
│         扩展点/契约仓库 · 能力目录 · 事件总线 · 审计 · 计量      │
│   系统插件：qian.tenancy  qian.authz  qian.data  qian.files     │
│            qian.config  qian.events  qian.audit                │
│            qian.observability  qian.entitlement                 │
│            qian.notify  qian.scheduler  qian.realtime          │
├─────────────────────────────────────────────────────────────┤
│ qian-lowcode（S2 新增可部署单元，JIT 宿主 / 非 AOT 孤岛，§08）  │
│   qian.metadata  qian.dynamic-data  qian.schema-migrator        │
│   qian.expression  qian.forms  qian.lists  qian.widgets         │
│   qian.data-model  qian.data-api  qian.print（服务端）          │
├─────────────────────────────────────────────────────────────┤
│ qian-workflow（S2 新增可部署单元，自研审批内核，§08）           │
│   qian.workflow：定义 · 引擎 · 运行态 · 解析器 · 子流程         │
├─────────────────────────────────────────────────────────────┤
│ qian-shell（yue 外壳静态站，Module Federation 2）               │
│   AppShell · 插槽注册表 · 控制台 / 应用 上下文切换 · 认证页族     │
│   S3：不可信 UI 走 Worker 远程渲染 / iframe 沙箱               │
└───────┬──────────┬──────────┬──────────┬──────────┬──────────┘
        ▼          ▼          ▼          ▼          ▼
   PostgreSQL    Redis     RabbitMQ   对象存储     Apollo（配置中心）
   唯一一等库    缓存/幂等  至少一次    文件/制品    非敏感配置 + secretRef
   RLS 双保险    限流背板   Outbox     OCI 插件包   （需 MySQL；Portal 接运维 IdP）
   schema/服务                        → Harbor

   横切：K3s · Argo CD app-of-apps · Kustomize · 按 digest 不可变发布 · default-deny NetworkPolicy
         ResourceQuota · /healthz /readyz · OTel trace + 计量标签 · K8s Secret / 密钥服务
```

读图要点：

| 要点 | 说明 | 真相源 |
|---|---|---|
| 两套网关职责从 S1 画清 | Traefik 只做入口；`qian.gateway` 是业务 API 边界（强制认证、限流、换签）；Higress 只管模型与 MCP 流量、对租户无感。互不承担对方职责 | ADR-010、ADR-015 |
| S1 可部署单元克制在 5 个 | `qian-gateway`、`qian-identity`、`qian-platform`、`qian-model-gateway-adapter`（可选）、`qian-shell`；三节点 K3s 的现实。S2 新增 `qian-lowcode`（第 6 个，JIT 宿主，承载 metadata / dynamic-data / forms / lists 的服务端与动态 SQL 生成）与 `qian-workflow`，另有 `qian-file-preview` 与可选 `qian-wasm-runtime`，见 §08 8.0.3 | ADR-003、§08 |
| S2 `qian-workflow` 独立成可部署单元 | `qian.workflow` 全部（定义、引擎、运行态、解析器、通知绑定、SLA、子流程）住独立单元而不进 `qian-lowcode`：审批域体量大，计时器与令牌推进有自己的伸缩曲线，与表单运行时解耦便于单独回滚（R21） | §08 8.0.3、ADR-003 §6 |
| 系统插件 18 个 | `qian.kernel`、`qian.plugin-sdk`、`qian.tenancy`、`qian.identity`（+ `connector.*`）、`qian.authz`、`qian.data`、`qian.files`、`qian.config`、`qian.gateway`、`qian.events`、`qian.audit`、`qian.observability`、`qian.shell`、`qian.entitlement`、`qian.model-gateway`（P1，可选）、`qian.notify`（P0）、`qian.scheduler`（P0）、`qian.realtime`（P1）。组织目录最小集是 `qian.identity` 的子模块（§07 7.4.3），不独立成插件 | ADR-003、§07 |
| PostgreSQL 唯一一等库 | 默认共享库 + 行级 `tenant_id` + RLS；平台表 schema-per-service；动态表混合存储。本期不支持 MySQL / 国产库作业务库 | ADR-004 |
| Apollo 的边界 | 只存平台 / 服务级非敏感配置与密钥引用；租户级配置不进 Apollo；需新增 MySQL；自研 .NET provider | ADR-007 |
| 模型接入不自建 | GPUStack 作为 OpenAI 兼容上游注册进 Higress；Qian 只做 Provider Adapter 与 `model-capabilities` 契约；租户 / 配额 / 计量在平台侧 | ADR-015 |
| 与 Agent 运行时的分工 | Qian = 能力提供方 + 业务主数据权威；外部 Agent 运行时 = Agent loop + AI 资产控制面；共用 Kun 与 Higress | ADR-016 |

## 4.2 产品形态按阶段

| 阶段 | 对外形态 | 一句话 |
|---|---|---|
| S1 技术底座 | 一组 NuGet 包 + CLI + TCK + 部署物料 + 后台控制台 | 「开发者用它半天写出一个多租户应用」 |
| S2 低代码构建 | 在 S1 之上的设计器（表单 / 列表 / 审批）与运行时，应用可打包 | 「搭建者不写代码建出带审批的应用并授权给租户用户」 |
| S3 面向客户 | 私有目录型市场 → 开放 ISV 与在线交易；租户自助（IdP、品牌、套餐） | 「租户自己从市场选购、开通、升级」 |
| S4 AI 原生 Agent 平台 | 每租户 MCP 门面、Agent 主体与委托链、元数据生成闭环 | 「Agent 能发现、调用、编排乃至生成平台能力与应用」 |

## 4.3 客群画像按阶段

| 阶段 | 首要客群 | 次要客群 | 他们要解决的问题 | 他们怎么衡量我们 |
|---|---|---|---|---|
| S1 | **平台开发者**（本团队）与 **内部项目的 .NET 开发者** | 兄弟系统（Agent 运行时）的开发者 | 每个新项目都在重复做租户、登录、鉴权、文件、配置、审计；多副本下多租户漏洞频发 | 从空仓库到可部署的多租户应用需要多久；安全默认值是否强制；TCK 是否能一次跑通 |
| S2 | **企业 IT 部门的业务搭建者**、**实施伙伴** | 平台开发者（扩展控件 / 节点） | 表单、审批、列表类需求多且变化快，纯代码交付跟不上 | 一个带审批的业务应用几天能上线；改表是否安全；权限是否能精确到字段与行 |
| S3 | **中型企业租户**（共享 SaaS）与 **对隔离有要求的企业**（高隔离档 / 私有化） | **ISV**（把行业应用上架） | 想用现成应用而非从零搭；需要国内 IdP 联邦、品牌、独立数据 | 市场里有没有想要的应用；开通是否自助；升级是否不破坏定制；数据是否隔离 |
| S4 | **已有租户中的 Agent 使用者**；**外部 Agent 平台 / 编码 Agent 用户** | ISV（上架 Agent 资产） | 希望 Agent 能用现有业务系统、能生成应用草稿且受控 | Agent 调平台能力是否与人同权；生成物是否可审、可回滚、可解释 |

画像中不写客户名与数量；目标数量级见 4.5。

## 4.4 交付方式

| 档位 | 适用 | 隔离拓扑 | 身份 | 市场 | 起始阶段 |
|---|---|---|---|---|---|
| **共享 SaaS** | 中小租户、试用、ISV 分发的主流形态 | 同一命名空间；共享 PostgreSQL，RLS + 行级过滤双保险；共享 Redis / RabbitMQ | 平台唯一 issuer；租户可挂外部 IdP 连接器（S3 自助） | 在线目录；只允许 package / function / connector / 受限 ui 类第三方包 | S1 底座 · S3 对外 |
| **高隔离租户** | 对数据隔离、合规或性能有要求的企业租户 | 独立命名空间 + 独立 PostgreSQL / Redis（ApplicationSet 生成）；独立库作付费选项 | 同上；可选租户自定义登录域名（S3） | 同共享 SaaS；可叠加专属部署的 service 类插件（运维行为） | S3 |
| **私有化** | 政企、高校、对离线有要求的客户 | 整套 overlay 复制到客户集群；离线包 + 授权文件绑定租户 ID；内网市场镜像同步 | 平台 issuer 部署在客户侧；S3 起 SAML / CAS / LDAP 连接器 | 离线目录 / 镜像同步 | S3（S1 起保证可私有化：无 AGPL / 商业许可依赖） |

共同约束：三档共用同一套制品、同一份清单与同一条发布流水线；不以 per-tenant namespace 为默认拓扑；生产与兄弟系统共用集群但独立节点池，达到一定租户数再拆（ADR-017）。

## 4.5 目标数量级

> 原则：不编数字。以下只给「待定」或可被后续商业计划替换的区间占位，并指明谁来填。

| 指标 | S1 | S2 | S3 | S4 | 填写责任 |
|---|---|---|---|---|---|
| 使用 SDK 的开发者 / 项目 | 本团队 + 1–2 个内部项目 | 待定 | 待定 | 待定 | 平台负责人 |
| 租户数（共享 SaaS） | 内部试用租户若干 | 内部与伙伴试点租户（个位数到十位数区间） | 待定（商业计划） | 待定 | 产品负责人 + 商务 |
| 高隔离 / 私有化客户 | 0 | 0–1 试点 | 待定 | 待定 | 商务 |
| 市场上架应用（自营 + ISV） | 包格式跑通即可 | 自营应用若干 | 待定 | 待定 | 市场运营 |
| 受邀 ISV | 0 | 0 | 首批受邀（个位数区间） | 待定 | 市场运营 |
| 单集群支撑的租户规模 | 以三节点 K3s 的容量测试为准 | 同左 | 待容量测试后填入 | 同左 | 平台运维 |

说明：
- 行业调研（`research/notes/d3-web-market.md`）提供的是竞品格局、包形态、分成与审核做法等定性参照，不作为我们的数量目标。
- 任何写入 §16 的 KPI 必须先在本表有对应的「填写责任」并被填写，否则只作过程指标。

## 4.6 明确不做的形态

- 不做协作套件内嵌型（依附 IM 的组织与消息底座）；国内 IM 只作为 IdP 连接器与消息渠道接入。
- 不做纯零代码业务搭建的消费级产品；S2 的搭建者面向企业 IT 与实施伙伴。
- 不做自建 Agent 运行时、沙箱、记忆与技能引擎（复用兄弟系统，ADR-016）。
- 不做自研模型代理层（复用 Higress / GPUStack，ADR-015）。
- 本期不做原生移动 App；移动端以 H5 / PWA 为主，S2 只做 PC 响应式并预留端覆盖。

## 修订历史

- 2026-10-06：草案 v1。
