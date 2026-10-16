> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class MessageContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

Metadata accompanying an integration event during handler dispatch.

```csharp
public sealed class MessageContext
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[MessageContext](/sdk/api/kun/kun-eventbus/messagecontext.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### MessageContext() \{#Kun_EventBus_MessageContext__ctor}

```csharp
public MessageContext()
```

## Properties

### EventType \{#Kun_EventBus_MessageContext_EventType}

Stable event type token.

```csharp
public required string EventType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### MessageId \{#Kun_EventBus_MessageContext_MessageId}

Unique message identifier from the outbox envelope.

```csharp
public required Guid MessageId { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### OccurredOn \{#Kun_EventBus_MessageContext_OccurredOn}

UTC timestamp when the message was originally enqueued.

```csharp
public required DateTimeOffset OccurredOn { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### TenantId \{#Kun_EventBus_MessageContext_TenantId}

来自消息信封的租户标识，在解析 handler 前回灌到租户作用域。

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TraceParent \{#Kun_EventBus_MessageContext_TraceParent}

W3C TraceParent from the originating span, if available.

```csharp
public string? TraceParent { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
