> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunEntityFrameworkCoreServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-dependencyinjection/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Registers Kun EF Core write-side services into the DI container.

```csharp
public static class KunEntityFrameworkCoreServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunEntityFrameworkCoreServiceCollectionExtensions](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-dependencyinjection/kunentityframeworkcoreservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunDataConventions\<TContext>(IServiceCollection) \{#Kun_EntityFrameworkCore_DependencyInjection_KunEntityFrameworkCoreServiceCollectionExtensions_AddKunDataConventions__1_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

为上下文装配审计字段自动填充；模型内配合 ApplyKunDataConventions。

```csharp
public static IServiceCollection AddKunDataConventions<TContext>(this IServiceCollection services) where TContext : DbContext
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Type Parameters

`TContext`

### AddKunEntityFrameworkCore(IServiceCollection) \{#Kun_EntityFrameworkCore_DependencyInjection_KunEntityFrameworkCoreServiceCollectionExtensions_AddKunEntityFrameworkCore_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers the unit-of-work, integration-event outbox bridge, and default domain-event
dispatcher. Callers must separately register their [KunDbContext](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md) subclass
via `AddDbContext`.

```csharp
public static IServiceCollection AddKunEntityFrameworkCore(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

#### Remarks

Also registers [TenantRlsConnectionInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlsconnectioninterceptor.md) as Scoped; it does not
activate unless the caller adds it to their `DbContextOptionsBuilder` via
`opts.AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>())`.

### AddKunTenantRls(IServiceCollection) \{#Kun_EntityFrameworkCore_DependencyInjection_KunEntityFrameworkCoreServiceCollectionExtensions_AddKunTenantRls_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

兼容入口：注册 RLS 双拦截器；推荐使用泛型重载自动装配。Consumers must
also wire the interceptor into their DbContext options via DI resolution, e.g.

```csharp
services.AddDbContext<CatalogDbContext>((sp, options) =>
    options.UseNpgsql(cs)
           .UseKunSchema("catalog")
           .AddInterceptors(sp.GetRequiredService<TenantRlsTransactionInterceptor>()));
```

Scoped lifetime — each request's [ITenantContext](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/itenantcontext.md)
is resolved per transaction. Not added automatically because the interceptor fires a
`set_config` round-trip on every transaction; services without RLS should not pay
that cost.

```csharp
public static IServiceCollection AddKunTenantRls(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

### AddKunTenantRls\<TContext>(IServiceCollection) \{#Kun_EntityFrameworkCore_DependencyInjection_KunEntityFrameworkCoreServiceCollectionExtensions_AddKunTenantRls__1_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

为指定上下文自动装配 RLS 双拦截器与租户模型缓存键。

```csharp
public static IServiceCollection AddKunTenantRls<TContext>(this IServiceCollection services) where TContext : DbContext, IKunMultiTenantDbContext
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

服务容器。

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

原服务容器。

#### Type Parameters

`TContext`

声明租户模型缓存键的上下文。
