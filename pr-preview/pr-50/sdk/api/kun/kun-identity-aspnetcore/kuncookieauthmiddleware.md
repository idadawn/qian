> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunCookieAuthMiddleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity.AspNetCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Middleware that hydrates `HttpContext.User` from a
[ISessionStore](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) entry keyed by the session cookie.
Requests without a valid session flow through anonymously; the
middleware never writes a 401 itself — downstream authorization
policies are responsible for that.

```csharp
public sealed class KunCookieAuthMiddleware
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCookieAuthMiddleware](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthmiddleware.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunCookieAuthMiddleware(RequestDelegate, ISessionStore, IOptions\<KunCookieAuthOptions>, TimeProvider, ILogger\<KunCookieAuthMiddleware>) \{#Kun_Identity_AspNetCore_KunCookieAuthMiddleware__ctor_Microsoft_AspNetCore_Http_RequestDelegate_Kun_Identity_Abstractions_Session_ISessionStore_Microsoft_Extensions_Options_IOptions_Kun_Identity_Configuration_KunCookieAuthOptions__System_TimeProvider_Microsoft_Extensions_Logging_ILogger_Kun_Identity_AspNetCore_KunCookieAuthMiddleware\_\_}

Creates the middleware.

```csharp
public KunCookieAuthMiddleware(RequestDelegate next, ISessionStore store, IOptions<KunCookieAuthOptions> options, TimeProvider timeProvider, ILogger<KunCookieAuthMiddleware> logger)
```

#### Parameters

`next` [RequestDelegate](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.requestdelegate)

`store` ISessionStore

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<KunCookieAuthOptions>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[KunCookieAuthMiddleware](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/kuncookieauthmiddleware.md)>

## Methods

### InvokeAsync(HttpContext) \{#Kun_Identity_AspNetCore_KunCookieAuthMiddleware_InvokeAsync_Microsoft_AspNetCore_Http_HttpContext\_}

Entry point called by the ASP.NET Core pipeline.

```csharp
public Task InvokeAsync(HttpContext context)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
