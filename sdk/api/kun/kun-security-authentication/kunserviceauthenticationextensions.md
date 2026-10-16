> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunServiceAuthenticationExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Authentication](/sdk/api/kun/kun-security-authentication/index.md)\
Assembly: Kun.Security.dll

Extension methods for registering Kun S2S JWT Bearer authentication on downstream services.

```csharp
public static class KunServiceAuthenticationExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunServiceAuthenticationExtensions](/sdk/api/kun/kun-security-authentication/kunserviceauthenticationextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunServiceAuthentication(AuthenticationBuilder, Action\<KunServiceAuthenticationOptions>) \{#Kun_Security_Authentication_KunServiceAuthenticationExtensions_AddKunServiceAuthentication_Microsoft_AspNetCore_Authentication_AuthenticationBuilder_System_Action_Kun_Security_Authentication_KunServiceAuthenticationOptions\_\_}

Adds Kun S2S JWT Bearer authentication. Rejects HS256 tokens — only asymmetric
algorithms (RS256, ES256) are accepted, consistent with the zero-trust policy.

```csharp
public static AuthenticationBuilder AddKunServiceAuthentication(this AuthenticationBuilder builder, Action<KunServiceAuthenticationOptions> configure)
```

#### Parameters

`builder` [AuthenticationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.authenticationbuilder)

The authentication builder.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunServiceAuthenticationOptions](/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md)>

Delegate to configure [KunServiceAuthenticationOptions](/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md).

#### Returns

[AuthenticationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.authentication.authenticationbuilder)

The authentication builder for chaining.
