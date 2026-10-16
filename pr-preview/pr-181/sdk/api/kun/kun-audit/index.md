> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.Audit

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Audit.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/index.md)

[Kun.Audit.Behaviors](/pr-preview/pr-181/sdk/api/kun/kun-audit-behaviors/index.md)

[Kun.Audit.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-audit-dependencyinjection/index.md)

[Kun.Audit.EntityFrameworkCore](/pr-preview/pr-181/sdk/api/kun/kun-audit-entityframeworkcore/index.md)

### Classes

[AuditActorType](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditactortype.md)

Well-known values for [AuditLog.ActorType](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_ActorType). These are
free-form strings by schema — the constants here are the vocabulary
the framework emits, but consumers may introduce additional actor
types (e.g. `"agent"`) without coordinating with Kun.

[AuditContentHasher](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditcontenthasher.md)

Canonicalises audit-entry fields and chains them with SHA-256:
`hash = sha256(prevHash || canonical(entryFields))`.
The output is lower-case hex so it is safe to embed in DB columns, URLs,
and Markdown tables without re-encoding.

[AuditEntry](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md)

Immutable record describing one audited command execution. Each entry is
part of a per-tenant hash chain: [AuditEntry.ContentHash](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash) covers this entry
plus the [AuditEntry.PrevHash](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_PrevHash) of the previous entry in the same chain,
so retroactive tampering with any row invalidates every following row.

[AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md)

Canonical audit-log record emitted by the framework on business-relevant
state changes (aggregate create / update / delete, explicit
[AuditableAttribute](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/auditableattribute.md) invocations, login events, …).

[AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)

Filter + pagination parameters for [IAuditReader.QueryAsync](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditreader.md#Kun_Audit_Abstractions_IAuditReader_QueryAsync_Kun_Audit_AuditQuery_System_Threading_CancellationToken_).
Filters combine with `AND`; unset filters match anything.

[EventBusAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md)

[IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that fans audit entries out as
`kun.audit.record.v1` integration events via
[IIntegrationEventBus](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/iintegrationeventbus.md), riding the consumer's Outbox for
atomicity with the business transaction.

[InMemoryAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/inmemoryauditchainstate.md)

Process-local [IAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md). Serialises read-compute-write
per tenant with a [`SemaphoreSlim`](https://learn.microsoft.com/dotnet/api/system.threading.semaphoreslim). Suitable for tests and
single-instance deployments; multi-instance deployments should back the
chain state with a durable store and DB-level uniqueness so two replicas
cannot fork the chain.

[InMemoryAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/inmemoryauditsink.md)

Process-local [IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md) that keeps all written entries in
memory. Intended for unit tests and non-durable development; do not use in
production because entries are lost on process restart and there is no
durability guarantee if the process crashes mid-write.

[NullAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/nullauditwriter.md)

[IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that silently drops every entry. Registered as
the fallback when no concrete writer has been configured so that
interceptor / pipeline wiring stays safe in tests and samples.

### Interfaces

[IAuditChainState](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditchainstate.md)

Maintains the rolling tip of the per-tenant audit hash chain. The interface
exposes a single atomic "acquire-previous, compute-new, commit-new" operation
so implementations are free to centralise the lock, the durable read, and the
write in one transaction.

[IAuditSink](/pr-preview/pr-181/sdk/api/kun/kun-audit/iauditsink.md)

Pluggable write target for audit entries. Implementations decide where
entries land (DB table, append-only log file, remote audit service) and
how durable the write is. The pipeline behaviour surrounds calls to this
interface with the per-tenant chain-state lock, so implementations may
treat writes as already serialised within a tenant.

### Enums

[AuditOutcome](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditoutcome.md)

Outcome of a command execution captured in an [AuditEntry](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md).
