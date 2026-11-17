> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AuditLog

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Canonical audit-log record emitted by the framework on business-relevant
state changes (aggregate create / update / delete, explicit
[AuditableAttribute](/sdk/api/kun/kun-audit-abstractions/auditableattribute.md) invocations, login events, …).

```csharp
public sealed class AuditLog : IMultiTenant
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditLog](/sdk/api/kun/kun-audit/auditlog.md)

#### Implements

IMultiTenant

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

[AuditLog](/sdk/api/kun/kun-audit/auditlog.md) is a persistence-agnostic POCO. ORM mapping (table
name, column types, indexes, immutability triggers) lives in
`Kun.Audit.EntityFrameworkCore/AuditLogConfiguration.cs` — no EF /
JSON attributes are placed here so the type stays Core-layer friendly.

Implements [IMultiTenant](/sdk/api/kun/kun-core-domain/imultitenant.md) so that when consumers store audit
rows inside a `KunDbContext`-derived context, the automatic tenant
global query filter isolates tenant A's audits from tenant B's without
extra configuration. Consumers that run audits on a dedicated database
can ignore this interface.

## Constructors

### AuditLog() \{#Kun_Audit_AuditLog__ctor}

```csharp
public AuditLog()
```

## Properties

### Action \{#Kun_Audit_AuditLog_Action}

Stable verb identifying what happened, e.g. `agent.created`,
`artifact.uploaded`, `user.login.succeeded`. Business code
supplies this through [AuditableAttribute](/sdk/api/kun/kun-audit-abstractions/auditableattribute.md) or, for
interceptor-driven audits, it is derived from the EF
`EntityState` (`{resourceType}.created`,
`{resourceType}.updated`, `{resourceType}.deleted`).

```csharp
public string Action { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ActorId \{#Kun_Audit_AuditLog_ActorId}

Identity of the actor that caused the change, extracted from `ICurrentUser.UserId`; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for unattributed system actions.

```csharp
public string? ActorId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorType \{#Kun_Audit_AuditLog_ActorType}

Category of the actor — one of the [AuditActorType](/sdk/api/kun/kun-audit/auditactortype.md) constants.

```csharp
public string ActorType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Id \{#Kun_Audit_AuditLog_Id}

Surrogate primary key; deterministic GUID assigned by the writer.

```csharp
public Guid Id { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### IpAddress \{#Kun_Audit_AuditLog_IpAddress}

Remote client address, when an ambient HTTP context exposes one; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for background-worker originated audits.

```csharp
public string? IpAddress { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### PayloadJson \{#Kun_Audit_AuditLog_PayloadJson}

Optional JSON-encoded payload snapshot. Interceptor-driven audits
serialize the set of changed properties; attribute-driven audits may
pass the Command DTO. May be [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when
`KunAuditOptions.CaptureEntityPayload` is disabled or when the
change contains PII that the caller chose to redact.

```csharp
public string? PayloadJson { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceId \{#Kun_Audit_AuditLog_ResourceId}

Primary-key value of the affected resource, rendered as string for homogeneity across Guid / long / string keys; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the change isn't tied to a single row.

```csharp
public string? ResourceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceType \{#Kun_Audit_AuditLog_ResourceType}

CLR or business type-name of the affected resource (typically the aggregate type name, e.g. `Agent`).

```csharp
public string ResourceType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TenantId \{#Kun_Audit_AuditLog_TenantId}

Tenant scope of the audited change; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for platform-level events (e.g. boot-time system actions).

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Timestamp \{#Kun_Audit_AuditLog_Timestamp}

UTC timestamp of the audit event.

```csharp
public DateTimeOffset Timestamp { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### TraceId \{#Kun_Audit_AuditLog_TraceId}

W3C trace id at the time of the change (extracted from [`Current`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.current)); enables cross-referencing audits with distributed traces.

```csharp
public string? TraceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
