> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunExceptionHandler

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.ExceptionHandling](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-exceptionhandling/index.md)\
Assembly: Kun.AspNetCore.dll

Global exception handler that translates Kun-specific exceptions into
RFC 7807 ProblemDetails responses.

- [ValidationException](/pr-preview/pr-202/sdk/api/kun/kun-validation/validationexception.md) → 400 Bad Request with validation errors.
- [ResultFailureException](/pr-preview/pr-202/sdk/api/kun/kun-core-results/resultfailureexception.md) → mapped via [ErrorToProblemDetailsExtensions.ToProblemDetails](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-mapping/errortoproblemdetailsextensions.md#Kun_AspNetCore_Mapping_ErrorToProblemDetailsExtensions_ToProblemDetails_Kun_Core_Results_Error_).
- [DomainException](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/domainexception.md) → 400 Bad Request with the domain error code.
- All other exceptions → rethrown for the default handler to produce 500.

```csharp
public sealed class KunExceptionHandler : IExceptionHandler
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunExceptionHandler](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-exceptionhandling/kunexceptionhandler.md)

#### Implements

[IExceptionHandler](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.diagnostics.iexceptionhandler)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunExceptionHandler(ILogger\<KunExceptionHandler>) \{#Kun_AspNetCore_ExceptionHandling_KunExceptionHandler__ctor_Microsoft_Extensions_Logging_ILogger_Kun_AspNetCore_ExceptionHandling_KunExceptionHandler\_\_}

Initializes the exception handler.

```csharp
public KunExceptionHandler(ILogger<KunExceptionHandler> logger)
```

#### Parameters

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[KunExceptionHandler](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-exceptionhandling/kunexceptionhandler.md)>

Logger for diagnostics.

## Methods

### TryHandleAsync(HttpContext, Exception, CancellationToken) \{#Kun_AspNetCore_ExceptionHandling_KunExceptionHandler_TryHandleAsync_Microsoft_AspNetCore_Http_HttpContext_System_Exception_System_Threading_CancellationToken\_}

```csharp
public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
```

#### Parameters

`httpContext` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`exception` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>
