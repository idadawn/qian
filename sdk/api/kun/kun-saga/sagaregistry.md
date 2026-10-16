> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SagaRegistry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga](/sdk/api/kun/kun-saga/index.md)\
Assembly: Kun.Saga.dll

Immutable, AOT-safe registry mapping saga names to their execution delegates.
Built in the current execution scope by [KunSagaBuilder](/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md) and
resolved after tenant scope restoration. Uses a [`FrozenDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozendictionary-2) for O(1) lookup.

```csharp
public sealed class SagaRegistry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaRegistry](/sdk/api/kun/kun-saga/sagaregistry.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaRegistry(IEnumerable\<SagaRegistration>) \{#Kun_Saga_SagaRegistry__ctor_System_Collections_Generic_IEnumerable_Kun_Saga_SagaRegistration\_\_}

Initializes a new [SagaRegistry](/sdk/api/kun/kun-saga/sagaregistry.md) from the given registrations.

```csharp
public SagaRegistry(IEnumerable<SagaRegistration> registrations)
```

#### Parameters

`registrations` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[SagaRegistration](/sdk/api/kun/kun-saga/sagaregistration.md)>

All registered saga entries keyed by saga name.

## Properties

### RegisteredNames \{#Kun_Saga_SagaRegistry_RegisteredNames}

Gets all registered saga names.

```csharp
public IEnumerable<string> RegisteredNames { get; }
```

#### Property Value

[IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

## Methods

### Find(string) \{#Kun_Saga_SagaRegistry_Find_System_String\_}

Looks up the registration for a saga by name.

```csharp
public SagaRegistration? Find(string sagaName)
```

#### Parameters

`sagaName` [string](https://learn.microsoft.com/dotnet/api/system.string)

The stable saga name (e.g. `OrderCreationSaga`).

#### Returns

[SagaRegistration](/sdk/api/kun/kun-saga/sagaregistration.md)?

The [SagaRegistration](/sdk/api/kun/kun-saga/sagaregistration.md), or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if not found.
