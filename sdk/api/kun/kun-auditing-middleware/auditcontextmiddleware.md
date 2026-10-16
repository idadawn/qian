> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AuditContextMiddleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing.Middleware](/sdk/api/kun/kun-auditing-middleware/index.md)\
Assembly: Kun.Auditing.dll

ASP.NET Core middleware that populates the request-scoped [AuditContext](/sdk/api/kun/kun-auditing/auditcontext.md)
with data captured from the incoming [`HttpContext`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext): remote IP,
User-Agent header, and the current W3C trace id.

```csharp
public sealed class AuditContextMiddleware : IMiddleware
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditContextMiddleware](/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md)

#### Implements

[IMiddleware](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.imiddleware)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Registered via `services.AddKunAuditing()` as scoped, then mounted into the
pipeline with `app.UseMiddleware<AuditContextMiddleware>()` before any
command / unit-of-work that must emit audit events.

The middleware only captures transport-level metadata. Actor identity
([AuditContext.ActorId](/sdk/api/kun/kun-auditing/auditcontext.md#Kun_Auditing_AuditContext_ActorId) / [AuditContext.ActorType](/sdk/api/kun/kun-auditing/auditcontext.md#Kun_Auditing_AuditContext_ActorType)) is populated
downstream — e.g. by a `JwtCurrentUser` bridge — because those values depend on
the service-to-service auth pipeline which runs after request routing.

## Constructors

### AuditContextMiddleware(AuditContext) \{#Kun_Auditing_Middleware_AuditContextMiddleware__ctor_Kun_Auditing_AuditContext\_}

Creates a new [AuditContextMiddleware](/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md).

```csharp
public AuditContextMiddleware(AuditContext auditContext)
```

#### Parameters

`auditContext` [AuditContext](/sdk/api/kun/kun-auditing/auditcontext.md)

The request-scoped audit context to populate.

## Methods

### InvokeAsync(HttpContext, RequestDelegate) \{#Kun_Auditing_Middleware_AuditContextMiddleware_InvokeAsync_Microsoft_AspNetCore_Http_HttpContext_Microsoft_AspNetCore_Http_RequestDelegate\_}

```csharp
public Task InvokeAsync(HttpContext context, RequestDelegate next)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`next` [RequestDelegate](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.requestdelegate)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
