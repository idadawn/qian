> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class AuditActorType

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-181/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Well-known values for [AuditLog.ActorType](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_ActorType). These are
free-form strings by schema — the constants here are the vocabulary
the framework emits, but consumers may introduce additional actor
types (e.g. `"agent"`) without coordinating with Kun.

```csharp
public static class AuditActorType
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditActorType](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditactortype.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Fields

### Agent \{#Kun_Audit_AuditActorType_Agent}

由 Agent 发起的操作。

```csharp
public const string Agent = "agent"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Service \{#Kun_Audit_AuditActorType_Service}

Action was attributed to an inter-service call authenticated via the internal S2S JWT but not tied to a human user.

```csharp
public const string Service = "service"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### System \{#Kun_Audit_AuditActorType_System}

Action originated inside the application process with no caller identity (boot-time migrations, scheduled jobs, …).

```csharp
public const string System = "system"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Unknown \{#Kun_Audit_AuditActorType_Unknown}

Attribution could not be determined — used when `ICurrentUser` reports unauthenticated and no system marker is available.

```csharp
public const string Unknown = "unknown"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### User \{#Kun_Audit_AuditActorType_User}

Action was attributed to a human end-user (resolved from `ICurrentUser.UserId`).

```csharp
public const string User = "user"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
