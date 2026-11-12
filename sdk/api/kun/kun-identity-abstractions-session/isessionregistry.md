> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface ISessionRegistry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions.Session](/sdk/api/kun/kun-identity-abstractions-session/index.md)\
Assembly: Kun.Identity.dll

Atomic session lifecycle operations. Renewing a revoked session must never recreate it.

```csharp
public interface ISessionRegistry : ISessionStore
```

#### Implements

[ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md)

## Methods

### ApplyLogoutAsync(OidcLogoutToken, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionRegistry_ApplyLogoutAsync_Kun_Identity_Abstractions_OidcLogoutToken_System_Threading_CancellationToken\_}

Atomically consumes a validated logout token once and revokes matching issuer/subject/session entries.

```csharp
Task<int> ApplyLogoutAsync(OidcLogoutToken logout, CancellationToken cancellationToken = default)
```

#### Parameters

`logout` [OidcLogoutToken](/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### RenewAsync(string, TimeSpan, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionRegistry_RenewAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Extends an existing live session, returning false on expiration or revocation.

```csharp
Task<bool> RenewAsync(string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### RevokeUserAsync(string, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionRegistry_RevokeUserAsync_System_String_System_Threading_CancellationToken\_}

Revokes every current application session for this user and returns the number removed.

```csharp
Task<int> RevokeUserAsync(string userId, CancellationToken cancellationToken = default)
```

#### Parameters

`userId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### TryUseNonceAsync(string, string, TimeSpan, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionRegistry_TryUseNonceAsync_System_String_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Consumes a nonce once within the supplied TTL. Used for browser transaction replay protection.

```csharp
Task<bool> TryUseNonceAsync(string purpose, string nonce, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`purpose` [string](https://learn.microsoft.com/dotnet/api/system.string)

`nonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>
