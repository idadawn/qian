> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SystemClock

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Core)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Time](/sdk/api/kun/kun-core-time/index.md)\
Assembly: Kun.Core.dll

Default [ISystemClock](/sdk/api/kun/kun-core-time/isystemclock.md) implementation backed by [`System`](https://learn.microsoft.com/dotnet/api/system.timeprovider.system).

```csharp
public sealed class SystemClock : ISystemClock
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SystemClock](/sdk/api/kun/kun-core-time/systemclock.md)

#### Implements

[ISystemClock](/sdk/api/kun/kun-core-time/isystemclock.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Properties

### Instance \{#Kun_Core_Time_SystemClock_Instance}

Singleton instance — [SystemClock](/sdk/api/kun/kun-core-time/systemclock.md) has no state.

```csharp
public static SystemClock Instance { get; }
```

#### Property Value

[SystemClock](/sdk/api/kun/kun-core-time/systemclock.md)

### UtcNow \{#Kun_Core_Time_SystemClock_UtcNow}

Current UTC time.

```csharp
public DateTimeOffset UtcNow { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)
