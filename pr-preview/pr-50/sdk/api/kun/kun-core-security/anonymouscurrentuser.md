> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class AnonymousCurrentUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Security](/pr-preview/pr-50/sdk/api/kun/kun-core-security/index.md)\
Assembly: Kun.Core.dll

[ICurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/icurrentuser.md) implementation used when no authentication context is available
(for instance, before S2S JWT validation or in background workers with no caller identity).

```csharp
public sealed class AnonymousCurrentUser : ICurrentUser
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AnonymousCurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/anonymouscurrentuser.md)

#### Implements

[ICurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/icurrentuser.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Properties

### Instance \{#Kun_Core_Security_AnonymousCurrentUser_Instance}

Singleton instance — [AnonymousCurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/anonymouscurrentuser.md) has no state.

```csharp
public static AnonymousCurrentUser Instance { get; }
```

#### Property Value

[AnonymousCurrentUser](/pr-preview/pr-50/sdk/api/kun/kun-core-security/anonymouscurrentuser.md)

### IsAuthenticated \{#Kun_Core_Security_AnonymousCurrentUser_IsAuthenticated}

是否已认证。

```csharp
public bool IsAuthenticated { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### PrincipalKind \{#Kun_Core_Security_AnonymousCurrentUser_PrincipalKind}

已认证主体类型（user / service / agent）；旧适配器默认按用户处理，身份适配器应透传真实类型。

```csharp
public string PrincipalKind { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Roles \{#Kun_Core_Security_AnonymousCurrentUser_Roles}

用户当前持有的角色集合。

```csharp
public IReadOnlyList<string> Roles { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### UserId \{#Kun_Core_Security_AnonymousCurrentUser_UserId}

用户唯一标识（通常是 JWT 的 `sub` claim）。未登录时为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? UserId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
