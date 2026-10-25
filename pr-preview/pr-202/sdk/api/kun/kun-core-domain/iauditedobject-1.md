> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IAuditedObject\<TUserKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

带用户标识的审计接口。
`TUserKey` 可以是 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)、[string](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)、[long](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/integral-numeric-types) 等，
以兼容本地用户、外部 IdP 的 `sub`、数据库自增等不同场景。

```csharp
public interface IAuditedObject<TUserKey> : IAuditedObject
```

#### Type Parameters

`TUserKey`

#### Implements

[IAuditedObject](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/iauditedobject.md)

## Properties

### CreatedBy \{#Kun_Core_Domain_IAuditedObject_1_CreatedBy}

Identity of the user who created the entity.

```csharp
TUserKey? CreatedBy { get; }
```

#### Property Value

TUserKey?

### UpdatedBy \{#Kun_Core_Domain_IAuditedObject_1_UpdatedBy}

Identity of the user who last updated the entity.

```csharp
TUserKey? UpdatedBy { get; }
```

#### Property Value

TUserKey?
