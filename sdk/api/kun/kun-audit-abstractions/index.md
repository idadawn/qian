> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Audit.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuditableAttribute](/sdk/api/kun/kun-audit-abstractions/auditableattribute.md)

Declarative marker for Command / Query / endpoint types that should be
audited regardless of whether their execution touched any tracked
aggregate. Interceptor-driven audits handle entity CRUD automatically;
this attribute covers the other category — business actions whose
significance isn't visible in the change-tracker (e.g. login,
download, export, permission grant).

### Interfaces

[IAuditReader](/sdk/api/kun/kun-audit-abstractions/iauditreader.md)

Queries the audit log store. Tenant-scoped by contract — see [AuditQuery.TenantId](/sdk/api/kun/kun-audit/auditquery.md#Kun_Audit_AuditQuery_TenantId).

[IAuditWriter](/sdk/api/kun/kun-audit-abstractions/iauditwriter.md)

Persists or publishes an [AuditLog](/sdk/api/kun/kun-audit/auditlog.md) entry.
