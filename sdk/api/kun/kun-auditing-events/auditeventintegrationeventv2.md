> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AuditEventIntegrationEventV2

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing.Events](/sdk/api/kun/kun-auditing-events/index.md)\
Assembly: Kun.Auditing.dll

规范审计事件 v2：使用字符串租户与主体，保留 v1 类型供迁移。

```csharp
public sealed record AuditEventIntegrationEventV2 : IEquatable<AuditEventIntegrationEventV2>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)

#### Implements

[IEquatable\<AuditEventIntegrationEventV2>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuditEventIntegrationEventV2(Guid, string?, string?, string, string?, string, string, string?, string?, string?, DateTimeOffset) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2__ctor_System_Guid_System_String_System_String_System_String_System_String_System_String_System_String_System_String_System_String_System_String_System_DateTimeOffset\_}

规范审计事件 v2：使用字符串租户与主体，保留 v1 类型供迁移。

```csharp
public AuditEventIntegrationEventV2(Guid Id, string? TenantId, string? UserId, string ActorType, string? ActorId, string Action, string ResourceType, string? ResourceId, string? TraceId, string? IpAddress, DateTimeOffset CreatedAt)
```

#### Parameters

`Id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

与本地 AuditLog 相同的事件标识。

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

来自租户上下文的标识。

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

来自已认证用户上下文的标识。

`ActorType` [string](https://learn.microsoft.com/dotnet/api/system.string)

主体类别，支持 agent。

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

主体标识。

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)

实体变更类型。

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)

实体类型。

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

实体主键。

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

链路标识。

`IpAddress` [string](https://learn.microsoft.com/dotnet/api/system.string)?

请求来源地址。

`CreatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

审计时间。

## Properties

### Action \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_Action}

实体变更类型。

```csharp
public string Action { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ActorId \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_ActorId}

主体标识。

```csharp
public string? ActorId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorType \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_ActorType}

主体类别，支持 agent。

```csharp
public string ActorType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CreatedAt \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_CreatedAt}

审计时间。

```csharp
public DateTimeOffset CreatedAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Id \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_Id}

与本地 AuditLog 相同的事件标识。

```csharp
public Guid Id { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### IpAddress \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_IpAddress}

请求来源地址。

```csharp
public string? IpAddress { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceId \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_ResourceId}

实体主键。

```csharp
public string? ResourceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceType \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_ResourceType}

实体类型。

```csharp
public string ResourceType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TenantId \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_TenantId}

来自租户上下文的标识。

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TraceId \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_TraceId}

链路标识。

```csharp
public string? TraceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_UserId}

来自已认证用户上下文的标识。

```csharp
public string? UserId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### \<Clone>$() \{#Kun_Auditing_Events_AuditEventIntegrationEventV2__Clone\_\_}

```csharp
public AuditEventIntegrationEventV2 <Clone>$()
```

#### Returns

[AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)

### Deconstruct(out Guid, out string?, out string?, out string, out string?, out string, out string, out string?, out string?, out string?, out DateTimeOffset) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_Deconstruct_System_Guid__System_String__System_String__System_String__System_String__System_String__System_String__System_String__System_String__System_String__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Guid Id, out string? TenantId, out string? UserId, out string ActorType, out string? ActorId, out string Action, out string ResourceType, out string? ResourceId, out string? TraceId, out string? IpAddress, out DateTimeOffset CreatedAt)
```

#### Parameters

`Id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ActorType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`IpAddress` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`CreatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(AuditEventIntegrationEventV2?) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_Equals_Kun_Auditing_Events_AuditEventIntegrationEventV2\_}

```csharp
public bool Equals(AuditEventIntegrationEventV2? other)
```

#### Parameters

`other` [AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(AuditEventIntegrationEventV2?, AuditEventIntegrationEventV2?) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_op_Equality_Kun_Auditing_Events_AuditEventIntegrationEventV2_Kun_Auditing_Events_AuditEventIntegrationEventV2\_}

```csharp
public static bool operator ==(AuditEventIntegrationEventV2? left, AuditEventIntegrationEventV2? right)
```

#### Parameters

`left` [AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)?

`right` [AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(AuditEventIntegrationEventV2?, AuditEventIntegrationEventV2?) \{#Kun_Auditing_Events_AuditEventIntegrationEventV2_op_Inequality_Kun_Auditing_Events_AuditEventIntegrationEventV2_Kun_Auditing_Events_AuditEventIntegrationEventV2\_}

```csharp
public static bool operator !=(AuditEventIntegrationEventV2? left, AuditEventIntegrationEventV2? right)
```

#### Parameters

`left` [AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)?

`right` [AuditEventIntegrationEventV2](/sdk/api/kun/kun-auditing-events/auditeventintegrationeventv2.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
