> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Audit.Behaviors

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuditLogPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-audit-behaviors/auditlogpipelinebehavior-2.md)

Mediator pipeline behaviour that records one [AuditEntry](/sdk/api/kun/kun-audit/auditentry.md) per handler
invocation and appends it to the per-tenant hash chain. Chain-state mutation and
the [IAuditSink](/sdk/api/kun/kun-audit/iauditsink.md) write are serialised inside
[IAuditChainState.AppendAsync](/sdk/api/kun/kun-audit/iauditchainstate.md#Kun_Audit_IAuditChainState_AppendAsync_System_String_System_Func_System_String_System_Threading_Tasks_ValueTask_System_String___System_Threading_CancellationToken_) so concurrent commands on the same
tenant cannot fork the chain.
