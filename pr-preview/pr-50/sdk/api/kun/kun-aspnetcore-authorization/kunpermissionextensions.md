> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunPermissionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.AspNetCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.AspNetCore.Authorization](/pr-preview/pr-50/sdk/api/kun/kun-aspnetcore-authorization/index.md)\
Assembly: Kun.AspNetCore.dll

将权限端口接入 ASP.NET Core 策略，不提供权限引擎。

```csharp
public static class KunPermissionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunPermissionExtensions](/pr-preview/pr-50/sdk/api/kun/kun-aspnetcore-authorization/kunpermissionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunAuthorization(IServiceCollection) \{#Kun_AspNetCore_Authorization_KunPermissionExtensions_AddKunAuthorization_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

注册策略处理器，宿主另行提供 IPermissionChecker；缺失时缺省拒绝。

```csharp
public static IServiceCollection AddKunAuthorization(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### RequireKunPermission\<TBuilder>(TBuilder, string) \{#Kun_AspNetCore_Authorization_KunPermissionExtensions_RequireKunPermission__1___0_System_String\_}

给端点附加静态权限元数据与认证策略，所有声明均需满足。

```csharp
public static TBuilder RequireKunPermission<TBuilder>(this TBuilder builder, string permission) where TBuilder : IEndpointConventionBuilder
```

#### Parameters

`builder` TBuilder

`permission` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

TBuilder

#### Type Parameters

`TBuilder`
