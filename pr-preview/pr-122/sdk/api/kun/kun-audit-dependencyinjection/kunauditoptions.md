> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunAuditOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-audit-dependencyinjection/index.md)\
Assembly: Kun.Audit.dll

Configuration for `AddKunAudit`.

```csharp
public sealed class KunAuditOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunAuditOptions](/pr-preview/pr-122/sdk/api/kun/kun-audit-dependencyinjection/kunauditoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunAuditOptions() \{#Kun_Audit_DependencyInjection_KunAuditOptions__ctor}

```csharp
public KunAuditOptions()
```

## Properties

### AnonymousActorType \{#Kun_Audit_DependencyInjection_KunAuditOptions_AnonymousActorType}

Actor type used when `ICurrentUser.IsAuthenticated` is
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (background workers, migrations, scheduled
jobs). Defaults to [AuditActorType.System](/pr-preview/pr-122/sdk/api/kun/kun-audit/auditactortype.md#Kun_Audit_AuditActorType_System).

```csharp
public string AnonymousActorType { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CaptureEntityPayload \{#Kun_Audit_DependencyInjection_KunAuditOptions_CaptureEntityPayload}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), the EF Core interceptor
captures the set of changed property values as
[AuditLog.PayloadJson](/pr-preview/pr-122/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_PayloadJson). Disable in deployments where
the payload would routinely contain PII that the consumer has no
redaction strategy for.

```csharp
public bool CaptureEntityPayload { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### DefaultActorType \{#Kun_Audit_DependencyInjection_KunAuditOptions_DefaultActorType}

Actor type used when `ICurrentUser` is authenticated but the
claim set does not otherwise indicate a service-to-service call.
Defaults to [AuditActorType.User](/pr-preview/pr-122/sdk/api/kun/kun-audit/auditactortype.md#Kun_Audit_AuditActorType_User).

```csharp
public string DefaultActorType { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### SwallowWriterExceptions \{#Kun_Audit_DependencyInjection_KunAuditOptions_SwallowWriterExceptions}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), the interceptor swallows
writer exceptions and logs them at `Error` level instead of
letting them roll back the business transaction. Set to
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) for compliance-critical deployments where
no business change may commit without a successful audit write.

```csharp
public bool SwallowWriterExceptions { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
