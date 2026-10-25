> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunIdentityServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.DependencyInjection](/sdk/api/kun/kun-identity-dependencyinjection/index.md)\
Assembly: Kun.Identity.dll

Service-collection extensions that register Kun.Identity.

```csharp
public static class KunIdentityServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunIdentityServiceCollectionExtensions](/sdk/api/kun/kun-identity-dependencyinjection/kunidentityservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunIdentity(IServiceCollection, Action\<KunIdentityBuilder>) \{#Kun_Identity_DependencyInjection_KunIdentityServiceCollectionExtensions_AddKunIdentity_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Identity_Configuration_KunIdentityBuilder\_\_}

Registers the Kun.Identity facade. Configure via the fluent
[KunIdentityBuilder](/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md): call `AddJwtBearer`,
`AddS2SIssuer`, and/or `AddOidcClient` on the builder.
Calling `AddKunIdentity` on its own is a no-op — it only
wires the builder surface.

```csharp
public static IServiceCollection AddKunIdentity(this IServiceCollection services, Action<KunIdentityBuilder> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunIdentityBuilder](/sdk/api/kun/kun-identity-configuration/kunidentitybuilder.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
