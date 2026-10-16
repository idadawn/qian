> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Enum AuditOutcome

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Outcome of a command execution captured in an [AuditEntry](/sdk/api/kun/kun-audit/auditentry.md).

```csharp
public enum AuditOutcome
```

## Fields

`Exception = 1`

The handler threw an exception.

`Success = 0`

The handler returned normally (non-exception path).
