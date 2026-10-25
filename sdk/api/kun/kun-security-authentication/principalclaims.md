> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class PrincipalClaims

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Security)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Authentication](/sdk/api/kun/kun-security-authentication/index.md)\
Assembly: Kun.Security.dll

从已认证的主身份读取授权主体，不拼接不同身份的 claim。

```csharp
public static class PrincipalClaims
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PrincipalClaims](/sdk/api/kun/kun-security-authentication/principalclaims.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### TryRead(ClaimsPrincipal?, out string?, out string?, out string?) \{#Kun_Security_Authentication_PrincipalClaims_TryRead_System_Security_Claims_ClaimsPrincipal_System_String__System_String__System_String\_\_}

读取 sub/NameIdentifier、tenant_id 与 principal_kind；缺少类型的旧浏览器身份按 user，重复或冲突字段拒绝。

```csharp
public static bool TryRead(ClaimsPrincipal? principal, out string? subjectId, out string? tenantId, out string? principalKind)
```

#### Parameters

`principal` [ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?

`subjectId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`principalKind` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
