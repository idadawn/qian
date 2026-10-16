> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class PropagateS2sTokenHandler

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Http](/sdk/api/kun/kun-security-http/index.md)\
Assembly: Kun.Security.dll

A [`DelegatingHandler`](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler) that copies the `Authorization` header
from the current incoming HTTP request to all outgoing requests made by the
[`HttpClient`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) it is attached to.
This propagates the S2S JWT transparently across service boundaries.

```csharp
public sealed class PropagateS2sTokenHandler : DelegatingHandler, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[HttpMessageHandler](https://learn.microsoft.com/dotnet/api/system.net.http.httpmessagehandler) ←
[DelegatingHandler](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler) ←
[PropagateS2sTokenHandler](/sdk/api/kun/kun-security-http/propagates2stokenhandler.md)

#### Implements

[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[DelegatingHandler.InnerHandler](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler.innerhandler),
[HttpMessageHandler.Dispose()](https://learn.microsoft.com/dotnet/api/system.net.http.httpmessagehandler.dispose#system-net-http-httpmessagehandler-dispose),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PropagateS2sTokenHandler(IHttpContextAccessor) \{#Kun_Security_Http_PropagateS2sTokenHandler__ctor_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Initializes the handler with an HTTP context accessor.

```csharp
public PropagateS2sTokenHandler(IHttpContextAccessor httpContextAccessor)
```

#### Parameters

`httpContextAccessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)

Accessor for the current HTTP context.

## Methods

### SendAsync(HttpRequestMessage, CancellationToken) \{#Kun_Security_Http_PropagateS2sTokenHandler_SendAsync_System_Net_Http_HttpRequestMessage_System_Threading_CancellationToken\_}

```csharp
protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
```

#### Parameters

`request` [HttpRequestMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[HttpResponseMessage](https://learn.microsoft.com/dotnet/api/system.net.http.httpresponsemessage)>
