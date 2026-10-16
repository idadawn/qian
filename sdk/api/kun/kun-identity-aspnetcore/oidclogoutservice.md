> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class OidcLogoutService

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Validates provider logout instructions before atomically revoking local sessions.

```csharp
public sealed class OidcLogoutService
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcLogoutService](/sdk/api/kun/kun-identity-aspnetcore/oidclogoutservice.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcLogoutService(IEnumerable\<IOidcClient>, ISessionRegistry) \{#Kun_Identity_AspNetCore_OidcLogoutService__ctor_System_Collections_Generic_IEnumerable_Kun_Identity_Abstractions_IOidcClient__Kun_Identity_Abstractions_Session_ISessionRegistry\_}

Validates provider logout instructions before atomically revoking local sessions.

```csharp
public OidcLogoutService(IEnumerable<IOidcClient> clients, ISessionRegistry sessions)
```

#### Parameters

`clients` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<IOidcClient>

`sessions` ISessionRegistry

## Methods

### HandleAsync(string, string, CancellationToken) \{#Kun_Identity_AspNetCore_OidcLogoutService_HandleAsync_System_String_System_String_System_Threading_CancellationToken\_}

Processes a signed logout token for the explicitly selected provider; issuer/sid scope and replay protection are preserved.

```csharp
public Task<int> HandleAsync(string clientName, string logoutToken, CancellationToken cancellationToken = default)
```

#### Parameters

`clientName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`logoutToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>
