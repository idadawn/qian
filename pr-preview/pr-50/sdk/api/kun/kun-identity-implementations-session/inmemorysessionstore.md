> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class InMemorySessionStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Implementations.Session](/pr-preview/pr-50/sdk/api/kun/kun-identity-implementations-session/index.md)\
Assembly: Kun.Identity.dll

Process-local session registry for tests/development; all index, renewal and revocation changes share a lock.

```csharp
public sealed class InMemorySessionStore : ISessionRegistry, ISessionStore
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InMemorySessionStore](/pr-preview/pr-50/sdk/api/kun/kun-identity-implementations-session/inmemorysessionstore.md)

#### Implements

[ISessionRegistry](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/isessionregistry.md),
[ISessionStore](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### InMemorySessionStore() \{#Kun_Identity_Implementations_Session_InMemorySessionStore__ctor}

Creates a registry using the system clock.

```csharp
public InMemorySessionStore()
```

### InMemorySessionStore(TimeProvider) \{#Kun_Identity_Implementations_Session_InMemorySessionStore__ctor_System_TimeProvider\_}

Creates a registry with an explicit clock.

```csharp
public InMemorySessionStore(TimeProvider timeProvider)
```

#### Parameters

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### ApplyLogoutAsync(OidcLogoutToken, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_ApplyLogoutAsync_Kun_Identity_Abstractions_OidcLogoutToken_System_Threading_CancellationToken\_}

Atomically consumes a validated logout token once and revokes matching issuer/subject/session entries.

```csharp
public Task<int> ApplyLogoutAsync(OidcLogoutToken logout, CancellationToken cancellationToken = default)
```

#### Parameters

`logout` [OidcLogoutToken](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### DeleteAsync(string, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_DeleteAsync_System_String_System_Threading_CancellationToken\_}

Removes the session. No-op when the session id is unknown.

```csharp
public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### GetAsync(string, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_GetAsync_System_String_System_Threading_CancellationToken\_}

Reads the session payload for `sessionId`; returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss or when the entry has expired.

```csharp
public Task<SessionData?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[SessionData](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?>

### RenewAsync(string, TimeSpan, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_RenewAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Extends an existing live session, returning false on expiration or revocation.

```csharp
public Task<bool> RenewAsync(string sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### RevokeUserAsync(string, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_RevokeUserAsync_System_String_System_Threading_CancellationToken\_}

Revokes every current application session for this user and returns the number removed.

```csharp
public Task<int> RevokeUserAsync(string userId, CancellationToken cancellationToken = default)
```

#### Parameters

`userId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### SetAsync(string, SessionData, TimeSpan, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_SetAsync_System_String_Kun_Identity_Abstractions_Session_SessionData_System_TimeSpan_System_Threading_CancellationToken\_}

Writes `data` under `sessionId`
with an absolute TTL of `ttl`. Overwrites any
existing entry atomically.

```csharp
public Task SetAsync(string sessionId, SessionData data, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`data` [SessionData](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### TryUseNonceAsync(string, string, TimeSpan, CancellationToken) \{#Kun_Identity_Implementations_Session_InMemorySessionStore_TryUseNonceAsync_System_String_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Consumes a nonce once within the supplied TTL. Used for browser transaction replay protection.

```csharp
public Task<bool> TryUseNonceAsync(string purpose, string nonce, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`purpose` [string](https://learn.microsoft.com/dotnet/api/system.string)

`nonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>
