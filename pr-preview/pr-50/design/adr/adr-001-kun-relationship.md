> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# ADR-001 Qian 与 Kun 的关系

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-001-kun-relationship.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-001-kun-relationship.md)，请修改源文件，不要改本页。

> 2026-10-07 经 [ADR-021](/pr-preview/pr-50/design/adr/adr-021-product-family-structure.md) 修订：补充 Kun / Qian / yue 三层口径、产品族的关联方式（组织、文档站、版本矩阵、看板）、`KunVersion` 单点钉版与 Kun 文档进文档站的规则；D1.1–D1.8 原文不变。

## 1 背景与问题（Context）

Kun 是团队自研的 .NET 10 微服务开发框架：32 个 `Kun.*` 模块约 2.1 万行 C#，MIT 许可，CalVer 发布到 NuGet，事实上单人维护，提交节奏自 2026 年 7 月起明显放缓（基线 K §1）。它在网关、零信任 S2S、租户隔离（EF 过滤 + PostgreSQL RLS + 三种策略）、Outbox、审计、存储、缓存、可观测这一层有 ADR、CI 门禁与 NuGet 包；但它**没有**租户目录、用户/组织/凭据存储、权限模型、配置中心接入、插件机制、元数据/动态实体与 K8s 部署物料（基线 K §0.1）。

调研确认 Kun 的真实成熟度低于其文档：可生产级只有 `Kun.Core` 与 `Kun.Mediator`；关键路径存在正确性缺口——领域事件到集成事件的路径会静默丢消息、RabbitMQ 消费端实际是 at-most-once、Outbox 发布器/消费端/Saga 执行器都不恢复租户上下文、Saga 与 Outbox 无多副本锁、网关默认不强制认证且限流未挂路由、gRPC 流式调用绕过鉴权、OIDC 客户端链路未打通（基线 K §0.1、§2.1–§2.4；基线 P §10.1 第 5 条）。另外存在多组「双胞胎 API」：三套 S2S 签发器、两族存储接口、多条审计路径、两个同名 `AddKunMultiTenancy`（基线 K §5.4）。Kun 文档与代码多处不一致（源生成器、`/s2s/issue`、NetworkPolicy 在代码里不存在），基线裁定**一律以代码为准**（基线 K §0 口径、附录 B-13）。

iClaw 以 `ProjectReference` 指向同级 `kun/` 目录并在 Docker 构建时复制源码，导致 Kun 破坏性变更直接打断构建、镜像构建依赖私有仓 PAT（基线 K §4.1）。Qian 需要在第一天就定下与 Kun 的关系，否则插件生态与第三方开发者无法依赖一个稳定的底座。

## 2 决策（Decision）

- **D1.1 定位**：Kun 是「微服务开发框架 / 底座 SDK」，不是平台内核。Qian 内核（插件注册表、生命周期、能力目录、租户目录、身份、权限、配置、审计装配）自成一层，Kun 模块是该层的实现依赖（v2 后此层 = `Qian.Runtime` + 平台插件 / `platform` profile base 组合包，Kun 的 `AddKunXxx()` 模块在 Qian 里被包装为插件，见 ADR-020 D20.1 / D20.10）。
- **D1.2 消费方式**：Qian 以 NuGet 包 + CalVer 锁版本消费 Kun；禁止把 Kun 源码复制进 Qian 仓库或以 `ProjectReference` 引用。
- **D1.3 对 Kun 的配套要求**：预发布 feed（`-beta.N`）、公共 API 兼容承诺、弃用窗口不少于 2 个小版本、完整变更日志。
- **D1.4 不推翻的核心设计**：四层依赖、`Result` 错误模型、显式注册的 Mediator、Outbox 表结构与稳定事件 ID、schema-per-service、RLS 双保险、PublicAPI 门禁，全部继承。
- **D1.5 改变三件事**：(a) 不把「AOT 兼容」写成平台承诺——业务服务全部 JIT，AOT 只留网关与 gRPC 边缘；(b) Kun 的安全默认值（RLS 拦截器、租户模型缓存键、强制认证、限流挂载）在 Qian 应用模板里改为**强制装配 + 构建期检查**，而非 opt-in；(c) 对每组双胞胎 API 选定唯一一套 canonical 实现并写进规范（签发器见 ADR-010，审计见 ADR-009，存储用新线 `Kun.Storage.Abstractions`，`ITenantContext` 用 AsyncLocal 版）。
- **D1.6 S1 必修的 Kun 缺口**（不修则多租户在后台链路泄漏）：① 多租户后台链路（消费端、Saga、Outbox 发布器回灌租户；两套 `ITenantContext` 收敛；claim 名统一为 `tenant_id`；生产禁用请求头兜底与 `default` 回退，fail-closed）；② 事件语义（丢消息修复、至少一次、多副本锁）；③ 网关加固（强制认证、路由级限流、私钥热轮换）与 gRPC 流式拦截器；④ 身份客户端（PKCE verifier、nonce、refresh、会话吊销）；⑤ 三套 S2S 签发器、两族存储接口、多条审计路径收敛，并对框架内写死的下游产品默认值「去产品化」。
- **D1.7 修复路径**：上游 PR 优先；每项修复设 1 个迭代的回合入期限，逾期则由 Qian 维护 `Kun.*.Patches` 补丁包族（MIT 允许），并记录回合入计划。
- **D1.8 规划口径**：凡 Kun 文档与代码不一致，以代码为准；文档声称但代码不存在的能力不得当作已有。

## 3 理由（Rationale）

- Kun 自己的 ADR 明确「不做 IdP、不做权限模型、不编排开户、不做反射式发现」，强行塞进 Kun 与其框架定位和单人维护带宽冲突（基线 K §5.2）。
- 第三方插件开发者只能依赖包，源码引用会把破坏性变更变成构建失败（基线 K §5.3）。
- 没有任何带 EF 的服务被 AOT 发布过，Kun 的 AOT 故事只覆盖网关、gRPC 无状态服务与纯 Mediator（基线 K §3.1 第 10 条）。
- iClaw 的教训：RLS 拦截器、`Kun.MultiTenancy`、`Kun.RateLimit`、`Kun.Observability` 都「有而未被用上」，在线服务靠约 216 处手写 `TenantId ==` 谓词（基线 K §4.1 痛点清单）。

## 4 备选方案与放弃原因（Alternatives）

- **monorepo / ProjectReference 引用 Kun**（iClaw 现状）：破坏性变更直接打断 Qian 构建，镜像构建依赖私有仓，第三方无法依赖源码。放弃。
- **长期 fork Kun**：分叉后双方修复不再互通，加重单人维护问题。只作为 D1.7 的逾期退路，并设回合入期限。
- **把 Kun 直接扩展为平台内核**：违背 Kun 的框架定位，插件、注册表、权限、元数据都不是框架职责。放弃。

## 5 后果与代价（Consequences）

- 正面：Qian 与 Kun 版本解耦；插件开发者有稳定的 SDK 依赖面；安全默认值不再依赖业务开发者自觉。
- 负面：S1 需要同时推进 Kun 修复与 Qian 内核建设；补丁包族一旦启用会产生双真相源，必须有回合入纪律；放弃平台级 AOT 承诺意味着业务服务镜像更大、冷启动更慢。
- 配套：建立 Qian → Kun 的需求通道与响应 SLA；为丢消息、租户回灌各补一个 Testcontainers 集成测试；在 Qian 仓库引入「在线 DbContext 必须继承多租户基类」等构建期检查。

## 6 对各阶段的影响

- **S1**：修复 D1.6 全部缺口；确定 canonical API 集；发布 Qian 应用模板。
- **S2**：元数据与动态数据层住进 Kun 允许的「非 AOT 孤岛」，复用租户上下文、RLS、Outbox（见 ADR-011）。
- **S3**：第三方 `service` 插件只依赖 Qian SDK 与 Kun NuGet 包，不接触源码。
- **S4**：能力目录与事件目录（Kun 唯一的机器可读契约产物）直接升格为 Agent 素材。

## 7 待确认事项

- Kun 的后续维护人与响应 SLA（决策清单 D1 需拍板项；Q7 已按「上游 PR 优先、1 迭代回合入期限」写入）。
- 2026-10-07 补充：仓库归属与包源统一（Qian 是否迁入与 Kun 同一组织）由 ADR-021 §7 / 任务 S1-DEC-009 承载。
- 「领域事件→集成事件丢消息」为代码路径推断、无测试覆盖，S1 需用最小集成测试实证后再决定走上游修复还是 Qian 侧编码规定（基线 K 附录 B-6）。
- 已裁决（R13）：S1 的租户开通编排由 `qian.kernel` 生命周期控制器（v2 后 `qian.kernel` 拆为 `Qian.Runtime` + 平台插件，生命周期控制器即 `platform` profile base 组合包里的 `qian.lifecycle`，见 ADR-020）以幂等步骤表承担，不用 Saga；Saga 的租户上下文回灌为 P0，其余修补（锁、Scoped 工厂、回写、重试）为 P1 可选（ADR-008 D8.11、PRD §07 7.1 / 7.3）。

## 8 参考

- 相关 ADR：ADR-003（内核边界）、ADR-008（事件）、ADR-009（审计）、ADR-010（网关）、ADR-011（动态数据层）。
- PRD：`PRD §02`（设计原则）、`PRD §07`（S1 技术底座功能规格）、`PRD §12`（能力边界）、`PRD §15`（演进路线）。
- 基线：K §0、§1、§2、§3.1、§4.1、§5.1–§5.4、附录 B；P §2.3、§10.1。
