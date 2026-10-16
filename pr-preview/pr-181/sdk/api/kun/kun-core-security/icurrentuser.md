> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ICurrentUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Security](/pr-preview/pr-181/sdk/api/kun/kun-core-security/index.md)\
Assembly: Kun.Core.dll

当前用户上下文，从 S2S JWT 的 `sub` / `roles` claim 中解析。

```csharp
public interface ICurrentUser
```

## Remarks

See `docs/architecture/when-to-use-currentuser.md` for when to
inject this interface vs `Kun.Identity.Abstractions.IAuthenticatedUser`
(the cookie / browser extension) and how to extend without introducing
a third sibling abstraction.

## Properties

### IsAuthenticated \{#Kun_Core_Security_ICurrentUser_IsAuthenticated}

是否已认证。

```csharp
bool IsAuthenticated { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### PrincipalKind \{#Kun_Core_Security_ICurrentUser_PrincipalKind}

已认证主体类型（user / service / agent）；旧适配器默认按用户处理，身份适配器应透传真实类型。

```csharp
string PrincipalKind { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Roles \{#Kun_Core_Security_ICurrentUser_Roles}

用户当前持有的角色集合。

```csharp
IReadOnlyList<string> Roles { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### UserId \{#Kun_Core_Security_ICurrentUser_UserId}

用户唯一标识（通常是 JWT 的 `sub` claim）。未登录时为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
string? UserId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
