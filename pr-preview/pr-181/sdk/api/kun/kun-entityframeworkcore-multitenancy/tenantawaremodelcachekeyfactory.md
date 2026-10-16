> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class TenantAwareModelCacheKeyFactory

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

[`IModelCacheKeyFactory`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.imodelcachekeyfactory) that partitions EF Core's compiled-model cache by
tenant ID in addition to context type and design-time flag. Required for the
[TenantIsolationStrategy.SchemaPerTenant](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_SchemaPerTenant) strategy so
that tenant A's schema-specific model is not reused for tenant B.

```csharp
public sealed class TenantAwareModelCacheKeyFactory : IModelCacheKeyFactory
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantAwareModelCacheKeyFactory](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantawaremodelcachekeyfactory.md)

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

Reads the tenant ID from [KunDbContext.TenantIdForModelCacheKey](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md#Kun_EntityFrameworkCore_KunDbContext_TenantIdForModelCacheKey). Contexts
that do not derive from [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md) fall back to the default type-only
cache key, so registering this factory is safe for mixed contexts in one host.

When `TenantIdForModelCacheKey` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) (e.g. background task
with no scoped tenant), the key uses the placeholder `"__kun_no_tenant__"` so a
single shared "no tenant" model gets cached rather than rebuilding on every call.

## Constructors

### TenantAwareModelCacheKeyFactory() \{#Kun_EntityFrameworkCore_MultiTenancy_TenantAwareModelCacheKeyFactory__ctor}

```csharp
public TenantAwareModelCacheKeyFactory()
```

## Methods

### Create(DbContext, bool) \{#Kun_EntityFrameworkCore_MultiTenancy_TenantAwareModelCacheKeyFactory_Create_Microsoft_EntityFrameworkCore_DbContext_System_Boolean\_}

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
