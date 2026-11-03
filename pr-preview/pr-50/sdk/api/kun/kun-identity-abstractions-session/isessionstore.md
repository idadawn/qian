> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface ISessionStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions.Session](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/index.md)\
Assembly: Kun.Identity.dll

Backend for cookie-based session data. Implementations ship in
`Kun.Identity.Implementations.Session` (in-memory for tests /
single-process dev; Redis-backed via `Kun.Caching` for
production). Implementations must be safe for concurrent use from
multiple request threads.

```csharp
public interface ISessionStore
```

## Methods

### DeleteAsync(string, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionStore_DeleteAsync_System_String_System_Threading_CancellationToken\_}

Removes the session. No-op when the session id is unknown.

```csharp
Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### GetAsync(string, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionStore_GetAsync_System_String_System_Threading_CancellationToken\_}

Reads the session payload for `sessionId`; returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss or when the entry has expired.

```csharp
Task<SessionData?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[SessionData](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?>

### SetAsync(string, SessionData, TimeSpan, CancellationToken) \{#Kun_Identity_Abstractions_Session_ISessionStore_SetAsync_System_String_Kun_Identity_Abstractions_Session_SessionData_System_TimeSpan_System_Threading_CancellationToken\_}

Writes `data` under `sessionId`
with an absolute TTL of `ttl`. Overwrites any
existing entry atomically.

```csharp
Task SetAsync(string sessionId, SessionData data, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`sessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`data` [SessionData](/pr-preview/pr-50/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
