> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class AuditContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing](/pr-preview/pr-202/sdk/api/kun/kun-auditing/index.md)\
Assembly: Kun.Auditing.dll

Request-scoped DTO that carries ambient audit context from the outer host
([AuditContextMiddleware](/pr-preview/pr-202/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md)) into the EF Core
[AuditingSaveChangesInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md).

```csharp
public sealed class AuditContext
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditContext](/pr-preview/pr-202/sdk/api/kun/kun-auditing/auditcontext.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

All fields are nullable and default to [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null); the middleware (or any
caller) populates them before the command handler opens its unit of work. The
interceptor does not fill in defaults — missing fields surface as [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
on the emitted [AuditEventIntegrationEvent](/pr-preview/pr-202/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md), which is the expected
behaviour for background jobs and other hosts without an HTTP context.

## Constructors

### AuditContext() \{#Kun_Auditing_AuditContext__ctor}

```csharp
public AuditContext()
```

## Properties

### ActorId \{#Kun_Auditing_AuditContext_ActorId}

Subject identifier of the acting principal (`sub` claim, user id, service id, …).

```csharp
public string? ActorId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorType \{#Kun_Auditing_AuditContext_ActorType}

Kind of actor performing the action. Defaults to `"system"` for background-job /
uninitialized contexts; typical values are `"user"`, `"agent"`, `"system"`.

```csharp
public string ActorType { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### IpAddress \{#Kun_Auditing_AuditContext_IpAddress}

Remote IP address (e.g. `HttpContext.Connection.RemoteIpAddress.ToString()`).

```csharp
public string? IpAddress { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TraceId \{#Kun_Auditing_AuditContext_TraceId}

W3C trace id (`Activity.Current?.TraceId.ToString()`), when an activity is active.

```csharp
public string? TraceId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserAgent \{#Kun_Auditing_AuditContext_UserAgent}

Client User-Agent header value, when present.

```csharp
public string? UserAgent { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
