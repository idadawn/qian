> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunDbContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Base EF Core DbContext for Kun Framework services.

```csharp
public abstract class KunDbContext : DbContext, IInfrastructure<IServiceProvider>, IDbContextDependencies, IDbSetCache, IDbContextPoolable, IResettableService, IDisposable, IAsyncDisposable, IKunMultiTenantDbContext
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) ←
[KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

#### Implements

[IInfrastructure\<IServiceProvider>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.iinfrastructure-1),
[IDbContextDependencies](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.internal.idbcontextdependencies),
[IDbSetCache](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.internal.idbsetcache),
[IDbContextPoolable](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.internal.idbcontextpoolable),
[IResettableService](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.iresettableservice),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable),
[IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable),
IKunMultiTenantDbContext

#### Inherited Members

[DbContext.Set\<TEntity>()](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.set#microsoft-entityframeworkcore-dbcontext-set-1),
[DbContext.Set\<TEntity>(string)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.set#microsoft-entityframeworkcore-dbcontext-set-1\(system-string\)),
[DbContext.OnConfiguring(DbContextOptionsBuilder)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.onconfiguring),
[DbContext.ConfigureConventions(ModelConfigurationBuilder)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.configureconventions),
[DbContext.OnModelCreating(ModelBuilder)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.onmodelcreating),
[DbContext.SaveChanges()](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechanges#microsoft-entityframeworkcore-dbcontext-savechanges),
[DbContext.SaveChanges(bool)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechanges#microsoft-entityframeworkcore-dbcontext-savechanges\(system-boolean\)),
[DbContext.SaveChangesAsync(CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesasync#microsoft-entityframeworkcore-dbcontext-savechangesasync\(system-threading-cancellationtoken\)),
[DbContext.SaveChangesAsync(bool, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesasync#microsoft-entityframeworkcore-dbcontext-savechangesasync\(system-boolean-system-threading-cancellationtoken\)),
[DbContext.Dispose()](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.dispose),
[DbContext.DisposeAsync()](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.disposeasync),
[DbContext.Entry\<TEntity>(TEntity)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.entry#microsoft-entityframeworkcore-dbcontext-entry-1\(-0\)),
[DbContext.Entry(object)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.entry#microsoft-entityframeworkcore-dbcontext-entry\(system-object\)),
[DbContext.Add\<TEntity>(TEntity)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.add#microsoft-entityframeworkcore-dbcontext-add-1\(-0\)),
[DbContext.AddAsync\<TEntity>(TEntity, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addasync#microsoft-entityframeworkcore-dbcontext-addasync-1\(-0-system-threading-cancellationtoken\)),
[DbContext.Attach\<TEntity>(TEntity)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.attach#microsoft-entityframeworkcore-dbcontext-attach-1\(-0\)),
[DbContext.Update\<TEntity>(TEntity)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.update#microsoft-entityframeworkcore-dbcontext-update-1\(-0\)),
[DbContext.Remove\<TEntity>(TEntity)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.remove#microsoft-entityframeworkcore-dbcontext-remove-1\(-0\)),
[DbContext.Add(object)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.add#microsoft-entityframeworkcore-dbcontext-add\(system-object\)),
[DbContext.AddAsync(object, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addasync#microsoft-entityframeworkcore-dbcontext-addasync\(system-object-system-threading-cancellationtoken\)),
[DbContext.Attach(object)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.attach#microsoft-entityframeworkcore-dbcontext-attach\(system-object\)),
[DbContext.Update(object)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.update#microsoft-entityframeworkcore-dbcontext-update\(system-object\)),
[DbContext.Remove(object)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.remove#microsoft-entityframeworkcore-dbcontext-remove\(system-object\)),
[DbContext.AddRange(params object\[\])](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addrange#microsoft-entityframeworkcore-dbcontext-addrange\(system-object\(\)\)),
[DbContext.AddRangeAsync(params object\[\])](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addrangeasync#microsoft-entityframeworkcore-dbcontext-addrangeasync\(system-object\(\)\)),
[DbContext.AttachRange(params object\[\])](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.attachrange#microsoft-entityframeworkcore-dbcontext-attachrange\(system-object\(\)\)),
[DbContext.UpdateRange(params object\[\])](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.updaterange#microsoft-entityframeworkcore-dbcontext-updaterange\(system-object\(\)\)),
[DbContext.RemoveRange(params object\[\])](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.removerange#microsoft-entityframeworkcore-dbcontext-removerange\(system-object\(\)\)),
[DbContext.AddRange(IEnumerable\<object>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addrange#microsoft-entityframeworkcore-dbcontext-addrange\(system-collections-generic-ienumerable\(\(system-object\)\)\)),
[DbContext.AddRangeAsync(IEnumerable\<object>, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.addrangeasync#microsoft-entityframeworkcore-dbcontext-addrangeasync\(system-collections-generic-ienumerable\(\(system-object\)\)-system-threading-cancellationtoken\)),
[DbContext.AttachRange(IEnumerable\<object>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.attachrange#microsoft-entityframeworkcore-dbcontext-attachrange\(system-collections-generic-ienumerable\(\(system-object\)\)\)),
[DbContext.UpdateRange(IEnumerable\<object>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.updaterange#microsoft-entityframeworkcore-dbcontext-updaterange\(system-collections-generic-ienumerable\(\(system-object\)\)\)),
[DbContext.RemoveRange(IEnumerable\<object>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.removerange#microsoft-entityframeworkcore-dbcontext-removerange\(system-collections-generic-ienumerable\(\(system-object\)\)\)),
[DbContext.Find(Type, params object?\[\]?)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.find#microsoft-entityframeworkcore-dbcontext-find\(system-type-system-object\(\)\)),
[DbContext.FindAsync(Type, params object?\[\]?)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.findasync#microsoft-entityframeworkcore-dbcontext-findasync\(system-type-system-object\(\)\)),
[DbContext.FindAsync(Type, object?\[\]?, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.findasync#microsoft-entityframeworkcore-dbcontext-findasync\(system-type-system-object\(\)-system-threading-cancellationtoken\)),
[DbContext.Find\<TEntity>(params object?\[\]?)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.find#microsoft-entityframeworkcore-dbcontext-find-1\(system-object\(\)\)),
[DbContext.FindAsync\<TEntity>(params object?\[\]?)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.findasync#microsoft-entityframeworkcore-dbcontext-findasync-1\(system-object\(\)\)),
[DbContext.FindAsync\<TEntity>(object?\[\]?, CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.findasync#microsoft-entityframeworkcore-dbcontext-findasync-1\(system-object\(\)-system-threading-cancellationtoken\)),
[DbContext.FromExpression\<TResult>(Expression\<Func\<IQueryable\<TResult>>>)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.fromexpression),
[DbContext.Database](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.database),
[DbContext.ChangeTracker](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.changetracker),
[DbContext.Model](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.model),
[DbContext.ContextId](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.contextid),
[DbContext.SavingChanges](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savingchanges),
[DbContext.SavedChanges](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savedchanges),
[DbContext.SaveChangesFailed](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesfailed),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

#### Extension Methods

[KunSaveChangesResultExtensions.SaveChangesResultAsync(DbContext, CancellationToken)](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-extensions/kunsavechangesresultextensions.md#Kun_EntityFrameworkCore_Extensions_KunSaveChangesResultExtensions_SaveChangesResultAsync_Microsoft_EntityFrameworkCore_DbContext_System_Threading_CancellationToken_)

## Remarks

Applies automatic global query filters for [IMultiTenant](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/imultitenant.md) entities
(filters out rows whose `TenantId` does not match the current tenant scope)
and for [ISoftDelete](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/isoftdelete.md) entities (excludes soft-deleted rows).
Ensures the Outbox is registered for atomic enqueue.

## Constructors

### KunDbContext(DbContextOptions, ITenantContext) \{#Kun_EntityFrameworkCore_KunDbContext__ctor_Microsoft_EntityFrameworkCore_DbContextOptions_Kun_Core_MultiTenancy_ITenantContext\_}

Creates a new [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md).

```csharp
protected KunDbContext(DbContextOptions options, ITenantContext tenantContext)
```

#### Parameters

`options` [DbContextOptions](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptions)

`tenantContext` ITenantContext

## Properties

### DefaultSchema \{#Kun_EntityFrameworkCore_KunDbContext_DefaultSchema}

Default database schema for this context. When non-[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), applied via
[`HasDefaultSchema`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.relationalmodelbuilderextensions.hasdefaultschema)
before entity configurations run — tables, Outbox, and Saga storage all land under this
schema by default. Callers should also configure `MigrationsHistoryTable` to the
same schema on the Npgsql options so migrations + business tables co-locate.

```csharp
protected virtual string? DefaultSchema { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Remarks

Overriding services' DbContexts (e.g. `CatalogDbContext`) should return a service-scoped
schema like `"catalog"` to enable single-database, multi-schema isolation
(SAD §5.1). Leaving this [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) preserves legacy `public`-schema
behavior.

### OutboxMessages \{#Kun_EntityFrameworkCore_KunDbContext_OutboxMessages}

The Outbox message store.

```csharp
public DbSet<OutboxMessage> OutboxMessages { get; }
```

#### Property Value

[DbSet](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbset-1)\<OutboxMessage>

### TenantIdForModelCacheKey \{#Kun_EntityFrameworkCore_KunDbContext_TenantIdForModelCacheKey}

Exposes the current tenant ID for use by a custom `IModelCacheKeyFactory`
so EF Core builds a distinct compiled model per tenant rather than sharing the
first-built model across all instances of the same DbContext type. Query filters also
read this member through the executing DbContext, rather than retaining another host's accessor.

```csharp
public string? TenantIdForModelCacheKey { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### ConfigureConventions(ModelConfigurationBuilder) \{#Kun_EntityFrameworkCore_KunDbContext_ConfigureConventions_Microsoft_EntityFrameworkCore_ModelConfigurationBuilder\_}

Override this method to set defaults and configure conventions before they run. This method is invoked before
[`OnModelCreating`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.onmodelcreating).

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
```

#### Parameters

`configurationBuilder` [ModelConfigurationBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelconfigurationbuilder)

The builder being used to set defaults and configure conventions that will be used to build the model for this context.

#### Remarks

```
If a model is explicitly set on the options for this context (via [`UseModel`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder.usemodel))
then this method will not be run. However, it will still run when creating a compiled model.

See [Pre-convention model building in EF Core](https://aka.ms/efcore-docs-pre-convention) for more information and
examples.
```

### OnConfiguring(DbContextOptionsBuilder) \{#Kun_EntityFrameworkCore_KunDbContext_OnConfiguring_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder\_}

Override this method to configure the database (and other options) to be used for this context.
This method is called for each instance of the context that is created.
The base implementation does nothing.

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
```

#### Parameters

`optionsBuilder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

A builder used to create or modify options for this context. Databases (and other extensions)
typically define extension methods on this object that allow you to configure the context.

#### Remarks

```
In situations where an instance of [`DbContextOptions`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptions) may or may not have been passed
to the constructor, you can use [`IsConfigured`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder.isconfigured) to determine if
the options have already been set, and skip some or all of the logic in
[`OnConfiguring`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.onconfiguring).

See [DbContext lifetime, configuration, and initialization](https://aka.ms/efcore-docs-dbcontext)
for more information and examples.
```

### OnModelCreating(ModelBuilder) \{#Kun_EntityFrameworkCore_KunDbContext_OnModelCreating_Microsoft_EntityFrameworkCore_ModelBuilder\_}

Override this method to further configure the model that was discovered by convention from the entity types
exposed in [`DbSet`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbset-1) properties on your derived context. The resulting model may be cached
and re-used for subsequent instances of your derived context.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
```

#### Parameters

`modelBuilder` [ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

The builder being used to construct the model for this context. Databases (and other extensions) typically
define extension methods on this object that allow you to configure aspects of the model that are specific
to a given database.

#### Remarks

```
If a model is explicitly set on the options for this context (via [`UseModel`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder.usemodel))
then this method will not be run. However, it will still run when creating a compiled model.

See [Modeling entity types and relationships](https://aka.ms/efcore-docs-modeling) for more information and
examples.
```
