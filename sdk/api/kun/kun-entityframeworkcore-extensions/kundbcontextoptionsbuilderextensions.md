> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunDbContextOptionsBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Extensions](/sdk/api/kun/kun-entityframeworkcore-extensions/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Provides Kun-opinionated configuration helpers for [`DbContextOptionsBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder).

```csharp
public static class KunDbContextOptionsBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunDbContextOptionsBuilderExtensions](/sdk/api/kun/kun-entityframeworkcore-extensions/kundbcontextoptionsbuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### UseKunSchema(DbContextOptionsBuilder, string) \{#Kun_EntityFrameworkCore_Extensions_KunDbContextOptionsBuilderExtensions_UseKunSchema_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_System_String\_}

Wires a service-scoped default schema through EF Core's migrations history table so
`__ef_migrations_history` co-locates with business tables under
`schema`. Use in tandem with `KunDbContext.DefaultSchema`
overriding the same value — the [SchemaConsistencyInterceptor](/sdk/api/kun/kun-entityframeworkcore-interceptors/schemaconsistencyinterceptor.md)
registered automatically by [KunDbContext](/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md) raises a clear exception at the
first `SaveChangesAsync` if the two diverge.

```csharp
public static DbContextOptionsBuilder UseKunSchema(this DbContextOptionsBuilder builder, string schema)
```

#### Parameters

`builder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The options builder to configure. A relational provider
(e.g. `.UseNpgsql(...)`) must already be applied.

`schema` [string](https://learn.microsoft.com/dotnet/api/system.string)

Service-scoped schema name (e.g. `"catalog"`).

#### Returns

[DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder)

The same `builder` for chaining.

#### Exceptions

[ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

`builder` is null.

[ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

`schema` is null/empty.

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

No relational provider has been configured
on `builder`.

### UseKunSchema\<TContext>(DbContextOptionsBuilder\<TContext>, string) \{#Kun_EntityFrameworkCore_Extensions_KunDbContextOptionsBuilderExtensions_UseKunSchema__1_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder___0__System_String\_}

Strongly-typed overload of [KunDbContextOptionsBuilderExtensions.UseKunSchema](/sdk/api/kun/kun-entityframeworkcore-extensions/kundbcontextoptionsbuilderextensions.md#Kun_EntityFrameworkCore_Extensions_KunDbContextOptionsBuilderExtensions_UseKunSchema_Microsoft_EntityFrameworkCore_DbContextOptionsBuilder_System_String_).

```csharp
public static DbContextOptionsBuilder<TContext> UseKunSchema<TContext>(this DbContextOptionsBuilder<TContext> builder, string schema) where TContext : DbContext
```

#### Parameters

`builder` [DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder-1)\<TContext>

`schema` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[DbContextOptionsBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontextoptionsbuilder-1)\<TContext>

#### Type Parameters

`TContext`
