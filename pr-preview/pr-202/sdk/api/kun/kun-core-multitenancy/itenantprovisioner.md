> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface ITenantProvisioner

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

租户生命周期钩子：开户（[ITenantProvisioner.ProvisionAsync](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md#Kun_Core_MultiTenancy_ITenantProvisioner_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_)）与注销（[ITenantProvisioner.DeprovisionAsync](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md#Kun_Core_MultiTenancy_ITenantProvisioner_DeprovisionAsync_System_String_System_Threading_CancellationToken_)）。

```csharp
public interface ITenantProvisioner
```

## Remarks

Shared 策略不需要具体实现 —— 新租户只是多一行 `TenantId` 的业务记录。消费者只需要注册
[NullTenantProvisioner](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/nulltenantprovisioner.md)（或依赖框架提供的 Singleton 默认值）。

SchemaPerTenant 与 DatabasePerTenant 策略依赖本接口的非空实现：在租户开户时分别创建 schema
或独立数据库，并在该隔离容器内运行 EF Core migrations。实现必须是**幂等的** ——
使用 `CREATE SCHEMA IF NOT EXISTS` / `CREATE DATABASE IF NOT EXISTS` 语义；
重复调用不得产生副作用。

调用时机由业务方控制；框架不做自动编排。典型场景：租户 signup API 成功创建租户记录后，
同事务（或补偿事务）里调用 [ITenantProvisioner.ProvisionAsync](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md#Kun_Core_MultiTenancy_ITenantProvisioner_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_)；租户注销走反向流程。

## Methods

### DeprovisionAsync(string, CancellationToken) \{#Kun_Core_MultiTenancy_ITenantProvisioner_DeprovisionAsync_System_String_System_Threading_CancellationToken\_}

清理指定租户的持久化基础件。幂等：对未开户的租户调用不得抛异常。

```csharp
Task DeprovisionAsync(string tenantId, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### ProvisionAsync(TenantInfo, CancellationToken) \{#Kun_Core_MultiTenancy_ITenantProvisioner_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken\_}

为指定租户准备持久化基础件（schema / database / seed 数据）。幂等。

```csharp
Task ProvisionAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
```

#### Parameters

`tenant` [TenantInfo](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
