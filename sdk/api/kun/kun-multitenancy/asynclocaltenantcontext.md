> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AsyncLocalTenantContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy](/sdk/api/kun/kun-multitenancy/index.md)\
Assembly: Kun.MultiTenancy.dll

[ITenantContext](/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) implementation backed by an
[`AsyncLocal`](https://learn.microsoft.com/dotnet/api/system.threading.asynclocal-1) so the tenant id flows with the async
call graph across thread hops, task schedulers, and concurrent
parallel tasks. Pair with `TenantContextMiddleware` (or a
background-worker scope that opens a tenant scope explicitly) to
populate the value.

```csharp
public sealed class AsyncLocalTenantContext : ITenantContext
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AsyncLocalTenantContext](/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md)

#### Implements

ITenantContext

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AsyncLocalTenantContext() \{#Kun_MultiTenancy_AsyncLocalTenantContext__ctor}

创建仅使用显式作用域的上下文。

```csharp
public AsyncLocalTenantContext()
```

### AsyncLocalTenantContext(ITenantIdSource) \{#Kun_MultiTenancy_AsyncLocalTenantContext__ctor_Kun_MultiTenancy_Abstractions_ITenantIdSource\_}

创建可读取已认证请求租户的上下文；显式作用域始终优先。

```csharp
public AsyncLocalTenantContext(ITenantIdSource source)
```

#### Parameters

`source` [ITenantIdSource](/sdk/api/kun/kun-multitenancy-abstractions/itenantidsource.md)

## Properties

### IsMultiTenant \{#Kun_MultiTenancy_AsyncLocalTenantContext_IsMultiTenant}

当前应用是否启用了多租户。

```csharp
public bool IsMultiTenant { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### TenantId \{#Kun_MultiTenancy_AsyncLocalTenantContext_TenantId}

当前请求的租户 ID；后台任务中可能为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### BeginScope(string?) \{#Kun_MultiTenancy_AsyncLocalTenantContext_BeginScope_System_String\_}

Sets [AsyncLocalTenantContext.TenantId](/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md#Kun_MultiTenancy_AsyncLocalTenantContext_TenantId) to `tenantId` for the
returned scope. The previous value is restored on dispose so
nested scopes compose predictably.

```csharp
public IDisposable BeginScope(string? tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)
