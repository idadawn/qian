---
title: 内核边界与 S1 服务拆分：内核只留契约、强制执行点与注册表；S1 共 5 个可部署服务 + 18 个系统插件
status: Accepted
date: 2026-10-06
decision_id: D3
supersedes: none
---

# ADR-003 内核边界与 S1 服务拆分

## 1 背景与问题（Context）

「内核 = 插件运行时 + 注册表 + 契约，其余皆插件」是插件思维的第一条可检验原则：内核代码里不出现业务名词，内核对外 API 可枚举且有 PublicAPI 清单（基线 P §1.1 P1）。基线 P §5 逐项评估了内核最小集与 Kun 现状：租户上下文两套实现互斥、无权限判定点、无插件注册表、能力目录只有事件 ID 注册表、审计四条路径并行、配置无任何 provider、Baggage 有键无写入点。

关于「权限引擎是内核还是插件」曾有分歧，裁决为：判定接口（PDP）与强制执行点（PEP）在内核，策略模型、存储、管理 API 与数据权限方案作为 `tier: system, alwaysEnabled` 的 `qian.authz` 插件实现——「契约在内核、实现可换」（基线 P 附录 A-4）。

部署现实是三节点 K3s、只跨两台物理宿主、几乎全钉首节点、无 ResourceQuota，每个独立 Pod 都是真实成本（基线 K §6.1；基线 P §10.1 第 8 条）。参考平台内核 11 万行加公共层反向依赖业务实体，是边界失守的反例（基线 P §1.3）。

## 2 决策（Decision）

- **D3.1 内核最小集**（只保留契约、强制执行点、注册表）：租户上下文与解析；主体身份与 S2S；权限判定点（PDP 接口 + Mediator Pipeline 形式的 PEP）；插件注册表、清单校验与两层生命周期控制器；扩展点 / 插槽注册表与契约仓库（`.proto` / OpenAPI 契约包，启用前契约一致性探测）；能力目录（命令 / 查询 / 事件 / 实体四类注册表，鉴权后按权限过滤）；事件总线（Outbox）；审计；配置绑定与配置源契约（`config.schema → namespace`）；可观测与计量。
- **D3.2 不在内核**：存储后端实现、IdP 适配、权限策略管理 UI、数据权限方案、市场、设计器、流程引擎、模型网关、连接器运行时、Wasm 运行时、沙箱运行时——全部是插件。新能力进内核必须走 ADR。
- **D3.3 内核与系统插件的区别**：能否被卸载、能否由第三方替换。系统插件（`tier: system`）不可被租户停用，但必须能独立出修订、逐租户迁移、回切。
- **D3.4 S1 可部署单元克制在 5 个**：`qian-gateway`（Kun.Gateway 加固，见 ADR-010）、`qian-identity`（OpenIddict，独立安全边界）、`qian-platform`（内核 + tenancy / authz / config / events / audit / files 等 `module` 型系统插件的 JIT 宿主）、`qian-model-gateway-adapter`（GPUStack ↔ Higress 控制面适配，可选）、`qian-shell`（前端外壳静态站）。
- **D3.5 S1 系统插件 18 个**：`qian.kernel`、`qian.plugin-sdk`、`qian.tenancy`、`qian.identity`（+ `qian.identity.connector.*`）、`qian.authz`、`qian.data`、`qian.files`、`qian.config`、`qian.gateway`、`qian.events`、`qian.audit`、`qian.observability`、`qian.shell`、`qian.entitlement`（授权 v0）、`qian.model-gateway`（P1，可选）、`qian.notify`（P0）、`qian.scheduler`（P0）、`qian.realtime`（P1）。组织目录最小集作为 `qian.identity` 的子模块交付（PRD §07 7.4.3），不独立成插件。除特别说明均为 `module` 形态编译期并入 `qian-platform`。
- **D3.6 预装 + 开关**：S1 全部官方 `module` 预装在宿主镜像内，按租户开关启用；运行时装载只对 `package / service` 成立（同 ADR-002 D2.10）。
- **D3.7 内核 canonical 选择**：`ITenantContext` 用 Kun.MultiTenancy 的 AsyncLocal 版 + 中间件，claim 名 `tenant_id`；存储用新线 `Kun.Storage.Abstractions/Local/S3/Oss`；服务侧限流用 `Kun.RateLimit`，网关侧用 Gateway 三策略；审计按 ADR-009。
- **D3.8 Kun 安全默认值写成内核强制项**：RLS 拦截器与 `KunMultiTenantModelCacheKeyFactory` 在内核 DbContext 装配里强制挂上；禁止业务代码 `IgnoreQueryFilters`；网关强制认证与路由级限流由 `qian.gateway` 装配保证。
- **D3.9 `qian.files` 范围**：基于新线 `IFileStorage` 封装租户前缀、配额、上传策略、文件元数据表；`ContentDispositionPolicy` 不可关闭；文件预览作为**可替换的预览提供者接口**（Q34）：首选 .NET 生态方案，Java 预览进程只作可选 sidecar，票据由内核签发并服务端校验。
- **D3.10 Operation/Task 资源** 为 S1 可选、S2 必做（状态机 `working / input_required / completed / failed / cancelled`）；S1 必须在 `qian.kernel` 的 SDK 契约包中预留 `qian.operations` 的资源契约，导入导出等长操作按该契约返回 Operation 资源（实现可到 S2）；流程待办与 S4 Agent 中断必须同模型。
- **D3.11 健康端点**：每个可部署服务提供 `/healthz`（liveness）与 `/readyz`（readiness，含 PG / Redis / RabbitMQ / Apollo 依赖检查）；Kun 的 `AddKunHealthChecks` 只是别名，`IHealthCheck` 实现由 `qian.kernel` 补。

## 3 理由（Rationale）

- 内核最小集闭合是插件生态不分裂的前提；多套 API 并存会让插件在不同实现之间分裂（基线 K §5.4）。
- 5 个可部署单元是三节点集群的现实折中：身份服务独立是安全边界要求（唯一 issuer 的密钥与会话不与业务进程同驻），网关独立是流量边界，其余 `module` 合并进一个宿主避免每插件一个 Deployment（基线 P §10.1 第 8 条）。
- 18 个系统插件对应 S1 退出标准所需的全部技术组件（基线 K 附录 A；基线 P §6）；`qian.notify` 与 `qian.scheduler` 由 ADR-012 的流程前置能力（可靠延时调度、消息模板注册）与「短信渠道 S1 必做」裁决带入，`qian.realtime` 是站内信与权限刷新的推送通道（无通道时降级轮询，故为 P1）。

## 4 备选方案与放弃原因（Alternatives）

- **每个系统插件一个 Pod**：三节点集群不可承受，且 `module` 形态本就是进程内。放弃。
- **单体宿主（含网关与身份）**：身份密钥与业务代码同进程，违反安全边界；网关无法独立伸缩。放弃。
- **权限引擎完全进内核**：策略模型与管理 UI 变化频繁，进内核会使内核膨胀且第三方（如 OpenFGA）无法替换实现。放弃，只保留 PDP/PEP。

## 5 后果与代价（Consequences）

- 正面：内核 API 面可枚举、可门禁；S1 部署拓扑简单，运维面小。
- 负面：`qian-platform` 是 JIT 宿主，任一 `module` 升级都要重建并发布该镜像；多个系统插件共进程意味着一个插件的故障可能影响同宿主其他插件，需要靠 ResourceQuota 与健康检查兜底。
- 配套：PublicAPI 清单与 `internal` 可见性治理；每个系统插件的 TCK 门禁；`qian-platform` 的模块化启动日志与按租户开关的可观测指标。

## 6 对各阶段的影响

- **S1**：交付 5 个服务 + 18 个系统插件；内核契约包 v1。
- **S2**：新增 `qian.metadata`、`qian.dynamic-data`、`qian.forms`、`qian.lists`、`qian.workflow`、`qian.operations` 等插件；新增 JIT 宿主 `qian-lowcode`（非 AOT 孤岛，承载元数据运行时）作为第 6 个可部署单元，另有 `qian-workflow`、`qian-file-preview` 与可选 `qian-wasm-runtime`（PRD §08 8.0.3）。
- **S3**：`qian.marketplace`、`qian.portal`、`qian.reports`、`qian.billing`；第三方 `service` 以独立工作负载身份运行。
- **S4**：`qian.agent-facade`（MCP 门面）、`qian.sandbox` 对接层；内核不新增 Agent 运行时。

## 7 待确认事项

- 「接受 S1 预装 + 开关模式」在决策清单中标为需拍板但未列入 Q1–Q18，本 ADR 按推荐写入。
- `qian-model-gateway-adapter` 是否进入 S1（决策清单 D18 需拍板项），本 ADR 标为可选。
- 已裁决（R1）：S2 新增 JIT 宿主 `qian-lowcode` 隔离元数据运行时（非 AOT 孤岛），另有 `qian-workflow`、`qian-file-preview` 与可选 `qian-wasm-runtime`；S1 仍为 5 个可部署单元（PRD §08 8.0.3）。

## 8 参考

- 相关 ADR：ADR-001、ADR-002、ADR-006、ADR-009、ADR-010、ADR-017、ADR-018。
- PRD：`PRD §03`、`PRD §07`、`PRD §12`。
- 基线：P §1.1、§5、§6、§10.1、附录 A-4；K §0.3、§2.5、§6.1、附录 A；D §9.1 第 7 条（文件预览）。
