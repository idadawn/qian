> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class OutboxPublisherOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EventBus)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

Configures the outbox publisher background service.

```csharp
public sealed class OutboxPublisherOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OutboxPublisherOptions](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-background/outboxpublisheroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OutboxPublisherOptions() \{#Kun_EventBus_Background_OutboxPublisherOptions__ctor}

```csharp
public OutboxPublisherOptions()
```

## Properties

### BatchSize \{#Kun_EventBus_Background_OutboxPublisherOptions_BatchSize}

Maximum messages fetched per poll. Default: 50.

```csharp
public int BatchSize { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### MaxAttempts \{#Kun_EventBus_Background_OutboxPublisherOptions_MaxAttempts}

Max retry attempts before a message is transitioned to [OutboxStatus.Failed](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md#Kun_EventBus_Outbox_OutboxStatus_Failed). Default: 5.

```csharp
public int MaxAttempts { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### PollInterval \{#Kun_EventBus_Background_OutboxPublisherOptions_PollInterval}

Poll interval between outbox scans. Default: 2 seconds.

```csharp
public TimeSpan PollInterval { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### RetryBaseDelay \{#Kun_EventBus_Background_OutboxPublisherOptions_RetryBaseDelay}

Initial retry delay (exponential backoff base). Default: 1 second.

```csharp
public TimeSpan RetryBaseDelay { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
