# Qian 调研基线要点汇编

> 面向产品负责人的 15 分钟读物。只整合 7 份领域基线，不加入基线以外的事实；基线间矛盾见 §3.5。被借鉴的商业低代码产品一律称「参考平台」。
>
> 阶段口径：**S1** 技术底座期——开发人员能用技术组件写出一个多租户应用；**S2** 低代码构建期——跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户」；**S3** 面向客户期——租户能从应用市场选购并开通应用；**S4** Agent 期（本期只规划模型服务网关）。
>
> 领域缩写：**K** Kun 能力与差距 · **P** 插件化内核与 Agent 架构 · **S** 租户/组织/身份/权限/系统管理 · **F** 应用/表单/列表/视图/发布 · **W** 流程与审批 · **D** 数据/集成/消息/调度/门户/报表/打印/大屏/预览 · **U** 前端与 yue。

---

## 1. 一页总览

### 1.1 七个领域各一段结论

**K · Kun 能力与差距。** Kun 是「微服务开发框架」而非平台内核：可生产级只有 `Kun.Core` 与 `Kun.Mediator`，关键路径有多处正确性缺口（丢消息、at-most-once、丢租户、无锁、网关不强制认证、gRPC 流式绕过鉴权）。六项硬性要求里微服务已具备，多租户部分具备，SSO 只有依赖方一侧，鉴权、Apollo、K3s 空白。S1 工作量落在空白处，没有必须推翻的核心设计，要改的是定位、AOT 承诺与不安全默认值；以 NuGet + CalVer 消费 Kun，PostgreSQL 为唯一一等数据库。

**P · 插件化内核与 Agent 原生架构。** 内核 = 插件运行时 + 注册表 + 契约，其余皆插件，系统能力也按同一清单以 `tier: system` 交付。Kun 的严格 AOT 决定进程内插件只能是编译期 `module`，运行时新增代码一律进程外，第三方代码永不进共享进程。一份契约派生路由、权限点、菜单、Apollo namespace、OpenAPI 与 MCP 工具；生命周期分平台级与租户级两层。Agent 侧复用 iClaw 运行时，协议以 MCP 2026-07-28 为目标；元数据 `package` 插件是最大杠杆，也是 Agent 生成应用最安全的形态。

**S · 租户、组织、身份与 SSO、权限、系统管理。** 本域是平台的可信计算基，S1 工作量最大。参考平台概念模型可借鉴、实现不可照搬（权限校验恒 true、会话不可吊销、租户解析 fail-open）。路线是 OpenIddict 自建 Qian Identity 作唯一 issuer，账号 Person + Membership，上游联邦全部做成连接器插件，Authentik 退居运维侧。权限分 L0–L6 七层，权限点由插件清单声明，决策端口可解释，数据权限编译为谓词叠加 RLS。租户控制面是独立服务并带显式状态机；消息中心是内核 + 渠道适配器插件。

**F · 应用、表单、列表、视图、发布。** 链路为应用 → 功能 → 表单/列表设计 → 发布到菜单与权限资源 → 动态数据引擎。保留「未发布/已发布/已修改」三态心智，但升级为不可变版本快照，发布拆成编译 → 迁移 → 切流 → 挂载的可重放步骤。最大差距是元数据 Schema 缺失、业务列迁移管道从零建、动态引擎的 SQL 安全与鉴权唯一入口。控件注册表与 Field Type Catalog 是单一事实源，Filter AST 在 S1 定义供五处共用；参考平台形态的代码生成不做。

**W · 流程与审批。** 流程整体属于 S2，S1 只交付硬性前置（责任岗位链组织模型、事件总线、调度、表达式规范、消息模板、元数据版本化契约、鉴权资源、文件存储）。引擎路线是自研 .NET 轻量审批内核，Elsa 3 仅作 PoC 对照，Flowable 只作可选 BPMN 适配器——参考平台引擎代码不到 2k 行，26k 行审批域业务才是大头。简单/标准流程统一为一套 JSON 定义、两种视图。参考平台 14 处做法必须修正（兜底全局 admin、会签停滞、拼 SQL 与 V8 任意脚本等）；逆向语义交叉是最易出 bug 的区域。

**D · 数据、集成、消息与调度、门户、报表、打印、大屏、文件预览。** 多子域同构的横切能力只在内核实现一次：可发布元数据框架、导入导出框架、引用登记、分享链接、预览票据、编码规则。S1 交付数据连接与类型系统、数据接口契约、通知、调度、实时推送、凭证保管；S2 交付数据建模、接口设计器、打印、文件预览、模型服务网关；门户与报表按任务书归 S3（笔记建议更早，待拍板）。大屏延后且不复用参考平台大屏源码（许可不清）；参考平台的进程内状态、明文凭据、任意 SQL/URL 代理不可照搬。

**U · 前端架构与 yue。** 参考平台前端「两套并存」是反面教材（去重后 251 条目、8 个设计器）；iClaw 有工程规范与令牌架构但没有组件库。yue 走路线 A：Base UI + Tailwind v4 + shadcn 源码自有化为基础层，数据密集组件在 TanStack 上自建，一套表单/表格/弹层引擎贯穿 S1 后台与 S2 低代码运行时，六层分层用工具强制。S1 起为 S4 预留机器可读 manifest、带版本的 Schema 与校验器、客户端动作注册表。三条安全硬线：不用 eval 执行租户脚本、令牌不进 URL/iframe、前端权限裁剪只是体验层。

### 1.2 阶段 × 领域矩阵

| 领域 | S1 | S2 | S3 | S4 |
|---|---|---|---|---|
| K | 修Kun缺口,租户,身份鉴权,Apollo,部署 | 元数据与动态数据层,表单列表流程引擎 | 自助开通,独立库档,动态路由,市场计费门户 | MCP门面,沙箱对接,Agent主体与act链 |
| P | 清单v1,注册表,状态机,签名,SDK/TCK | package,function,批量迁移 | 市场,ISV审核,哈希钉死,三方service待定 | 每租户MCP门面,Agent生成package |
| S | 租户控制面,唯一issuer,RBAC,通知内核 | 数据/字段权限,责任人解析,授权界面,导入 | 自助开通,租户IdP,SCIM,OpenFGA | Agent主体,令牌交换act,Explain工具 |
| F | 应用实体,菜单模型,FilterAST,控件契约 | 表单/列表设计器,动态数据引擎,迁移器,发布向导 | 应用包安装,外链能力令牌,个人视图 | AI建表,OCR控件,Agent工具契约落地 |
| W | 只交付前置:组织链,事件,调度,表达式规范 | 审批内核最小集,瀑布视图设计器,工作台 | SLA自动处置,委托签收撤销,决策,自由流 | AI建流程草稿,Agent作解析器/审批者 |
| D | 类型系统,数据连接,接口契约,通知,调度,实时 | 建模,接口设计器,打印,预览,模型网关 | 门户设计器,表格报表,开放API,分享链接 | 接口即工具,注册表→MCP映射,模板自检 |
| U | 令牌,核心组件,壳,表格,表单引擎,认证页 | 控件集,表单/列表运行时,三大设计器 | 门户/报表/打印设计器,移动H5,UI沙箱 | @yue/agent,MCP Apps,私有注册表 |

---

## 2. 逐领域要点

### 2.1 K · Kun 能力与差距、既有资产复用、基础设施

**领域全景**
- Kun 能力矩阵：32 个模块约 21k 行 C#，MIT、CalVer、单人维护；按「可生产 / 可用但薄 / 骨架 / 默认装配不可上线」逐模块判定。
- 六项硬性要求逐条评估（补齐路径 K 在 Kun 补 / Q 在 Qian 新建 / T 引入第三方）与 S1 技术组件逐项判定。
- 低代码动态性与 Kun「编译期已知、显式注册、AOT 零反射」世界观的 11 条张力及对策。
- 既有资产复用（iClaw、iclaw-deploy）、部署基线、Apollo 引入方式、模型服务网关基线。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | 修 Kun 关键缺口（租户回灌、至少一次、Saga/Outbox 锁、网关加固、gRPC 流式、Identity G2–G8、签发器与审计收敛）；租户目录与生命周期；`qian.identity`（OpenIddict）；`Kun.Authorization` 端口 + `qian.authz`；`qian.data` 默认安全装配；`qian.files` 新线；Apollo 部署 + 自研 provider + secretRef；能力目录；插件机制与 SDK/TCK；镜像、chart、健康端点、RabbitMQ K8s 化、default-deny、ResourceQuota；baggage；缓存租户前缀 | SchemaPerTenant 真库验证；Saga 修补；机器身份签发（S4 前必做）；Alertmanager；Redis 限流；通知通道插件 | 弃用 `Kun.Infrastructure` 死接口、旧线 `Kun.Storage`、`Kun.Audit.EFC` 事后拦截器、多余签发器；AOT 作平台级承诺——无带 EF 的服务被 AOT 发布过 |
| S2 | 元数据聚合 + 动态记录存储 + SQL 生成 + 动态 DDL + 泛型 CRUD 命令 + 信封事件 + 元数据校验器；表单/列表/流程引擎；数据权限编译器与字段权限；表达式解释器 | Wasm `function` 运行时（ABI 先拍板）；`connector` 运行时 | — |
| S3 | 自助开通、DatabasePerTenant；租户自助配 IdP/HRD/Passkey/SCIM/安装即建 client；动态路由 provider；接线 Application 生成器；市场、计费、门户、报表；不可信 UI 沙箱 | OpenFGA、网关 OPA；配置灰度/审核/回滚 | MySQL/国产库作业务库（本期，RLS 无等价物）；per-tenant namespace 默认拓扑；Dapr sidecar |
| S4 | MCP 门面、A2A、Arazzo、Agent 生成 `package`；对接 CubeSandbox；Agent 主体与 `act` 链 | — | 自建 Agent 运行时（复用 iClaw）；自研模型代理层（绕过 Higress） |

**关键规则与数据对象**
1. Kun 文档与代码不一致一律以代码为准（源生成器、`/s2s/issue`、NetworkPolicy 在代码里不存在）。
2. 租户只来自已校验令牌：claim 统一 `tenant_id`，生产禁用 `X-Tenant-Id` 头兜底与 `"default"` 回退（fail-closed）。
3. 集成事件只能在命令 handler 同 scope 发出，否则随子 scope 释放丢失；消费端改「处理成功后标记」。
4. 审计只留一套：实体级同事务 + Outbox，命令级哈希链补 PG 持久化；`actor_type` 加 `agent`，记录 `on_behalf_of`。
5. 三套 S2S 签发器收敛为一套，claim 对齐 iClaw 契约（`iss/aud[]/sub/tenant_id/project_id?/roles/scope/session_id`，exp−iat ≤ 300s）。
6. 租户级配置不进 Apollo（无租户概念、namespace 上限），由 `qian.tenancy/qian.config` 承载；Apollo 只存非敏感与 secretRef；Nacos 收窄为 AI Registry。
7. 插件按阶段：S1 kernel / plugin-sdk / tenancy / identity / authz / config / data / files / gateway / events / audit / observability；S2 metadata / dynamic-data / forms / lists / workflow；S3 marketplace / portal / reports / billing；model-gateway 跨 S1/S3。
8. 现网：单集群 K3s v1.35 三节点只跨两台宿主、几乎全钉首节点、唯一存储类 local-path；没有 Apollo、MySQL、Alertmanager 与集群内 RabbitMQ。

**在 Kun 上实现的差距与难点**
- S1 两座大山同时开工：自建身份核心与鉴权引擎都是「很大」量级且互为前置，OIDC 一致性测试与渗透测试要进预算。
- 关键路径缺口不先修，多租户会在后台链路漏——K3s 多副本 + 多租户必然触发。
- Kun 维护带宽：单人、节奏放缓、CHANGELOG 漏记；Qian 的框架需求通道与 SLA 未定。
- Apollo 隐性成本：MySQL 运维面（现网全 PG）、官方 .NET 客户端停滞且不适配 AOT、与 Nacos/GitOps 双真相、边界 ADR 修订。
- 现网单点与治理缺失须在多租户上线前补；许可证红线（开源低代码/AI 平台只借鉴设计，Zitadel、Duende 避开）；iClaw 可复用件多为设计态。

**插件化与 Agent 可用性要点**
- 低代码运行时 = 住在「非 AOT 孤岛」的 Kun 消费方模块：不走 EF 模型，少量泛型命令按元数据分派，通用信封事件 `qian.record.changed.v1`，`IMetadataValidator` 保留字段级错误。
- 形态按信任等级分 module/service/package/function/connector/ui；安装拆制品/部署/租户三层状态机；S1 接受「预装全部官方插件 + 按租户开关」。
- 能力目录（命令/查询/事件/实体四类注册表 + JSON Schema）直接升格为 S4 素材；复用 iClaw 的 Higress 治理契约、租户 API Key 与 OpenAI 兼容面，不复用 Agent loop。

**待拍板问题**
1. 身份路线：接受「混合 C2：OpenIddict 自建唯一 issuer，Authentik 退居运维侧」，还是选 Keycloak / Logto？前提：私有化/离线交付、首期是否必须 SAML/CAS/LDAP、自定义登录域名、等保/商密。
2. Qian 与 iClaw 共用边界：是否共用 Authentik、S2S 签发与 claim 形状、Usage Ledger/租户 Key、Higress 实例；租户主数据权威归谁。
3. 配置中心分叉：坚持 Apollo 并修订边界 ADR、Nacos 收窄；MySQL 托管 RDS 还是集群内 operator；是否与 iClaw 共用；首发 2.5.2 还是 3.0.x。
4. Kun 消费与治理：NuGet + CalVer 还是 monorepo；修复走上游 PR 还是补丁包；维护人与响应 SLA。
5. 数据库：是否接受 PostgreSQL 为唯一一等数据库；默认隔离策略 Shared + RLS 还是 SchemaPerTenant。
6. 插件机制边界（与 P 域重复）：第三方 `service` 上架；宿主是否全部 AOT/需 JIT 宿主；专属部署是否售卖；应用包「拷贝即拥有」还是「引用随升级」；ABI Extism 还是 WIT；MCP 注册归属。
7. 生产集群：与 iClaw 共用同一 K3s 还是独立集群只共用 IdP/Harbor；多租户隔离三档的目标客户与比例。
8. 模型服务网关归属：并入团队 MaaS 还是另起；GPUStack 接入范围；Adapter 维护方；GPU 归属。

### 2.2 P · 插件化内核与 Agent 原生架构

**领域全景**
- 「插件思维」11 条可检验原则（内核最小集闭合、系统能力也是插件、清单从代码派生、信任等级决定形态、一份契约多处生成、身份 ≠ 授权、制品按 digest 不可变等）。
- 插件模型：kind × role × distribution 三轴 + bundle；清单 `qian-plugin.yaml` 字段草案与 10 类派生物。
- 生命周期与治理：两层状态机、迁移「扩展 → 迁移 → 收缩」、升级同意规则、五级信任与签名审核、K3s 交付控制器。
- 内核最小集 12 项、首批系统插件 15 项、市场演进 M0–M4；Agent 就绪（与 iClaw 分工、四条路径、16 条防返工）；前端插件化（MF2、版本化插槽、按信任等级选沙箱）。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | 清单 v1 Schema + 校验器 + 源生成器；注册表 + 两层状态机 + 逐租户幂等钩子；扩展点/契约仓库；鉴权后过滤的能力目录；修订回切、digest 引用、强制验签；GitOps 执行器；授权 v0 与 Apollo 开关分离；SDK/CLI/TCK；外壳 + MF2 + 插槽注册表；统一主体模型与委托链字段；可解释权限判定 API；审计含主体链与来源通道；bundle v0；K3s 交付物 | Operation/Task；模型网关底座接线；域名租户解析；Saga 平台内部长事务；多 Pod 共享限流 | 热插 DLL；参考平台式程序集目录加载；Dapr sidecar 默认依赖；进程共享 JS 引擎；接口体 AES 加密中间件（本质是混淆） |
| S2 | `package` 元数据插件；`function` Wasm 运行时；逐租户批量迁移；升级同意规则；`template/managed` + 元数据分层；Operation/Task；元数据生成闭环（人用）；字段敏感度；工作流即能力；出网 SSRF 基线；业务插槽 + yue 组件目录机器可读；WebSocket 背板；调度；权限管理界面 | `connector` 运行时；开发者租户与远程调试 | 代码生成作低代码主轨（双轨漂移）；Nacos 作 Qian 配置中心 |
| S3 | 私有目录市场（目录/安装向导/试用/升级中心/审核台）；ISV 入驻与席位；Plan/Seat/Meter/Trial；三方合并冲突预检；授权服务器对齐 MCP（RFC 9728/8414/8707）；工具描述哈希钉死；成本与配额；不可信 UI 沙箱；第三方 `service` 独立工作负载身份 | 第三方 `service` 上架（待拍板）；支付、评分；Pact can-i-deploy；禁止导出 managed 应用 | 引入 NocoBase/Dify/Grafana/n8n 代码（许可证） |
| S4 | 每租户 MCP 逻辑门面（Higress 托管）；Agent 生成 `package`；Skills over MCP；市场兼任私有 MCP registry | A2A 端点、急停；MCP Apps 视图；WebMCP | 自建 Agent 引擎、沙箱、记忆、技能（复用 iClaw） |

**关键规则与数据对象**
1. 清单：`id` 反向域名发布后不可改；插件 SemVer 与 Kun CalVer 独立；`engines.qian/kun/yue` 声明兼容；OCI 只接受 `@sha256:`；签名不在清单里。
2. `capabilities` 条目是 Agent 可发现的最小单元：`sideEffect read|write|external|destructive` + `policyHint`（采 iClaw 四值）、`requiredScopes`、`schemaSha256` 进授权（schema 变即旧授权失效）。
3. 两个方向的权限：`provides.permissions`（本插件定义的权限点）与反向权限 `permissions.platform/data/egress/secrets/ui`（租户管理员开通时逐项批准，升级新增即重批）。
4. 平台级 Draft → Submitted → Verified → Published → Installed → Active → Deprecated → Retired；租户级 Available → Entitled → Provisioning → Enabled ⇄ Disabled → Upgrading / Uninstalled → Purged；Entitled（授权库）与 Enabled（Apollo 开关）分开存。
5. 「未启用」在网关路由、SDK 入口、前端外壳三处同时拦截；信任等级 system/official/partner/private/community 决定允许的 kind 与 UI 沙箱；未签名直接安装失败。
6. 迁移「扩展 → 迁移数据 → 收缩」，不承诺自动回滚；`inputSchema` 禁止 tenant 字段；所有写操作走带 `sideEffect/idempotencyKey/dryRun` 的命令。

**在 Kun 上实现的差距与难点**
- AOT 与动态性必须尽早拍板：Kun 严格 AOT 层不能热插 DLL，「不重建镜像上新官方插件」只能走 JIT 宿主或进程外。
- 元数据运行时只能住进非 AOT 孤岛；大量租户下逐租户迁移与三方合并各平台都没有完美方案。
- 权限模型从零自建且要同时服务 UI/API/MCP；Kun 的安全默认值要写成内核强制项。
- Wasm 宿主缺口：wasmtime-dotnet 不支持组件模型，WIT 要多维护一个 Rust 宿主，C# 产 Wasm 不成熟。
- MF 零隔离与共享依赖漂移；每个 `service` 一个 Deployment，三节点集群吃紧；iClaw 的 MCP Catalog / AI Registry / 市场均设计态；协议工具链仍预览级。

**插件化与 Agent 可用性要点**
- 分工：Qian = 能力提供方 + 业务主数据权威；iClaw = Agent 运行时 + AI 资产控制面；Higress = 共用数据面；Kun = 共用基座。
- 发现：能力目录按权限过滤；工具定义机械生成、暴露经 `agent.expose` 策划。调用：一律经 Higress MCP 路由，令牌受众绑定、禁透传、最小 scope，写类工具幂等。
- 编排：Arazzo 描述多步流程；Operation/Task 对齐 MCP Tasks 与 A2A。生成：Agent 生成应用 = 生成 `package`，经校验 + TCK + 差异审阅 + 人工确认；代码只落 `function` 沙箱；记录 `generatedBy/promptHash`。
- 与 iClaw 对齐 S2S claim、事件 envelope、工具元数据、授权快照、模型计量、契约格式（OpenAPI 3.1 + JSON Schema 2020-12）。

**待拍板问题**
1. S3 是否允许第三方 `service`（任意代码容器）上架，还是只开放 `package/function/connector/受限 ui`？
2. 内核宿主是否全部 Native AOT？是否需要 JIT 官方插件宿主以不重建镜像上新？
3. 租户与身份权威归属：Qian 内核还是 iClaw Kun.Edge 签发 S2S JWT？是否共用同一 IdP？托管 IdP 还是自建？
4. 配置与开关：是否给 iClaw `IConfigProvider` 加 Apollo 实现；授权与开关的权威存储；namespace 按「插件 × 租户」如何建模。
5. AI 资产/MCP 注册放 Nacos 还是 Qian 注册表并兼容 `server.json`？两个市场是否合并，谁审核、谁签名？
6. 应用包默认 `template` 还是 `managed`，或两者都支持？共享与专属部署是否作套餐差异售卖？
7. `function` 插件 ABI 选 Extism 还是 WIT 组件？S2 前拍板。
8. 默认隔离策略 Shared + RLS 还是 SchemaPerTenant？PostgreSQL 是否唯一一等数据库？

### 2.3 S · 租户、组织、身份与 SSO、权限、系统管理

**领域全景**
- A 租户控制面（主档、隔离与开通、白名单、生命周期、管理员）；B 组织目录（组织树、岗位、责任人、用户、同步）；C 身份与 SSO（Person + Membership、平台作 IdP、连接器 SPI、MFA）。
- D 会话与安全策略；E 权限（权限点注册、RBAC、数据/字段权限、高级模式、决策端口）；F 系统管理（配置 Schema、密钥托管、字典、菜单注册、审计）。
- G 控制台/工作台/个人中心（URN、偏好 KV、委托）；H 消息入口（类型注册、站内信、渠道适配器）；I 移动端（多端同契约、深链）。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | 租户主档、共享库 + RLS 开通、上下文 fail-closed、插件白名单、最小状态机 + 事件、单管理员 + 配额、切换租户令牌重签、运营后台 CRUD；组织树、岗位、用户档案；Person + Membership、唯一 issuer、Argon2 密码登录、连接器 SPI + 通用 OIDC、多租户账号选择、client_credentials；会话中心、后通道登出、登录与密码策略、在线用户、登录审计、`identity_revision`、CSRF；权限点注册、决策端口、API 强制校验、RBAC、超管显式、默认角色模板、决策审计；配置 Schema 化、密钥托管、字典、i18n、菜单清单注册、操作审计、上下文变量契约；URN、偏好 KV；消息类型注册、站内信 + 邮件、管理员接收配置；多端同契约、深链协议 | 独立 schema；品牌字段；导入导出框架；企微/钉钉/飞书连接器（至少一个）；TOTP；最小授权页；编号规则 API；活动流；短信渠道 | 内部 IM（非主线，参考实现不可水平扩展）；自研服务器监控（多副本下单机指标无意义）；请求日志落库 + 一键清空 |
| S2 | 责任岗位/责任人/主管解析契约；导入向导接入；数据权限编译器、字段权限、创建者自动 Owner、授权界面含字段/数据；编号规则界面；应用菜单 + 多端；个人中心扩展 | 用户分组；工作交接扩展点；内核表 ACL；自定义策略包；在线翻译；行政区划；资源包导入导出；Widget 壳；委托 grant 原语 | — |
| S3 | 完整状态机与数据销毁；套餐下发；多管理员与邀请；自助申请审核；目录同步（国内源 + SCIM）；租户多连接 + HRD；国内三家 + ISV；自助注销与导出；OpenFGA；应用中心/后台权限区分；面向客户工作台；扫码 | 岗位约束；白标域名；微信/短信 OTP；SAML/CAS/LDAP；Passkey；组织角色/逐级递减/身份；Explain；公告管理；H5/PWA；App 推送 | 本期原生 App |
| S4 | Agent 主体、`act` 链、CIMD/RFC 9728；AI 助手入口 | — | — |

**关键规则与数据对象**
1. 租户状态机：待激活 → 试用 | 正常 →(到期) 宽限 → 只读 → 冻结 →(保留期满) 注销；任一状态可被运营方 Suspend；每次迁移发事件并联动会话吊销；租户号与隔离策略创建后不可改。
2. 功能授权是白名单（租户 → 插件/应用 → 版本），替代参考平台黑名单；S3 套餐 = 预定义白名单 + 配额集合。
3. Person（全局自然人）+ Membership（租户成员）；命中多个 Membership 签 5 分钟选择令牌；第三方绑定同租户唯一、需显式确认。
4. 令牌只放身份、`tenant_id`、`membership_id`、scope、`identity_revision`；权限不进令牌，按主体缓存并以 `identity_revision` 失效；网关换签 60s S2S。
5. 会话失效触发：改密、停用、锁定、租户冻结/到期、强制下线、权限变更；后通道登出双向；并发上限默认 5。
6. 权限七层 L0 租户隔离（EF + RLS）→ L1 角色 → L2 功能权限 → L3 资源实例 → L4 数据权限（条件树 + 动态变量编译为谓词，同角色 AND、角色间 OR，无授权默认拒绝）→ L5 字段 → L6 ABAC；超管旁路显式并审计。
7. 用户「删除」= 禁用 + 标记；SSO 同步删除只置禁用；基数超限拒绝并返回结构化错误。
8. 核心对象：Tenant / TenantEntitlement / Plan；Person / Membership / ExternalIdentity / Connector / Session；Organization / Position / Group；Role / RoleAssignment / PermissionDefinition / Grant / DataScopeRule / FieldPolicy；MenuEntry / ResourceRef(URN) / UserPreference / ConfigSchema / SecretRef；MessageType / SendConfig；AuditEvent。

**在 Kun 上实现的差距与难点**
- Kun 缺口 G1–G8：无鉴权抽象；外部 issuer 只能配一个；OIDC 客户端不发 `code_verifier`、不校验 nonce、无 refresh/client_credentials；会话无 user→sessions 索引；CSRF 推迟；三套签发器；claim 名不统一且 header 兜底 fail-open；`SessionData` 单 TenantId。
- 参考平台不可照搬：接口层恒 true；租户解析 fail-open；无服务端吊销；权限无缓存；数据权限只对自生成查询生效；匿名回调返回明文密码；WebSocket 无背板；配置 100+ 键扁平堆放。
- 自建身份核心的安全责任（OIDC 一致性测试、渗透、补丁节奏）；国内生态 ISV 模式开源 IdP 普遍缺，需自研连接器。

**插件化与 Agent 可用性要点**
- 拆分：租户控制面（独立服务）、Qian Identity（内核 TCB）、认证因子与连接器插件族、Directory/Sync 族、Access/`Kun.Authorization`（OpenFGA/OPA 为适配）、导航、消息内核与渠道适配器族、工作台、审计日志、租户自助。
- Agent 预留：OIDC discovery / RFC 8414 / 9728 / 8707；Agent 客户端类型 + RFC 8693 `act` 链，有效权限 = 用户权限 ∩ 授予 scope；权限点带风险级别与 step-up；危险操作 Agent 只能发起申请；Check/ListAllowedActions/Explain 作工具；目录语义化查询 + SCIM schema；数据权限 AST 可序列化；URN 目录即能力索引；CloudEvents；「待确认票据」；审计闭环。

**待拍板问题**
1. 交付形态：是否有私有化/离线交付？决定 AGPL 与商业授权组件能否使用。
2. issuer 设计：租户自定义登录域名是否刚需？决定单一规范 issuer 还是每租户 issuer。
3. 账号归属：以全局自然人 + 租户成员为主，还是允许「租户托管身份」？并存时的合并规则。
4. 协议范围：首期是否必须 SAML/CAS/LDAP？是否接受先用代理 IdP 连接器过渡。
5. 高级权限模式：是否有客户在 S2 就需要身份切换、组织角色或约束？
6. 隔离策略产品化：独立 schema/库是否作付费选项？是否承诺隔离策略迁移服务？
7. 配置分工：租户级在线配置放平台配置服务、Apollo 只放部署级默认值，是否被架构接受？
8. 合规要求：是否有等保 2.0 / 商用密码（SM2/SM3）要求？MFA 是否 S1 必做？
9. 跨产品 SSO：Qian 与 iClaw 是否共用账号体系与 Authentik？
10. 配额口径：管理员是否计入用户配额；平台超管是否按租户可见。
11. 参考平台 SaaS 租户服务未随源码交付，自助申请审核、邀请格式需自行设计。

### 2.4 F · 应用、表单设计、列表设计、视图、功能发布

**领域全景**
- 1 应用（生命周期、成员授权）；2 功能生命周期（三态发布与回滚、预览、复制/导入、代码生成、系统表单登记、外链）；3 表单基础设置与数据绑定（建表、主副子表、类型映射）。
- 4 表单设计器；5 控件 59 + OCR 9；6 表单属性；7 列表设计（6 种布局、查询/列表字段、按钮、权限四开关、个人视图）；8 视图。
- 9 发布到菜单与权限资源；10 运行时数据引擎（Records、查询组装、鉴权注入、事件 Hook、数据日志）。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1（模型与契约） | 应用实体与成员授权；菜单与资源管理模型；可发布资产模型（草稿 + 不可变版本快照）；删除保护依赖图；系统表单登记 → 插件清单；表名规范契约；Field Type Catalog；控件能力矩阵；组织类选择器 API；审计列 + 序号服务；数据规则策略（主键/乐观锁/逻辑删/业务主键）；服务端 Hook；权限四开关模型；数据权限方案模型；鉴权在引擎唯一入口；导入导出引擎；数据事件 Outbox；数据日志 schema；Filter AST + 运算符矩阵 | expression-engine 提前 | — |
| S2 | 功能列表与创建；回滚到任意版本可 diff；预览；复制/导出/导入；三栏设计器、单一 Schema + 端可见性、结构化校验错误 + 定位；基础输入/选择/日期/上传/组织类/子表/下拉树与弹窗选择/关联表单/公式/系统控件/六种布局；表单规则与按钮（弹窗/接口/发起审批）；列表设计器「一份语义 + 端覆盖」、普通表格、过滤排序分页、查询/列表字段、固定按钮、导入导出、审批按钮矩阵、权限开关产出可授权项；Records API、参数化查询、批量 Resolver；发布向导、权限同步、数据权限方案自动生成 | 跨应用设置；应用导出导入；数据管理；薄壳脚手架；模板套件；脚本沙箱；次要控件（码类/省市区/Iframe 等）；页签管理；左树/行内编辑/分组/树形布局、复杂表头、高级查询、合计、批量打印；视图 | 参考平台形态代码生成（第二真相源、能力漂移）；匿名外链照搬（读草稿、不校验、无有效期）；树形表格清空数据权限取全量 |
| S3 | 应用包安装（app-bundle） | 应用中心体验；外链能力令牌；签章/签名/定位；卡片布局；查询方案保存；个人视图 | — |
| S4 | AI 建表/生成字段；OCR 控件库；Agent 工具契约落地 | — | — |

**关键规则与数据对象**
1. 功能状态机 未发布 → 已发布 → 已修改 → 已发布；运行时除设计器预览外一律读当前生效版本；发布拆成编译 → 迁移 → 切流 → 挂载并记发布日志。
2. 记录生命周期：新建 = 权限裁剪 → 必填 → 唯一/业务主键 → 系统字段 → 主副子表同事务 → created 事件；编辑 = 乐观锁 → 子表按 id 比对 → 数据日志 → updated；子表删除策略 关联删除 / 删除校验 / 不删除。
3. 数据权限合并（抽源码裁决）：同一角色内多方案 AND，不同角色 OR，任一「全部数据」整体放开；未授权 = 看不到；行级策略覆盖所有读写路径（参考平台只管列表）。
4. Filter AST `{logic, groups[{logic, conditions[{field, op, value|sysVar}]}]}`，13 个运算符 × 字段类型矩阵，系统变量目录（currentUser/Org/Position 及其下属/下级、formId、keyword、flowId）；数据过滤、高级查询、数据权限、启用规则、流程条件五处共用。
5. 权限「双重门」：按钮/列/字段须既在设计器勾选又被授权；但鉴权不依赖设计者勾选与客户端 `menuId/flowIds`，资源化 ACL 在数据引擎唯一入口执行。
6. 固定系统列 `f_id/f_tenant_id` 联合主键、`f_flow_*`、`f_delete_*`、`f_version`、子表 `f_foreign_id`；编码「前缀 + 年月日 + 六位序列」，名称应用内判重、编码全局判重，跨应用导入用命名空间化稳定编码 + id 重映射表。
7. 核心对象：App、FunctionDef、FunctionRelease、FormSchema（Data/UI/Rule/Binding 四段）、Widget、ListSchema（base + overrides）、DataModel、FieldTypeMapping、FilterExpression、DataAuthScheme、Menu、PermissionItem、CapabilityManifest、DataLog、PublicLink、ImportJob。

**在 Kun 上实现的差距与难点**（输入笔记无 Kun 材料，由硬性约束推出）
- 元数据 Schema 缺失：参考平台 70 种控件共用 947 行 POCO、前后端隐式契约 → 版本化 JSON Schema，后端保存校验、yue 与运行时共用。
- 业务列迁移管道从零建（参考平台只补系统列）→「元数据 diff → 迁移计划 → 审核 → 执行（含租户批量）」。
- 动态引擎 SQL 安全：约 47 处拼 SQL、逗号连接代替 JOIN、租户值字面量 → 参数化 + 标识符白名单 + 查询构建器。
- 鉴权分散在控制器、依赖客户端传参、移动端控制器无校验 → 引擎入口「资源 + 动作 + 行级」三段式。
- 发布无事务 → 可重放幂等步骤 + outbox；全量缓存 + N+1 → 按需批量 Resolver；控件三态各自硬编码 → 一次声明 edit/readonly/cell；前端 `eval` 暴露令牌 → 沙箱 + 服务端 Hook。

**插件化与 Agent 可用性要点**
- 内核极小（插件宿主与 SDK、租户身份上下文、元数据与能力注册表、鉴权策略引擎、系统变量、查询 DSL、事件总线、网关）；S1 插件 app-registry / navigation / authz / data-store / schema-migrator / widget-registry 等；S2 form-engine / designer / widgets-* / expression-engine / dynamic-data-engine / list-engine / publish-wizard 等；S3 app-bundle / public-link。
- 八个扩展点替代写死枚举：控件类型、列表布局、数据来源、动作类型、权限资源类型、菜单类型、字段类型映射、客户端动作。
- Agent：每个已发布功能自动产出 JSON Schema + OpenAPI + 语义描述；`GET /registry/widgets` 一份元数据两用；校验器前后端共用 + JSON Pointer；声明式优先于脚本；最小工具集 `listWidgets / generateFormFromTable / validateForm / publish(dryRun) / mountMenu / records.* / authz.check`；错误枚举化；表单引用图；「匿名可用」标志。

**待拍板问题**
1. 代码生成是否保留：A 不做；B S2 可选薄壳脚手架；C 参考平台式完整 CRUD 源码。
2. 低代码存储模型：A 每功能一组物理表；B 统一宽表/JSON 列；C 混合（主表物理 + 多值字段 JSON 列）。
3. 多租户隔离默认策略与允许范围；是否允许业务库由租户提供。
4. S2 是否交付移动端：A 仅 PC 响应式；B PC + 移动端渲染（PWA）；C 两套配置。
5. 视图（数据集驱动列表）归 S2 还是 S3。
6. 脚本能力边界：A 仅声明式规则 + 服务端 Hook；B 另加前端沙箱 JS；C 另加服务端受控脚本。
7. 数据权限合并语义沿用「同角色 AND / 跨角色 OR / 全部数据放开」，还是改为可配置。
8. 功能编码是否命名空间化（租户.应用.功能）；同一功能是否允许挂多个应用菜单。

### 2.5 W · 流程与审批

**领域全景**
- 1 流程定义与生命周期（五类流程、版本三态、启停）；2 建模（节点类型、条件表达式、全局属性）；3 人员解析（七种来源、范围过滤、兜底链、委托）。
- 4 审批方式（或签/会签/依次、系统审批、拒绝策略）；5 处置操作 27 类；6 表单与数据（绑定、字段权限、数据传递）；7 时效与通知。
- 8 集成与自动化（领域事件、外部节点、触发节点 → 任务流程、决策）；9 运行态与工作台；10 S4 预留。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | 只交付前置：组织模型（岗位、责任岗位、上下级、第 N 级责任人查询）、事件总线 Outbox、按业务键可取消的调度、表达式/条件引擎规范、消息模板注册、元数据版本化契约、鉴权资源 `workflow:{key}:launch`、文件存储、Higress 回调入口、插件注册契约 | Elsa 3 PoC 对照；流程内核 API 提前到 S1 末 | — |
| S2（最小集） | 一套 JSON 定义 + 节点 JSON Schema + `:validate`；版本三态后端强制只读；启停与发布范围鉴权；瀑布/节点树设计器（schema 驱动面板）；发起/审批/结束节点、三种网关、抄送；条件组 + 函数起步集 + 系统/全局变量；审批人四种来源（指定成员、发起者、上级责任人多级、表单变量）；兜底三档；或签、会签（人数/比例 + 停滞兜底）、依次；动作 同意/拒绝/退回/转审/暂存/撤回/终止/指派；审批意见；表单绑定锁数据模型；字段权限后端强制；实例/节点/工作项/流转记录/抄送；标题模板；工作台四列表 + 详情页；监控最小集；通知六状态；领域事件；`actions` 契约 | 自由画布视图；办理节点；选择分支；子流程；外部节点（契约必定）；触发节点与任务流程；其余审批人来源；会签延后计算；系统审批与自动去重；节点独立表单与数据传递；加签减签；限时/超时/催办；评论；声明式自定义动作 | 简单 → 标准切换（一套 DSL）；自定义按钮任意 JS（多租户安全、Agent 不可发现）；定时器节点（参考平台亦未实现） |
| S3 | 基线标 S3 未分必做/可选：拒绝 `continue`、归档、签收、委托/代理、撤销申请、暂停/恢复、协办、超时自动处置、决策流程（调用式）、自由流程、批量审批、节点打印、签名、导入导出 | — | 嵌入式决策流程（与调用式重复）；在途实例迁移新版本（本期，需求不明）；移动端特性（本期） |
| S4 | AI 建流程（只生成「设计中」草稿）；Agent 作审批人解析器/外部审批者/事件消费者/自动化动作 | — | — |

**关键规则与数据对象**
1. 版本 设计中 → 启用中 → 已归档，已归档可再启用；只有设计中可编辑，后端拒绝其它版本写入；在途实例绑定发起时版本。
2. 实例状态 S2 最小：待提交、进行中、已通过、已拒绝、已终止、已撤回、已退回；暂停/撤销/异常预留。
3. 节点完成：或签 = 首个终结动作结束节点；会签阈值按人数或比例、实时/延后；两阈值都不可达时发 `node.stalled` 转人工，不得静默停滞。
4. 自动化优先级 系统审批 > 异常处理 > 自动审批去重；下一节点是候选/选择分支或兜底「无法提交」时失效；自由节点开启后全部失效。
5. 撤回 ≠ 撤销 ≠ 催办：撤回针对进行中且下一节点未动作；撤销针对已通过实例派生反向审批；催办仅超时后站内信。退回三模式 × 四目标，需路径快照。
6. 上级责任人按岗位链推导（岗位 → 上级责任岗位 → 现任责任人），不是直接上级；解析结果与有效用户取交集、为空走兜底链。
7. 任务流程集合语义显式 `cardinality: single|each|batch`；破坏性动作声明 `destructive` 并试运行。对象：FlowDefinition / FlowVersion / NodeDefinition / Edge-Condition / Variable / FlowInstance / NodeInstance / WorkItem / AddSignGroup / OperationRecord（只追加，actor 人/Agent/系统）/ CcRecord / ReturnSnapshot / Timer / RevokeRequest / Delegation / ExternalCallbackLog / AutomationRun。

**在 Kun 上实现的差距与难点**
- 引擎路线：参考平台 .NET + Java Flowable 双进程双库；Elsa 3.9 缺会签/加签/退回；Flowable 8 需 JVM；Camunda 8 需商业许可 → 自研 .NET 轻量审批内核，借鉴 warm-flow / FlowLong 表模型。
- 多副本：进程内事件总线、本机自调用、非原子缓存锁、内存延时队列 → Outbox + 平台调度 + 乐观并发/分布式锁。
- 表达式：拼 SQL 到租户库 + `static` V8 任意 JS → JSON AST 内存求值 + 函数注册表 + Jint 限额；前端 quickjs 沙箱。
- Schema 化：节点无 schema、属性面板 18.8k 行 → 每种节点发布 JSON Schema + `uiMeta`，网关/分支约束全部进 `:validate`。
- 组织模型前置否则 S2 动态审批人做不出；逆向操作交叉语义需回归集与属性测试；外部回调 `{correlationId, outcome, fields, actor}` + 签名 + 幂等键 + 超时。
- 工作量：审批域大（12–15k 行）、引擎中（5–8k）、设计器大；S2 流程域是 3–4 人数个迭代的主线。bpmn-js 要求水印可见，选 React Flow。

**插件化与 Agent 可用性要点**
- 插件：workflow-core、designer、workbench、admin、assignee、notify（S2 必做）；sla、integration、node-subflow、automation 等（S2 可选）；freeflow、decision（S3）。内核不内置通知/打印/归档/签名。
- 扩展点：NodeType、IAssigneeResolver、IAssigneeFallbackPolicy、ICompletionPolicy、IWorkItemAction、GatewayStrategy、VariableSource、TimeoutHandler、TriggerSource/ActionNode、函数注册表。
- 契约：`:validate/:publish/:enable`；注册表 `GET node-types / assignee-strategies / actions`；`GET /workitems/{id}/actions` 返回带 `inputSchema/preconditions/humanOnly` 的动作，`POST` 带 `idempotencyKey`；CloudEvents 事件 `instance.* / node.* / workitem.* / external.*`。
- Agent：动作即工具（签名 `humanOnly`）；Agent 代办走委托模型；生成只落草稿经 `:validate`；解析与自动决策附「为何」；四个挂点（推荐审批人、判定、执行、充当审批者）。

**待拍板问题**
1. 引擎路线：是否接受「自研 .NET 审批内核为主、Elsa PoC 对照、Flowable 仅作可选适配器」；是否需要 BPMN 导出。
2. 一套 DSL 两种视图：是否确认简单/标准合并为一套 JSON 定义；S2 是否只交付瀑布/节点树视图。
3. S2 最小集边界：加签/减签、候选人、办理节点、子流程、时效能力均列可选，需确认哪些进必做（尤其加签与催办）。
4. 拒绝后继续流转：保留为 S3 还是不做；撤销申请「遇拒绝自动通过」是否沿用。
5. 兜底人模型：租户管理员角色 vs 指定异常处理人；「无法提交」是否默认档位。
6. Agent 代办身份：以用户身份代办还是独立系统主体；哪些动作对 Agent 禁止。
7. 命名与市场：流程层「启用/停用」替代「上架/下架」；流程模板、决策表节点包、自动化模板是否作市场商品。
8. 决策与自动化：automation 是否提前到 S2 必做；decision 是否 S3 可选。
9. 需技术验证（合并）：合流语义与幂等重放、多副本计时器互斥、责任岗位链查询性能、回调鉴权与重放窗口等。

### 2.6 D · 数据中心、集成、消息与调度、门户、报表、打印、大屏、文件预览

**领域全景**
- D1 数据中心（基准类型、方言、连接、建模、数据接口、值翻译、引用登记）；D2 集成中心（连接器凭证、消息通道、身份源同步、开放接口、自动化）。
- D3 消息中心（发送编排、渠道 Provider、站内信、接收配置、监控重试、公告、实时推送）；D4 任务调度（调度器、回调契约、任务类型）。
- D5 门户（实体、设计器、控件注册表、筛选联动、多端）；D6 报表；D7 打印；D8 大屏；D9 文件预览；D10 AI 中心（仅模型服务网关）。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | 基准数据类型、PG 方言、数据连接（凭证引用）、数据接口 SQL 契约 + SDK、值翻译、引用登记；凭证保管、短信 Provider、SMTP 邮件；通知模型与发送编排、渠道 Provider、站内信、结构化记录；实时推送；调度器、回调契约、执行日志；可发布元数据框架、导入导出框架、控件注册表契约、i18n；外部身份映射 | MySQL 方言；基准字段代码约定；内置启动作业；文件预览票据；`ai-gateway` 以 Higress 路由先行；水印租户策略 | 脚本型定时任务（任意代码执行）；跨库数据同步（本期） |
| S2 | 控件映射、数据建模、接口设计器、API 接口与日志、复制/导入导出；短信通道 UI、群机器人、模板同步；监控 UI、数据接口型任务、任务管理 UI；打印全套（实体、版本、设计器、绑定、挂载、浏览器打印）；文件预览提供者 + 格式清单；模型目录与按用途路由 | Mock 接口；写接口；自动化与 Webhook；接收配置；公告；报表子集；门户配置路径型；多联打印；云厂商模板邮件 | — |
| S3 | 门户设计型全套（设计器、三类控件、筛选联动、多端、分配、挂菜单）；报表（实体、版本、数据集、查询排序、工具栏、预览导出 XLSX、挂菜单）；开放接口；分享链接内核；身份源同步 | 公众号/钉钉/企微/飞书消息；敏感词；短链；App 推送；高级图表与地图；自定义布局；分栏；服务端 PDF；大屏全部（只消费内核接口） | 旧版报表设计器（两套设计器、直连任意库）；大屏自有库与任意 SQL 数据集；用户自写 Vue 组件；大屏参考源码复用（许可证高风险）；在线编辑（本期） |
| S4 | 接口即工具、注册表 → MCP 映射、模板 validate/render/explain；AI 中心智能体/提示词/知识库/MCP 管理 | — | — |

**关键规则与数据对象**
1. 横切规则只实现一次：编码「前缀 + YYYYMMDD + 六位序列」；名称应用内唯一、编码全局唯一；复制「.副本 + 五位随机」且默认未发布；导入跳过/追加并输出待重连清单；被引用保护。
2. 可发布元数据统一 `draft / published / archived` + 当前生效指针；「启用」归档版是把指针指回的动作；同一时刻只一个生效版本；运行态只读发布快照。
3. 数据接口：定义层带读写属性，消费侧叫「数据绑定」；SQL 文本模式应为高级权限 + 只读账号 + 超时与行数上限；「服务出参」保存后不可改。
4. 凭据只能一处来源（connector-vault），通道配置与 Apollo 只存引用，详情接口不回传密钥。
5. 消息：发送配置 → 多模板（每模板一渠道）→ 渠道账号；接收偏好 个人 → 管理员 → 放行；异步须有「发送中」态；每收件人一条记录，重试按失败收件人定向。
6. 调度回调契约：服务名寻址、签名、幂等键、超时、重试、受限服务身份、结果回写；不持久化实例地址。分享链接与预览票据由内核签发并服务端校验。
7. 对象：数据域 BaseDataType / DbDialect / DataConnection / DataModel / DataApi / DataBinding；集成消息域 ConnectorCredential / ChannelAccount / NotifySendConfig / NotifyTemplate / NotifyRecord / InboxMessage / ExternalIdentity / ApiClient；调度实时域 JobDefinition / JobRun / RealtimeSession；呈现域 Portal / WidgetManifest / ReportTemplate / PrintTemplate / ShareLink / PreviewTicket / MetadataPackage。

**在 Kun 上实现的差距与难点**
- 进程内状态必须外置：WebSocket 连接池、公告延时队列、`Thread.Sleep` 重试、无集群配置的调度器。
- 事件总线：参考平台 RabbitMQ 单队列非持久化先 ack 后处理 → 统一信封 + 持久化 + 死信 + 广播与竞争两种订阅。
- 回调契约替代「HTTP 自调用 localhost」；凭据三处明文、共享对称密钥带默认值 → 内核凭证保管。
- 数据接口多租户安全：任意 SQL、任意 URL 代理携带用户 token、匿名上传下载 → 白名单、鉴权、限额。
- 报表引擎语言绑定（Java 引擎约 2.2 万行）→ 先列表 + XLSX 再渐进到分组汇总、交叉表；服务端出图 System.Drawing 在 Linux 失败 → SkiaSharp；服务端 PDF 成本未评估；预览服务 → Helm 子 chart 钉版本；77 个 JSON 文本列无 Schema → jsonb + 注册 Schema 版本。

**插件化与 Agent 可用性要点**
- 插件：data-core、db-dialect-*、data-connect、data-api、connector-vault + connector-*、notify + notify-channel-*、realtime、scheduler + job-type-*、identity-sync-*、open-api、automation、portal + portal-widgets-*、print、report、file-preview、ai-gateway；内核横切（可发布元数据、导入导出、引用登记、分享链接、字典、i18n、能力注册表）。
- 能力注册表是唯一目录，UI/OpenAPI/MCP 三种消费同源；数据接口 = 工具（用途说明、Schema、危险级别、幂等性、scope、限速），写接口加「需人工确认」。
- 声明式模板 + `validate / render / explain`，「生成格式就是导入格式」；元数据操作 API 化并暴露状态机；通知以四操作（列配置/校验/发送/查监控）暴露，凭据不出现在契约；模型目录含类型/能力标签/配额；MCP 暴露经注册表生成，本期只预留映射约定。

**待拍板问题**
1. 门户与报表阶段归属：任务书列 S3，调研笔记建议门户设计器 S2、报表子集 S2 后期；S2 应用能否没有可视化首页与报表。
2. 报表深度：S3 是否需要交叉表/分栏级复杂报表，还是「列表 + 分组汇总 + XLSX」足够。
3. 大屏是否列入产品范围：S3 可选自建还是交由应用市场生态提供。
4. 移动端策略：门户/报表/打印 PC 与 APP 配置「响应式 + 端覆盖」还是独立配置。
5. 数据接口 SQL 文本模式：多租户 SaaS 下是否开放，还是只提供配置式查询。
6. 服务端 PDF：S2 就提供服务端 HTML → PDF，还是先只做浏览器打印。
7. 预览服务技术栈：接受 Java 进程作为 sidecar，还是选 .NET 生态/文档服务器方案。
8. 开放接口凭据：是否废弃用户级 AppSecret，统一为网关应用级客户端凭据并取消「公有」免鉴权。
9. 需进一步调研（合并）：数据集表迁移关系、与 Kun 数据访问层分工、表格设计器选型等；部分手册分卷未输入。

### 2.7 U · 前端架构与 yue 组件库

**领域全景**
- 参考平台前端全集去重后 251 条目（原语 31、表单叶子 34、布局 22、业务选择器 18、壳部件 17、设计器 10 等）；控件库 68 个 key 为 yue 物料骨架。
- iClaw 资产盘点：技术栈与令牌架构可沿用，组件库缺失全部自建，Agent 交互信息模型沉淀为 `@yue/agent`。
- yue 六层分层 + 横切包、S1 最小集合、技术选型推荐、工程化基线 21 项、移动端策略、复杂度热点与风险。

**分级结论**

| 阶段 | 必做 | 可选 | 明确不做及原因 |
|---|---|---|---|
| S1 | `@yue/tokens` 三层令牌、明暗两态、对比度单测、运行时主题注入；`@yue/core` 全集 + `data-slot`/`data-testid`；`@yue/shell` AppShell 单一布局、后端菜单 → 路由 + `menuTypeRegistry`、Page/ColPage/LeftTree、Preferences、租户切换器、兜底页；`@yue/data` DataTable 基础版、Tree/TreeSelect/Cascader、Range、Upload（分片 + MD5）；`@yue/form` Field + react-hook-form + Ajv、SchemaForm + FormApi、基础联动、编辑/只读态、校验与脱敏规则目录、弹层 API；`@yue/biz` ApiSelect/DictSelect/OrganizeSelect/UserSelect/RoleSelect/授权树；认证页族（密码、SSO、多租户选择、过期弹窗）；`@yue/sdk` 注册表 API、PlatformClient、拦截器链、i18n、mock；元数据契约草案（含 `device`、`auth`）；SSE 事件 JSON Schema；工程化基线；移动端契约层 | 多标签基础版；Captcha；DTCG 导出；Qrcode 提前；视觉回归 | 锁屏（前端比对密码仅 UI 遮罩）；两套设计体系混用；`eval`/`new Function` |
| S2 | Slider/Rate/ColorPicker/Steps/Timeline；DataTable 进阶（树表、列权限、行内编辑、复杂表头）；ImportWizard/ExportDialog、SuperQuery、ConditionBuilder；CodeEditor/Cron；PosSelect/GroupSelect、PopupSelect 族、RelationForm、授权树族；控件注册表交付控件集、表单渲染器三态、子表、三层联动、公式控件、列表渲染器（普通/左树/树形）、动作执行器、流程表单宿主、审批中心基础、端支持矩阵显式降级；设计器基础设施、表单/列表/审批流设计器、校验器共用；应用切换；组件 manifest 全量；视觉回归 | 密度模式；Command Palette；Watermark；Carousel/QrCode/Barcode；Transfer；富文本/Markdown；`@yue/charts`；全局搜索；分组/卡片列表；详情页签/数据日志；`@yue/mobile` H5 渲染器 | bpmn-js（许可证要求水印可见）；Formily/amis/lowcode-engine（停滞或自带体系） |
| S3 | 通知抽屉；门户设计器与运行时；不可信 UI 沙箱 | 多布局/偏好抽屉；主题预设；扫码登录/注册；OCR 页签；自动化流设计器；打印（pdfme）/报表（Univer）/大屏设计器与运行时；透视表；外链运行时；工作台；移动 H5；shadcn 私有 registry | 内置 IM（非平台核心）；第三方不可信 UI 直接 MF 挂载（零隔离）；小程序原生渲染（S1–S3 不纳入，待拍板） |
| S4 | `@yue/agent`（事件契约 S1 定义）；shadcn MCP Server | AI 助手悬浮窗/划词翻译；MCP Apps 视图；WebMCP | — |

**关键规则与数据对象**
1. 分层依赖严格自下而上（tokens → core → shell/data/form/charts/code-editor → biz → runtime/widgets → designer），用 eslint-plugin-boundaries / dependency-cruiser 写死进 CI。
2. 只保留一套表单/表格/弹层引擎贯穿 S1 后台 CRUD 与 S2 低代码运行时；业务组件只做叶子，不得新建引擎。
3. 控件注册表 `registerWidget(type, {meta, Runtime, Designer, toSearchControl, toCellRenderer})`；meta 含 props JSONSchema、valueType、`devices[]`、canBe* 能力位、defaultOperators、authAware；一份注册表同时作设计器左栏与 `GET /registry/widgets`。
4. 一份 schema + `device` 维度覆盖，不要两份元数据；物料端支持矩阵与显式降级。
5. 联动三层：声明式动作 → 可序列化表达式 DSL（JSONLogic 类）→ QuickJS 沙箱脚本（白名单 API、超时、内存上限、不暴露令牌）；小程序端只保证前两层。
6. 令牌 primitive → semantic → component，命名禁同名异义（`--accent` 冲突反例），状态色成对 + `Tone` 类型，WCAG AA 对比度单测进 CI，`data-theme/data-brand` 由服务端按租户下发。
7. 安全硬线：不用 `eval` 执行租户脚本；令牌不进 URL/iframe（一次性票据或 postMessage 握手）；前端权限裁剪只是体验层，鉴权在 Kun；不在前端持有加密密钥；不持久化 token/锁屏密码。
8. 元数据契约包（无 UI）：FormSchema / ListSchema / PortalLayout / FlowNodes 的 JSON Schema（数据部分对齐 2020-12），带 `$schema` 与版本；SSE 事件以 JSON Schema 为唯一真相源生成 TS。

**实现差距与难点**（本域主要是重建成本）
- 复杂度热点从高到低：表单运行时 + 子表（联动图、三处呈现）；动态列表运行时（六种形态）；数据密集基础组件自建（路线 A 最大人力）；流程设计器约 30 个节点属性面板；设计器基础设施（嵌套拖拽、撤销重做）；多标签 KeepAlive 等价能力。
- 风险：Base UI 成熟度未验证（S1 首迭代验证门，不通过整体切 Radix，不混用）；自建组件库人力与 S2 窗口冲突（备选 antd v6 限控制台域）；两套引擎复燃；DSL 表达力不足倒退回 eval；iClaw 代码复用授权不明；供应链（dnd-kit、Puck 0.x、Univer Pro 水印）。
- 与 Kun 的接口：API client 从 Kun OpenAPI 生成（orval / openapi-typescript）+ 契约测试；拦截器链（鉴权刷新队列、租户头、大数处理）。

**插件化与 Agent 可用性要点**
- 壳插槽与注册表（`@yue/sdk`）：导航、设置面、命令、快捷键、预览器、客户端动作（名称、参数 JSON Schema、权限、人话描述）——S1 定义、S3 客户可见、S4 Agent 消费。
- S1 起预留：机器可读 manifest；带版本的 Schema 与校验器（Agent 生成物走同一校验再落库）；客户端动作/导航/命令注册表。`@yue/agent` S4 交付，SSE 事件契约 S1 定义。
- 选型：Base UI + Tailwind v4 + shadcn 自有化；dnd-kit；`@xyflow/react` 12；react-grid-layout；TanStack Table v9 + Virtual；react-hook-form + Ajv；ECharts 6；Univer 开源核心；pdfme；Monaco + CodeMirror 6；quickjs-emscripten；Zustand + TanStack Query；富文本/Markdown 未决。
- 移动端：同一 React 渲染器端适配，H5/PWA 为主，小程序与 App 以壳嵌 H5 + 少量原生桥。

**待拍板问题**
1. 组件底座：路线 A 是否接受其 S2 交付风险；是否允许控制台域临时用 antd v6 并约定迁移期限。
2. 移动端投入：S2 是否交付 H5 表单渲染器；S3 是否纳入审批/工作台 H5 与小程序/App 壳；原生 App 是否在本期。
3. 后台壳多面策略：沿用「控制台 / 应用前台 / 应用后台」三面新窗口切换，还是一个壳按上下文切换。
4. 布局与偏好系统范围：是否接受 S1 单一侧栏、S3 再做多布局与偏好抽屉。
5. 富文本/Markdown 编辑器选型：笔记未覆盖，需补选型调研。
6. iClaw 代码复用授权：iClaw 与 Qian 是否同一权利主体、能否直接搬代码。
7. yue 分发渠道：私有 npm 之外是否建 shadcn 私有 registry + MCP Server 面向 ISV 与 Agent。
8. 审批流设计器形态：纵向节点树（对 Agent 生成最友好）还是 React Flow 自由画布；是否需要 BPMN 导出。

---

## 3. 跨领域汇总

### 3.1 S1 必做清单（括号为来源领域）

- **Kun 修补与多租户**：租户回灌、至少一次消费、Saga/Outbox 锁、网关加固、gRPC 流式、签发器与审计收敛；租户目录与生命周期、fail-closed 上下文、插件白名单；PG 唯一一等库、`qian.data` 默认安全装配（K/P/S）。
- **身份与鉴权**：OpenIddict 唯一 issuer、Person + Membership、会话中心、后通道登出、连接器 SPI + 通用 OIDC、client_credentials、`identity_revision`、CSRF（K/S）；`Kun.Authorization` 端口、租户域 RBAC、权限点清单声明、API 强制校验、决策审计、可解释判定（K/S/P）；数据权限契约与系统变量（S/F）；统一主体模型与委托链（P）。
- **插件机制**：清单 v1 + 源生成器、注册表、两层状态机、扩展点/契约仓库、能力目录、强制验签、修订回切、GitOps 执行器、授权 v0、SDK/CLI/TCK、bundle v0（P/K）；系统表单登记 → 插件清单（F）。
- **配置、事件、审计、部署**：Apollo 部署 + 自研 provider + secretRef + 配置 Schema（K/S/P）；凭证保管（D）；租户级配置服务（K/S）；Outbox 至少一次 + 事件目录；审计一套 + PG 哈希链 + `agent` actor；baggage；RabbitMQ K8s 化、镜像与 chart、健康端点、default-deny、ResourceQuota（K/P）。
- **数据、文件、消息、调度**：文件存储新线（K/P）；基准类型、PG 方言、数据连接、数据接口契约、引用登记、通知内核 + 渠道 Provider、调度器 + 回调契约、实时推送（D/S）；审计列 + 序号服务、数据规则策略、服务端 Hook、Filter AST（F）；可发布元数据框架、导入导出框架、控件注册表契约、i18n（D/F/S）。
- **应用、导航、组织、前端**：应用实体、菜单与资源模型（F）；URN、偏好 KV、深链协议、多端同契约、组织树/岗位/用户档案、字典（S）；责任岗位链查询、表达式规范、消息模板注册（W）；令牌、`@yue/core`、AppShell、DataTable、SchemaForm、Upload、业务选择器、认证页族、壳插槽注册表、元数据契约草案、工程化基线（U）；MF2（P）。
- **Agent 防返工**：带 Schema 的写命令、结构化错误、`inputSchema` 禁 tenant、事件契约化、能力目录 ≠ 公开 OpenAPI、副作用分级（P）。

### 3.2 S2 必做清单

- 元数据注册表、Dynamic Data Engine（Records API、参数化、唯一入口三段式鉴权）、Field Type Catalog、Schema Migrator、泛型 CRUD、信封事件、元数据校验器（K/F/P）。
- 表单/列表设计器与 S2 控件集、发布向导、权限资源同步、数据权限方案自动生成、表达式引擎（F/U）；审批引擎最小集（W）；工作流即能力（P）。
- 数据权限编译器、字段权限、授权界面、创建者自动 Owner（S/K/P）；责任人解析、导入向导、应用菜单多端（S）。
- 数据建模、接口设计器、短信/机器人通道、监控与任务管理 UI、打印全套、文件预览、模型目录与路由（D）。
- `package`、`function` Wasm、逐租户批量迁移、升级同意规则、`template/managed`、Operation/Task、元数据生成闭环、字段敏感度、SSRF 基线、WebSocket 背板（P）。
- DataTable 进阶、ImportWizard、SuperQuery、ConditionBuilder、CodeEditor、控件注册表交付、渲染器与子表、三层联动、流程表单宿主、三大设计器、校验器共用、manifest 全量（U）。

### 3.3 S3 必做清单

- 自助开通、DatabasePerTenant、完整租户状态机、套餐、多管理员与邀请、租户 IdP/HRD、国内三家 + ISV、目录同步 + SCIM、自助注销、Passkey、安装即建 client、OpenFGA、面向客户工作台（K/S）。
- 私有目录市场、安装向导、升级中心、审核台、ISV 入驻与席位、Plan/Seat/Meter/Trial、三方合并冲突预检、工具描述哈希钉死、授权服务器对齐 MCP、成本配额、计量计费、动态路由、接线生成器（P/K）；app-bundle 安装（F）。
- 门户设计型全套、表格报表、开放 API、分享链接、身份源同步（D）；不可信 UI 沙箱、通知抽屉、门户设计器与运行时（U/P）。W 基线的 S3 项（归档、签收、委托、撤销、SLA 自动处置、决策、自由流）未分必做/可选。

### 3.4 待拍板问题汇总（去重编号）

| # | 问题 | 来源 | 基线倾向 |
|---|---|---|---|
| Q1 | 身份路线：OpenIddict 自建唯一 issuer 还是 Keycloak / Logto；前提：私有化交付、SAML/CAS/LDAP 首期必要性、自定义登录域名、等保/商密 | K/S/P | 混合 C2，Authentik 退居运维；SAML 等先代理 IdP；自定义域只换登录页宿主 |
| Q2 | 账号归属：全局自然人 + 租户成员，还是允许租户托管身份 | S | Person + Membership 为主，托管身份可选 |
| Q3 | 与 iClaw 共用边界：IdP、S2S 签发与 claim、Usage Ledger、Higress；租户主数据权威 | K/P/S | claim 对齐 iClaw；Qian 为业务主数据权威；复用 Higress |
| Q4 | 默认隔离 Shared + RLS 还是 SchemaPerTenant；PG 唯一一等库；独立库是否付费选项；是否承诺迁移 | K/P/S/F | Shared + RLS 默认，PG 唯一，独立库作 S3 高隔离档，不做在线迁移 |
| Q5 | 配置中心：Apollo 与 ADR 修订；MySQL RDS 还是 operator；租户级配置归属；授权与开关权威存储；namespace 建模 | K/P/S | Apollo 为权威，Nacos 收窄；租户级配置不进 Apollo；授权在租户库、开关在 Apollo |
| Q6 | Kun 消费：NuGet + CalVer 还是 monorepo；上游 PR 还是补丁包；维护 SLA | K | NuGet + CalVer，上游 PR 优先 |
| Q7 | 插件边界：第三方 `service` 上架；宿主是否全 AOT；专属部署是否售卖；`package` 默认模式；`function` ABI；MCP 注册归属 | K/P | 第三方代码不进共享进程；AOT 只留边缘；两种分发都支持；Qian 注册表为真相源；ABI S2 前拍板 |
| Q8 | 生产集群与 iClaw 共用还是独立；隔离三档客户比例 | K | 独立集群或独立节点池 |
| Q9 | 模型网关并入 MaaS 还是另起；GPUStack 范围；GPU 归属 | K | 复用 MaaS，不另写模型代理 |
| Q10 | 高级权限模式是否有客户在 S2 需要 | S | S3 可选；S1 预留 `profile` claim |
| Q11 | 等保/商密要求；MFA 是否 S1 必做 | S | TOTP S1 可选 |
| Q12 | 配额口径：管理员是否计入；超管是否按租户可见 | S | 不计平台超管、计租户管理员 |
| Q13 | 参考平台 SaaS 租户服务未交付，自助申请审核、邀请格式需自行设计 | S | — |
| Q14 | 代码生成：不做 / 薄壳脚手架 / 完整 CRUD | F/P | 薄壳，不早于 S2 后期 |
| Q15 | 低代码存储模型：物理表 / 宽表 JSON / 混合 | F | 混合 |
| Q16 | 移动端：S2 仅 PC 还是加 H5；S3 是否纳入小程序/App 壳；多端配置策略 | F/U/D | S2 仅 PC + 端覆盖预留；H5/PWA 为主；小程序原生不纳入 |
| Q17 | 视图归 S2 还是 S3 | F | S2 可选 |
| Q18 | 脚本边界：仅声明式 + 服务端 Hook / 加前端沙箱 / 加服务端受控脚本 | F/W/U | S2 声明式；三层策略 QuickJS 兜底；自由 JS 按钮不做 |
| Q19 | 数据权限合并语义沿用还是可配置 | F | 沿用「同角色 AND / 跨角色 OR / 全部放开」 |
| Q20 | 功能编码命名空间化；同一功能可否挂多应用菜单 | F | 命名空间化稳定编码 + id 重映射表 |
| Q21 | 引擎路线：自研 .NET + Elsa PoC + Flowable 适配器；BPMN 导出 | W/U | 接受裁决；BPMN 仅作导出器 |
| Q22 | 一套 DSL 两种视图；S2 只交付瀑布视图；DOM 节点树还是 React Flow | W/U | 一套 JSON；瀑布必做、自由画布可选；不用 bpmn-js |
| Q23 | S2 流程最小集边界：加签、候选、办理节点、子流程、时效是否进必做 | W | 均列可选，加签与催办需确认 |
| Q24 | 拒绝后继续流转保留 S3 还是不做 | W | S3，S2 预留枚举 |
| Q25 | 兜底人模型：租户管理员角色 vs 指定异常处理人 | W | 不再兜底到全局 admin |
| Q26 | Agent 代办身份：用户身份还是独立主体；哪些动作禁止 | W/P/S | 委托模型区分本人/Agent/系统；签名 humanOnly；权限 = 用户权限 ∩ scope |
| Q27 | 流程「启用/停用」替代上架下架；流程模板是否作市场商品 | W | 改名避免撞名 |
| Q28 | automation 是否提前 S2 必做；decision 阶段 | W/D | automation S2 可选；decision S3 可选 |
| Q29 | 门户与报表阶段：任务书 S3 vs 笔记建议更早 | D | 按任务书；配置路径型门户与报表子集 S2 可选 |
| Q30 | 报表深度：复杂报表 vs 列表 + 分组汇总 + XLSX | D | 先子集，再渐进 |
| Q31 | 大屏是否列入范围 | D | 延后，不复用参考平台大屏代码 |
| Q32 | 数据接口 SQL 文本模式是否开放 | D | 高级权限 + 只读账号 + 限额 |
| Q33 | 服务端 PDF 渲染 S2 还是先浏览器打印 | D | S2 浏览器，S3 可选服务端 |
| Q34 | 预览服务技术栈：Java sidecar 还是 .NET/文档服务器 | D | 提供者接口可替换 |
| Q35 | 开放接口凭据：废弃用户级 AppSecret，统一网关应用级客户端凭据 | D | 是 |
| Q36 | 组件底座：路线 A 的 S2 风险；控制台域是否临时 antd v6 | U | 路线 A + S1 验证门，不混用 |
| Q37 | 后台壳多面策略：三面新窗口 vs 一个壳按上下文切换 | U | — |
| Q38 | 布局模式与偏好系统范围 | U | S1 单一侧栏，S3 可选 |
| Q39 | 富文本/Markdown 编辑器选型 | U | 需补调研 |
| Q40 | iClaw 代码复用授权 | U/K | 只借鉴思路或由所有方确认 |
| Q41 | yue 分发渠道：shadcn 私有 registry + MCP Server | U | S3 可选 / S4 必做 |

### 3.5 基线之间的矛盾与未对齐

| 议题 | 分歧 |
|---|---|
| IdP 形态 | K、S 已裁决 OpenIddict 自建唯一 issuer；P §6 仍写成「RP 侧 module + 托管 IdP 待拍板」，需按 K/S 统一 |
| 企微/钉钉/飞书连接器 | K 列 S1 必做；S 裁决 S1 可选；P 列 S2 可选 / S3 必做 |
| refresh 令牌与单点登出 | K/S 列 S1 必做；P 列 S2 可选 |
| 审计路径 | K 以 `Kun.Auditing` 同事务 Outbox 为准；P 称「弃用 `Kun.Auditing`」改 `AuditLog` + iClaw 式拦截器 |
| Wasm `function` | K 列 S2 可选；P 列 S2 必做 |
| 打印 | D 列 S2 必做；U 把打印设计器列 S3 可选 |
| 工作台 | S 裁决 S3 必做；U 列 S3 可选 |
| 短信渠道 | D 列 S1 必做（验证码/找回密码）；S 列 S1 可选 |
| 公告 | D 列 S2 可选；S 列 S3 可选 |
| DTCG 令牌 | P 列 S1 必做；U 列导出 S1 可选 |
| MF2 运行时 | K/P 列 S1 必做；U 的分层与选型未提 Module Federation |

### 3.6 最难的问题汇总

1. **身份核心与鉴权引擎同时自建**（K/S/P）：「很大」量级且互为前置，安全责任全在自己 → 显式列安全预算；端口在内核、实现为 system 插件。
2. **Kun 关键路径缺口**（K/P）：丢消息、at-most-once、丢租户、无锁在多副本多租户下必然触发 → S1 必修并用集成测试实证。
3. **AOT 与动态性**（K/P）：严格 AOT 不能热插，低代码靠运行时元数据 → 非 AOT 孤岛、进程外插件、AOT 只留边缘。
4. **逐租户迁移与三方合并**（P/K/F）：数千租户 DDL、半升级态、租户定制与新版本冲突 → 批次/并发/人工介入、冲突预检。
5. **权限服务三入口与动态表**（S/F/P）：UI/API/MCP 一致，行级谓词作用于动态表并叠加 RLS → 内核 PEP + 引擎唯一入口三段式。
6. **元数据 Schema 与迁移管道**（F/W/D）：参考平台 77 个 JSON 列无 Schema、改表 = 删表重建 → 版本化 Schema + 校验器共用；diff → 计划 → 审核 → 执行。
7. **动态引擎 SQL 安全**（F/D）：拼 SQL、租户字面量、任意 SQL/URL 代理 → 参数化 + 标识符白名单 + 限额。
8. **审批逆向语义交叉**（W）：退回/撤回/撤销/拒绝续流与子流程、加签、计时器互相影响 → 回归集 + 属性测试，只追加流转记录。
9. **进程内状态外置**（D/S/W）：WebSocket 池、延时队列、缓存锁、调度无集群 → Redis/消息总线背板、平台调度、乐观并发。
10. **Wasm 宿主与脚本沙箱**（P/F/U）：.NET 不支持组件模型、eval 不可用 → Extism vs WIT 早拍板、三层联动策略。
11. **自建数据密集组件**（U）：DataTable/Tree/Upload 的 a11y 与 i18n 质量 → S1 最小集先行、Base UI 验证门、备选 antd 限域。
12. **Kun 维护带宽与 iClaw 设计态资产**（K/P）：单人维护，想复用的注册中心/市场尚未实现 → 明确通道与 SLA、按同一契约先行。

---

## 4. 附录：基线文件与章节目录

| 领域 | 文件路径 |
|---|---|
| K | `/Users/dawn/project/tmp/qian-refs/research/baselines/k-kun-capability-gap.md` |
| P | `/Users/dawn/project/tmp/qian-refs/research/baselines/p-plugin-agent-architecture.md` |
| S | `/Users/dawn/project/tmp/qian-refs/research/baselines/s-system-tenant-identity.md` |
| F | `/Users/dawn/project/tmp/qian-refs/research/baselines/f-online-dev-form-list-app.md` |
| W | `/Users/dawn/project/tmp/qian-refs/research/baselines/w-workflow.md` |
| D | `/Users/dawn/project/tmp/qian-refs/research/baselines/d-data-integration-report-portal.md` |
| U | `/Users/dawn/project/tmp/qian-refs/research/baselines/u-frontend-yue.md` |

**K**：0 导读 · 1 Kun 能力矩阵 · 2 六项硬性要求逐条评估 · 3 低代码特有需求与 Kun 设计取向的张力及对策 · 4 既有资产复用清单 · 5 「Kun 不能满足时可以重新构建」的判断 · 6 基础设施与部署基线 · 7 风险与未决问题 · 附录 A 能力分级与插件归属总表 · 附录 B 笔记间矛盾与裁决 · 附录 C 输入笔记索引

**P**：0 结论先行 · 1 目标与原则 · 2 插件模型候选与推荐 · 3 插件清单字段草案 · 4 生命周期与治理 · 5 内核最小集 · 6 首批插件清单 · 7 从插件到应用市场 · 8 Agent 就绪设计 · 9 前端插件化与 yue 的关系 · 10 风险与未决问题 · 附录 A 矛盾与裁决 · 附录 B 分级总表 · 附录 C 阅读说明与未核实项

**S**：1 领域全景 · 2 功能清单表· 3 关键业务规则与状态机 · 4 核心数据对象 · 5 与其它领域的依赖 · 6 在 Kun 上实现的差距与难点 · 7 值得沿用的交互与界面模式 · 8 插件化与 Agent 可用性 · 9 专题小节 · 10 矛盾与裁决 · 11 未决问题

**F**：0 阅读说明 · 1 领域全景 · 2 功能清单表· 3 关键业务规则与状态机 · 4 核心数据对象 · 5 与其它领域的依赖 · 6 差距与难点 · 7 值得沿用的交互模式 · 8 插件化与 Agent 可用性 · 9 未决问题 · 附录 A 控件全集分级表 · 附录 B 元数据模型建议轮廓 · 附录 C 矛盾与裁决 · 附录 D 出处索引

**W**：0 阅读指南与裁决总则 · 1 领域全景 · 2 功能清单与分级 · 3 关键业务规则与状态机 · 4 核心数据对象 · 5 与其它领域的依赖 · 6 差距与难点 · 7 值得沿用的交互模式 · 8 插件化与 Agent 可用性 · 9 未决问题 · 附：术语对照

**D**：0 阅读指南 · 1 领域全景 · 2 功能清单表· 3 关键业务规则与状态机 · 4 核心数据对象 · 5 与其它领域的依赖 · 6 差距与难点 · 7 值得沿用的交互模式 · 8 插件化与 Agent 可用性 · 9 未决问题 · 附录 A 自研/集成开源/延后与许可证风险 · 附录 B AI 中心清单

**U**：0 结论速览 · 1 参考平台前端的组件与设计器全集 · 2 我方既有前端资产盘点与可继承程度 · 3 yue 组件库分层建议 · 4 技术选型候选与推荐 · 5 工程化基线 · 6 移动端策略的输入 · 7 复杂度热点、风险与未决问题 · 附：出处索引
