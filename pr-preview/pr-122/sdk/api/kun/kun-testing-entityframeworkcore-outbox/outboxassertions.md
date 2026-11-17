> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class OutboxAssertions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Testing.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Testing.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Testing.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Testing.EntityFrameworkCore.Outbox](/pr-preview/pr-122/sdk/api/kun/kun-testing-entityframeworkcore-outbox/index.md)\
Assembly: Kun.Testing.EntityFrameworkCore.dll

Fluent assertion extensions for EF-persisted [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)
state. Helpers come in two overload families:
one on [`IEnumerable`](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1) of [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) for
tests that already hold a pre-queried snapshot, and one on
[`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) for the one-liner case. On mismatch, helpers
throw [KunAssertionException](/pr-preview/pr-122/sdk/api/kun/kun-testing/kunassertionexception.md) with a precise message that
includes the helper name, expected `eventTypeId`, observed count,
and the distinct event-type IDs actually present (so the failure
message tells the consumer what WAS staged, not just what wasn't).
See ADR-015 §F3.

```csharp
public static class OutboxAssertions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OutboxAssertions](/pr-preview/pr-122/sdk/api/kun/kun-testing-entityframeworkcore-outbox/outboxassertions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### ShouldHaveExactlyOneOutboxMessage(IEnumerable\<OutboxMessage>, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldHaveExactlyOneOutboxMessage_System_Collections_Generic_IEnumerable_Kun_EventBus_Outbox_OutboxMessage__System_String\_}

Asserts the outbox contains exactly one [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)
whose [OutboxMessage.EventTypeId](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_EventTypeId) equals `eventTypeId`,
and returns it for further inspection.

```csharp
public static OutboxMessage ShouldHaveExactlyOneOutboxMessage(this IEnumerable<OutboxMessage> outbox, string eventTypeId)
```

#### Parameters

`outbox` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<OutboxMessage>

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

OutboxMessage

### ShouldHaveExactlyOneOutboxMessage(DbContext, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldHaveExactlyOneOutboxMessage_Microsoft_EntityFrameworkCore_DbContext_System_String\_}

Asserts the DbContext's [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) set contains
exactly one message with the given `eventTypeId`,
and returns it.

```csharp
public static OutboxMessage ShouldHaveExactlyOneOutboxMessage(this DbContext db, string eventTypeId)
```

#### Parameters

`db` [DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

OutboxMessage

### ShouldHaveOutboxMessage(IEnumerable\<OutboxMessage>, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldHaveOutboxMessage_System_Collections_Generic_IEnumerable_Kun_EventBus_Outbox_OutboxMessage__System_String\_}

Asserts the outbox contains at least one [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)
whose [OutboxMessage.EventTypeId](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_EventTypeId) equals `eventTypeId`
(ordinal comparison).

```csharp
public static void ShouldHaveOutboxMessage(this IEnumerable<OutboxMessage> outbox, string eventTypeId)
```

#### Parameters

`outbox` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<OutboxMessage>

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

### ShouldHaveOutboxMessage(DbContext, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldHaveOutboxMessage_Microsoft_EntityFrameworkCore_DbContext_System_String\_}

Asserts the DbContext's [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) set contains
at least one message with the given `eventTypeId`.
Equivalent to
`db.Set<OutboxMessage>().AsEnumerable().ShouldHaveOutboxMessage(eventTypeId)`.

```csharp
public static void ShouldHaveOutboxMessage(this DbContext db, string eventTypeId)
```

#### Parameters

`db` [DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

### ShouldNotHaveOutboxMessage(IEnumerable\<OutboxMessage>, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldNotHaveOutboxMessage_System_Collections_Generic_IEnumerable_Kun_EventBus_Outbox_OutboxMessage__System_String\_}

Asserts the outbox contains no [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) whose
[OutboxMessage.EventTypeId](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_EventTypeId) equals `eventTypeId`.

```csharp
public static void ShouldNotHaveOutboxMessage(this IEnumerable<OutboxMessage> outbox, string eventTypeId)
```

#### Parameters

`outbox` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<OutboxMessage>

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

### ShouldNotHaveOutboxMessage(DbContext, string) \{#Kun_Testing_EntityFrameworkCore_Outbox_OutboxAssertions_ShouldNotHaveOutboxMessage_Microsoft_EntityFrameworkCore_DbContext_System_String\_}

Asserts the DbContext's [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) set contains
no messages with the given `eventTypeId`.

```csharp
public static void ShouldNotHaveOutboxMessage(this DbContext db, string eventTypeId)
```

#### Parameters

`db` [DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)
