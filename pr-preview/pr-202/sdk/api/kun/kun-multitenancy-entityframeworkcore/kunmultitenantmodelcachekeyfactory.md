> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunMultiTenantModelCacheKeyFactory

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.EntityFrameworkCore](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/index.md)\
Assembly: Kun.MultiTenancy.EntityFrameworkCore.dll

[`IModelCacheKeyFactory`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.imodelcachekeyfactory) that emits a distinct compiled
model per tenant when the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) implements
[IKunMultiTenantDbContext](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/ikunmultitenantdbcontext.md). Falls back to EF Core's
default key shape (context type + design-time flag) otherwise, so
consumers who forget to opt in get the same behaviour they had
before.

```csharp
public sealed class KunMultiTenantModelCacheKeyFactory : IModelCacheKeyFactory
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenantModelCacheKeyFactory](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenantmodelcachekeyfactory.md)

#### Implements

[IModelCacheKeyFactory](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.imodelcachekeyfactory)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Register on the [`DbContextOptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder):
`.ReplaceService<IModelCacheKeyFactory, KunMultiTenantModelCacheKeyFactory>()`.

Without this factory, EF Core keys compiled models only on
[`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) type. The first tenant's query filter is
then baked into the shared model — subsequent tenants would read
against the first tenant's filter constant, silently leaking data
across tenant boundaries.

## Constructors

### KunMultiTenantModelCacheKeyFactory() \{#Kun_MultiTenancy_EntityFrameworkCore_KunMultiTenantModelCacheKeyFactory__ctor}

```csharp
public KunMultiTenantModelCacheKeyFactory()
```

## Methods

### Create(DbContext, bool) \{#Kun_MultiTenancy_EntityFrameworkCore_KunMultiTenantModelCacheKeyFactory_Create_Microsoft_EntityFrameworkCore_DbContext_System_Boolean\_}

Gets the model cache key for a given context.

```csharp
public object Create(DbContext context, bool designTime)
```

#### Parameters

`context` [DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)

The context to get the model cache key for.

`designTime` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

Whether the model should contain design-time configuration.

#### Returns

[object](https://learn.microsoft.com/dotnet/api/system.object)

The created key.
