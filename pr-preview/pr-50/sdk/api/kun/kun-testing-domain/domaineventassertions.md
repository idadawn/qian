> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class DomainEventAssertions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Testing` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Testing`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Testing)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Testing.Domain](/pr-preview/pr-50/sdk/api/kun/kun-testing-domain/index.md)\
Assembly: Kun.Testing.dll

Fluent assertion extensions for [IAggregateRoot](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iaggregateroot.md) domain-
event collections. On mismatch, throws
[KunAssertionException](/pr-preview/pr-50/sdk/api/kun/kun-testing/kunassertionexception.md) with a precise message per the
ADR-012 §Error-message contract.

```csharp
public static class DomainEventAssertions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DomainEventAssertions](/pr-preview/pr-50/sdk/api/kun/kun-testing-domain/domaineventassertions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Uses [`OfType`](https://learn.microsoft.com/dotnet/api/system.linq.enumerable.oftype--1) — compile-
time-safe and AOT-friendly (no reflection, no
`MakeGenericMethod`).

## Methods

### ShouldNotRaise\<TEvent>(IAggregateRoot) \{#Kun_Testing_Domain_DomainEventAssertions_ShouldNotRaise__1_Kun_Core_Domain_IAggregateRoot\_}

Asserts the aggregate raised zero events of type
`TEvent`.

```csharp
public static void ShouldNotRaise<TEvent>(this IAggregateRoot aggregate) where TEvent : IDomainEvent
```

#### Parameters

`aggregate` IAggregateRoot

#### Type Parameters

`TEvent`

### ShouldRaise\<TEvent>(IAggregateRoot) \{#Kun_Testing_Domain_DomainEventAssertions_ShouldRaise__1_Kun_Core_Domain_IAggregateRoot\_}

Asserts the aggregate raised exactly one event of type
`TEvent`. Returns the event for further
inspection.

```csharp
public static TEvent ShouldRaise<TEvent>(this IAggregateRoot aggregate) where TEvent : IDomainEvent
```

#### Parameters

`aggregate` IAggregateRoot

#### Returns

TEvent

#### Type Parameters

`TEvent`

### ShouldRaiseExactly\<TEvent>(IAggregateRoot, int) \{#Kun_Testing_Domain_DomainEventAssertions_ShouldRaiseExactly__1_Kun_Core_Domain_IAggregateRoot_System_Int32\_}

Asserts the aggregate raised exactly `count`
events of type `TEvent`. Returns them as a
read-only list preserving the original emission order.

```csharp
public static IReadOnlyList<TEvent> ShouldRaiseExactly<TEvent>(this IAggregateRoot aggregate, int count) where TEvent : IDomainEvent
```

#### Parameters

`aggregate` IAggregateRoot

`count` [int](https://learn.microsoft.com/dotnet/api/system.int32)

#### Returns

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<TEvent>

#### Type Parameters

`TEvent`
