> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class OidcLoginService

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Completes the protected browser transaction before creating a local session.

```csharp
public sealed class OidcLoginService
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcLoginService](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/oidcloginservice.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcLoginService(ChallengeHelper, IEnumerable\<IOidcClient>, SignInHelper) \{#Kun_Identity_AspNetCore_OidcLoginService__ctor_Kun_Identity_AspNetCore_ChallengeHelper_System_Collections_Generic_IEnumerable_Kun_Identity_Abstractions_IOidcClient__Kun_Identity_AspNetCore_SignInHelper\_}

Completes the protected browser transaction before creating a local session.

```csharp
public OidcLoginService(ChallengeHelper challenges, IEnumerable<IOidcClient> clients, SignInHelper signIn)
```

#### Parameters

`challenges` [ChallengeHelper](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md)

`clients` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<IOidcClient>

`signIn` [SignInHelper](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/signinhelper.md)

## Methods

### CompleteAsync(HttpContext, string, string, CancellationToken) \{#Kun_Identity_AspNetCore_OidcLoginService_CompleteAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_System_Threading_CancellationToken\_}

Validates one-time state, exchanges with the original verifier/nonce/redirect URI, signs in, and returns a local redirect path.

```csharp
public Task<string> CompleteAsync(HttpContext context, string code, string state, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`state` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>
