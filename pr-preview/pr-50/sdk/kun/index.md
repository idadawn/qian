> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Kun 框架

:::info 镜像页
本页由 kun 仓库的定位与模块矩阵拼装，来源 [`README.md`](https://github.com/idadawn/kun/blob/v2026.10.12-hotfix.3/README.md)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。改动请提到 kun 仓库。
:::

> 一款为 .NET 10 打造的现代化云原生微服务开发框架

Kun 是 Qian 的底座框架：Qian 平台基于 Kun 框架构建，前端使用 yue 组件库（[ADR-021](/pr-preview/pr-50/design/adr/adr-021-product-family-structure.md)）。本节按仓库 `Directory.Packages.props` 的 `KunVersion` 镜像 kun 仓库 **v2026.10.12-hotfix.3** 的文档，与 Qian 实际依赖的 Kun 是同一版本；Kun 的代码与文档改动去 [kun 仓库](https://github.com/idadawn/kun)提。

## 本节内容

- [与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)：Qian 自写的唯一一页——Kun 模块与平台插件、seam 的对应，canonical API，强制装配与禁用项，补丁包状态
- 上手：[Getting Started with Kun Framework](/pr-preview/pr-50/sdk/kun/getting-started.md)
- 模块地图：[模块地图](/pr-preview/pr-50/sdk/kun/modules.md)
- 指南：[Gateway 限流策略指南](/pr-preview/pr-50/sdk/kun/guides/gateway-rate-limit.md)、[Identity 客户端、浏览器会话与迁移](/pr-preview/pr-50/sdk/kun/guides/identity-client.md)、[Saga 编排指南（Orchestration）](/pr-preview/pr-50/sdk/kun/guides/orchestrating-sagas.md)、[Outbox + Integration Events 指南](/pr-preview/pr-50/sdk/kun/guides/outbox-and-integration-events.md)、[Read-Side Patterns: Writing Query Handlers](/pr-preview/pr-50/sdk/kun/guides/read-side-patterns.md)、[SaaS 多租户策略选型指南](/pr-preview/pr-50/sdk/kun/guides/saas-multitenancy.md)、[Command 校验指南（Kun.Validation）](/pr-preview/pr-50/sdk/kun/guides/validating-commands.md)、[Tracing Tour: Reading Distributed Traces in the Aspire Dashboard](/pr-preview/pr-50/sdk/kun/guides/tracing-tour.md)、[Schema-per-service with KunDbContext](/pr-preview/pr-50/sdk/kun/guides/schema-per-service.md)
- 安全：[审计：同事务实体记录与 PostgreSQL 命令哈希链](/pr-preview/pr-50/sdk/kun/security/audit-log.md)、[Multi-Tenancy: Row-Level Security as Defence in Depth](/pr-preview/pr-50/sdk/kun/security/multi-tenancy-rls.md)、[Service-to-Service JWT Authentication](/pr-preview/pr-50/sdk/kun/security/s2s-jwt.md)
- 设计说明与 ADR：[ADR-001 Do Not Introduce a New Kun.Identity Module](/pr-preview/pr-50/sdk/kun/architecture/adr-001-kun-identity-module-decision.md)、[ADR-003 Introduce Kun.Audit and Kun.Audit.EntityFrameworkCore Modules](/pr-preview/pr-50/sdk/kun/architecture/adr-003-kun-audit-module.md)、[ADR-004 Introduce Kun.Observability Module](/pr-preview/pr-50/sdk/kun/architecture/adr-004-kun-observability-module.md)、[ADR-005 Introduce Kun.Caching Module](/pr-preview/pr-50/sdk/kun/architecture/adr-005-kun-caching-module.md)、[ADR-006 Introduce Kun.RateLimit Module](/pr-preview/pr-50/sdk/kun/architecture/adr-006-kun-ratelimit-module.md)、[ADR-007 Introduce Kun.MultiTenancy and Kun.MultiTenancy.EntityFrameworkCore Modules](/pr-preview/pr-50/sdk/kun/architecture/adr-007-kun-multitenancy-module.md)、[ADR-008 Introduce Kun.Identity Module — Reversing ADR-001](/pr-preview/pr-50/sdk/kun/architecture/adr-008-kun-identity-module.md)、[ADR-009 Introduce Kun.Configuration Module](/pr-preview/pr-50/sdk/kun/architecture/adr-009-kun-configuration-module.md)、[ADR-010 Add Cookie Session + IAuthenticatedUser to Kun.Identity](/pr-preview/pr-50/sdk/kun/architecture/adr-010-kun-identity-cookie-session.md)、[ADR-011 Result<T> composition extensions (sync + async)](/pr-preview/pr-50/sdk/kun/architecture/adr-011-result-async-extensions.md)、[ADR-012 Introduce Kun.Testing module](/pr-preview/pr-50/sdk/kun/architecture/adr-012-kun-testing-module.md)、[ADR-013 Baggage-driven activity enricher for Kun.Observability](/pr-preview/pr-50/sdk/kun/architecture/adr-013-kun-observability-enricher.md)、[ADR-014 Result-returning validation pipeline behavior](/pr-preview/pr-50/sdk/kun/architecture/adr-014-validation-result-behavior.md)、[ADR-015 Outbox pattern design for Kun.EventBus](/pr-preview/pr-50/sdk/kun/architecture/adr-015-outbox-pattern.md)、[ADR-016 配置可靠性、权限端口与能力目录](/pr-preview/pr-50/sdk/kun/architecture/adr-016-platform-ports.md)、[ADR-S02 Introduce Kun.Sandbox Module — E2B-compatible sandbox abstraction for Kun consumers](/pr-preview/pr-50/sdk/kun/architecture/adr-s02-kun-sandbox-module.md)、[ADR P2-D SaaS 多租户 3-策略架构](/pr-preview/pr-50/sdk/kun/architecture/adr-p2d-multitenancy.md)、[When to use ICurrentUser vs IAuthenticatedUser](/pr-preview/pr-50/sdk/kun/architecture/when-to-use-currentuser.md)、[Why Kun Splits Writes from Reads](/pr-preview/pr-50/sdk/kun/architecture/why-cqrs.md)、[Why Kun Uses the Outbox Pattern](/pr-preview/pr-50/sdk/kun/architecture/why-outbox.md)、[Why Kun Uses Saga Orchestration](/pr-preview/pr-50/sdk/kun/architecture/why-saga.md)
- 白皮书：[Kun Framework 架构设计与产品白皮书](/pr-preview/pr-50/sdk/kun/whitepaper.md)
- [变更日志](/pr-preview/pr-50/sdk/kun/releases/index.md)；发布说明：[Kun Framework — v0.1.0-phase1](/pr-preview/pr-50/sdk/kun/releases/v0-1-0-phase1.md)
- [API 参考](/pr-preview/pr-50/sdk/api/kun/index.md)：DocFX 从 34 个 `Kun.*` 包生成

## 模块矩阵

Phase 1 顶层速览（完整矩阵请见 [`docs/architecture/module-map.md`](/pr-preview/pr-50/sdk/kun/modules.md)）：

| 模块                        | 职责                                                                                        |             是否 AOT             |
| :------------------------ | :---------------------------------------------------------------------------------------- | :----------------------------: |
| `Kun.Core`                | 领域抽象、CQRS 接口、Result、`ITenantContext`                                                      |                ✅               |
| `Kun.Mediator`            | `FrozenDictionary` 无反射调度、Pipeline Behaviors                                               |                ✅               |
| `Kun.Infrastructure`      | 仓储/UoW/规约接口、`IProcessedMessageStore`                                                      |                ✅               |
| `Kun.EntityFrameworkCore` | 写侧 EF Core 实现、Outbox 落库、租户过滤器                                                             |          ⚠️ trim-safe          |
| `Kun.Dapper`              | 读侧 Dapper AOT 封装                                                                          |          ⚠️ trim-safe          |
| `Kun.EventBus`            | 领域/集成事件抽象、Outbox 发布器                                                                      |                ✅               |
| `Kun.EventBus.RabbitMQ`   | RabbitMQ 传输、消费端幂等                                                                         |                ✅               |
| `Kun.Saga`                | Saga Orchestration 编排器                                                                    | ⚠️ trim-safe（Phase 2 拆 EF 子模块） |
| `Kun.Security`            | S2S JWT 签发/校验、租户上下文注入                                                                     |                ✅               |
| `Kun.Identity`            | 外部 OIDC 客户端、JWT 校验与原子会话                                                                   |                ✅               |
| `Kun.MultiTenancy`        | `ITenantContext` + EF 查询过滤器                                                               |                ✅               |
| `Kun.Audit`               | 双通道审计：writer/reader 抽象 + `EventBusAuditWriter` + SHA-256 哈希链 Mediator Pipeline Behavior   |                ✅               |
| `Kun.RateLimit`           | 按租户 / IP 分区限流                                                                             |                ✅               |
| `Kun.Caching`             | `IKunCache` + Memory / Redis 后端                                                           |                ✅               |
| `Kun.Storage`             | 7-method `IFileStorage` + Local / S3 + 活跃-MIME attachment 强制                              |                ✅               |
| `Kun.Observability`       | 统一 OpenTelemetry 装配：`AddKunObservability` 一键接入 Kun 全套 ActivitySource + HTTP/EF/Runtime 指令 |                ✅               |
| `Kun.Configuration`       | 强类型 Options + fail-fast 校验                                                                |                ✅               |
| `Kun.Sandbox`             | E2B v2 兼容 sandbox + Supervisor / E2B Cloud / InMemory 后端                                  |                ✅               |
| `Kun.Validation`          | SourceGen 验证器                                                                             |                ✅               |
| `Kun.AspNetCore`          | Minimal API 扩展、Pipeline Behaviors                                                         |          ⚠️ trim-safe          |
| `Kun.Grpc`                | gRPC 拦截器、Result→RpcException 映射                                                           |                ✅               |
| `Kun.Gateway`             | YARP 扩展、S2S 签发、限流                                                                         |                ✅               |
