> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# ADR-009 审计

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-009-audit.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-009-audit.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Kun 的审计能力分裂为多条并行路径：① 命令级 SHA-256 哈希链 `AuditEntry{PrevHash, ContentHash}`，但 `IAuditSink / IAuditChainState` **只有 InMemory 实现**、单实例；② `IAuditWriter / AuditLog` 通道（EF 表或 EventBus `kun.audit.record.v1`）+ `Kun.Audit.EntityFrameworkCore.AuditSaveChangesInterceptor`（SaveChanges 成功后写，**不同事务**）；③ `Kun.Auditing` 拦截器经 Outbox 同事务发 `kun.audit.event.v1`；`IAuditReader` 只有接口无实现；`AuditActorType` 无 `agent`；`TenantId / UserId` 在 Auditing 里写死 null（基线 K §1 Audit 行、附录 B-2/B-3）。基线 K 与 P 对「保留哪条路径」表述不同（K 以 `Kun.Auditing` 同事务 Outbox 为准；P 称弃用 `Kun.Auditing` 改用 `AuditLog` + iClaw 式拦截器），决策清单补充裁决「以语义定：实体级审计必须与业务同事务写入 `AuditLog` 并经 Outbox 发事件；命令级哈希链补 PostgreSQL 持久化；事后不同事务的拦截器弃用；具体落在哪个 Kun 包由 ADR 定」。

iClaw 的 `AuditSaveChangesInterceptor` 在 `SavingChangesAsync` 阶段写审计，使审计行与业务事务同提交、事件进 Outbox，已生产使用，且 Q40 允许搬运该代码（基线 K §4.1）。Agent 时代的硬要求：审计必须回答「哪个 Agent 以谁的名义、经哪个通道、调用了哪个能力的哪个契约版本」（基线 P §8.3 第 7 条）。参考平台审计统计全表加载、请求日志落库一键清空（基线 D §6 第 13 条；基线 S 明确不做项）。

## 2 决策（Decision）

- **D9.1 只保留一套审计实现**，由 `qian.audit`（`tier: system`）装配；其他路径标记废弃并在 TCK 中禁止引用。
- **D9.2 命令 / 能力级审计**：`AuditEntry` 哈希链，由 Mediator Pipeline 行为（`AddAuditBehavior`）写入；`IAuditChainState` 与 `IAuditSink` 提供 **PostgreSQL 实现**（可串行化读 tip + `content_hash UNIQUE`），支持多副本。
- **D9.3 实体级审计**：`AuditLog` 由**同事务**拦截器写入（在 `SavingChangesAsync` 阶段，吸收 iClaw 写法并迁移为 `qian.audit` 的拦截器），并经 Outbox 同事务发出审计事件。
- **D9.4 弃用** `Kun.Audit.EntityFrameworkCore.AuditSaveChangesInterceptor`（事后、不同事务）及其 `kun.audit.record.v1` 事件 ID。
- **D9.5 包归属**：以 `Kun.Auditing` 的同事务 Outbox 原语为基础扩展（而非另起一包），统一审计事件 ID 为一个（建议 `qian.audit.event.v1`，兼容期内保留 `kun.audit.event.v1` 别名）；`TenantId / UserId` 从 `ITenantContext / ICurrentUser` 填充而非写死 null。
- **D9.6 审计字段扩展**：`actor_type ∈ {user, service, agent, system}`；`on_behalf_of`（委托链）；`capability_id`；`contract_version`；输入哈希；来源通道 `channel ∈ {ui, api, mcp, a2a, job, event}`；`trace_id` 贯通；`generatedBy / promptHash` 预留给 S4。
- **D9.7 `IAuditReader`** 提供 EF 实现与只读查询 API；统计走聚合查询，不全表加载。
- **D9.8 必须入审计的事件**：权限判定（可解释结果）、插件生命周期操作、反向权限批准、租户状态迁移、配置发布（含 Apollo）、登录与会话事件、跨插件调用、Agent 调用、数据权限与字段权限变更。
- **D9.9 平台超管对租户不可见，但其操作在审计中可见**（Q12 补充裁决）。
- **D9.10 不做**：请求日志落库 + 一键清空；审计记录的物理删除（只允许按保留期归档）。

## 3 理由（Rationale）

- 哈希链只有内存实现意味着生产环境没有防篡改链；补 PostgreSQL 持久化是 S1 必做（基线 K 附录 B-2）。
- 同事务写入是「业务成功即有审计、业务失败即无审计」的唯一正确语义，事后拦截器在崩溃窗口会丢审计或留下孤儿记录。
- 把两份基线的分歧按「语义」而非「包名」裁决：K 与 P 都要求同事务 + Outbox，差异只在事件 ID 与拦截器实现来源，统一后不冲突。
- `actor_type=agent` 与委托链字段若不在 S1 定下，S4 每个服务都要后补「这是不是 Agent 调的」（基线 P §8.3 第 3/7 条）。

## 4 备选方案与放弃原因（Alternatives）

- **保留多条路径由插件自选**：插件生态在多套 API 间分裂，审计不完整。放弃。
- **只用外部日志系统（Loki）作审计**：无防篡改链、无同事务保证、不可按主体查询。只作备份与长期保留。
- **事后拦截器**（`Kun.Audit.EFC`）：不同事务，崩溃窗口丢审计。放弃。
- **审计全部走事件、不落本地表**：读侧依赖消费延迟，决策审计无法同步查询。放弃。

## 5 后果与代价（Consequences）

- 正面：一套 API、一个事件 ID；防篡改链可多副本运行；审计天然带主体链与来源通道，S4 无需返工。
- 负面：哈希链串行读 tip 是写入吞吐瓶颈，需按租户或按时间分链 / 分区；同事务写入增加业务事务体积；统一事件 ID 需要与 iClaw 协调兼容期。
- 配套：审计保留期与归档策略；审计查询权限点；TCK 用例「每个写命令必有审计条目」。

## 6 对各阶段的影响

- **S1**：D9.1–D9.9 全部落地；`AddAuditBehavior` 在 Qian 应用模板默认装配。
- **S2**：动态记录的数据日志复用实体级路径；流程流转记录只追加并带 actor 类型；元数据变更记录来源与审批人。
- **S3**：市场审核、授权变更、ISV 操作入链；审计导出给租户。
- **S4**：`channel=mcp/a2a`、`on_behalf_of`、`generatedBy / promptHash` 实际使用；审计闭环作为 Agent 安全防线之一。

## 7 待确认事项

- 哈希链分链 / 分区策略（按租户 / 按月）与吞吐基线需技术验证。
- 审计事件 ID 统一方案及与 iClaw 的兼容期。
- 审计数据保留期（法规与客户要求）。

## 8 参考

- 相关 ADR：ADR-001、ADR-003、ADR-006、ADR-008、ADR-016。
- PRD：`PRD §07`、`PRD §13`、`PRD §14`（质量与评估）。
- 基线：K §1 Audit 行、§4.1、§5.1 第 8 条、附录 B-2/B-3；P §5 审计行、§8.3 第 7 条、附录 A-7；D §6 第 13 条；S §2 F 系统管理、明确不做项。
