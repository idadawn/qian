> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class NullTenantProvisioner

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

[ITenantProvisioner](/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) 的 no-op 默认实现，用于 Shared 策略或不需要租户生命周期
钩子的宿主。两个方法都立即返回 [`CompletedTask`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.completedtask)。

```csharp
public sealed class NullTenantProvisioner : ITenantProvisioner
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NullTenantProvisioner](/sdk/api/kun/kun-core-multitenancy/nulltenantprovisioner.md)

#### Implements

[ITenantProvisioner](/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NullTenantProvisioner() \{#Kun_Core_MultiTenancy_NullTenantProvisioner__ctor}

允许 DI 容器将其作为 Singleton 构造。

```csharp
public NullTenantProvisioner()
```

## Properties

### Instance \{#Kun_Core_MultiTenancy_NullTenantProvisioner_Instance}

Singleton 实例；该类型无状态。

```csharp
public static NullTenantProvisioner Instance { get; }
```

#### Property Value

[NullTenantProvisioner](/sdk/api/kun/kun-core-multitenancy/nulltenantprovisioner.md)

## Methods

### DeprovisionAsync(string, CancellationToken) \{#Kun_Core_MultiTenancy_NullTenantProvisioner_DeprovisionAsync_System_String_System_Threading_CancellationToken\_}

清理指定租户的持久化基础件。幂等：对未开户的租户调用不得抛异常。

```csharp
public Task DeprovisionAsync(string tenantId, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### ProvisionAsync(TenantInfo, CancellationToken) \{#Kun_Core_MultiTenancy_NullTenantProvisioner_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken\_}

为指定租户准备持久化基础件（schema / database / seed 数据）。幂等。

```csharp
public Task ProvisionAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
```

#### Parameters

`tenant` [TenantInfo](/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
