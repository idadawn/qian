> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IAuditableEntityRegistry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing](/pr-preview/pr-122/sdk/api/kun/kun-auditing/index.md)\
Assembly: Kun.Auditing.dll

Immutable, DI-populated lookup of entity CLR types that participate in audit logging.
Populated at [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
build time by calls to `services.AddAuditable<TEntity>()`.

```csharp
public interface IAuditableEntityRegistry
```

## Remarks

The registry is the single runtime source of truth for auditable entities —
[AuditableAttribute](/pr-preview/pr-122/sdk/api/kun/kun-auditing/auditableattribute.md) is never read at runtime. Backing storage is a
[`FrozenSet`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozenset-1) so membership checks inside the
EF interceptor are O(1).

## Properties

### RegisteredTypes \{#Kun_Auditing_IAuditableEntityRegistry_RegisteredTypes}

Gets the set of CLR types that were registered via
`services.AddAuditable<TEntity>()`. Frozen, case-sensitive type identity.

```csharp
IReadOnlySet<Type> RegisteredTypes { get; }
```

#### Property Value

[IReadOnlySet](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)\<[Type](https://learn.microsoft.com/dotnet/api/system.type)>
