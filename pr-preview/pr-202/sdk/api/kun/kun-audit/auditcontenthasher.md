> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class AuditContentHasher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-202/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Canonicalises audit-entry fields and chains them with SHA-256:
`hash = sha256(prevHash || canonical(entryFields))`.
The output is lower-case hex so it is safe to embed in DB columns, URLs,
and Markdown tables without re-encoding.

```csharp
public static class AuditContentHasher
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditContentHasher](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditcontenthasher.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### Compute(AuditEntry) \{#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry\_}

计算包含主体类型的哈希；历史 Unknown 条目兼容既有算法。

```csharp
public static string Compute(AuditEntry entry)
```

#### Parameters

`entry` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Compute(Guid, string?, string?, string, DateTimeOffset, long, AuditOutcome, string?, string?) \{#Kun_Audit_AuditContentHasher_Compute_System_Guid_System_String_System_String_System_String_System_DateTimeOffset_System_Int64_Kun_Audit_AuditOutcome_System_String_System_String\_}

Computes the chain hash for one audit entry.

```csharp
public static string Compute(Guid id, string? tenantId, string? actorId, string commandName, DateTimeOffset occurredAt, long latencyMs, AuditOutcome outcome, string? errorCode, string? prevHash)
```

#### Parameters

`id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

Entry id.

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant id, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`actorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Actor id, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`commandName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Command type short name. Must be non-empty.

`occurredAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp (formatted with `"O"` round-trip format for determinism).

`latencyMs` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Wall-clock latency in milliseconds.

`outcome` [AuditOutcome](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md)

Success / Exception.

`errorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Exception type name on failure, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`prevHash` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Previous entry's [AuditEntry.ContentHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash), or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the first entry.

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

Lower-case hex SHA-256.
