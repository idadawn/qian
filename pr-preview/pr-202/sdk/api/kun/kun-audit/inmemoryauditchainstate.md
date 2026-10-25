> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class InMemoryAuditChainState

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-202/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Process-local [IAuditChainState](/pr-preview/pr-202/sdk/api/kun/kun-audit/iauditchainstate.md). Serialises read-compute-write
per tenant with a [`SemaphoreSlim`](https://learn.microsoft.com/dotnet/api/system.threading.semaphoreslim). Suitable for tests and
single-instance deployments; multi-instance deployments should back the
chain state with a durable store and DB-level uniqueness so two replicas
cannot fork the chain.

```csharp
public sealed class InMemoryAuditChainState : IAuditChainState, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InMemoryAuditChainState](/pr-preview/pr-202/sdk/api/kun/kun-audit/inmemoryauditchainstate.md)

#### Implements

[IAuditChainState](/pr-preview/pr-202/sdk/api/kun/kun-audit/iauditchainstate.md),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### InMemoryAuditChainState() \{#Kun_Audit_InMemoryAuditChainState__ctor}

```csharp
public InMemoryAuditChainState()
```

## Methods

### AppendAsync(string?, Func\<string?, ValueTask\<string>>, CancellationToken) \{#Kun_Audit_InMemoryAuditChainState_AppendAsync_System_String_System_Func_System_String_System_Threading_Tasks_ValueTask_System_String___System_Threading_CancellationToken\_}

Appends one entry to the tenant's chain. Within `computeNewHash`
the caller should build the [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md), call
[AuditContentHasher.Compute](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditcontenthasher.md#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry_) with the supplied previous hash, and
return the freshly computed hash. The implementation guarantees the
read-compute-write sequence is serialised per `tenantKey`
so concurrent commands on the same tenant do not fork the chain.

```csharp
public ValueTask<string> AppendAsync(string? tenantKey, Func<string?, ValueTask<string>> computeNewHash, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantKey` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant identifier, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the shared/global chain.

`computeNewHash` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?, [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>>

Factory that, given the previous hash (or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the chain is empty),
produces the new [AuditEntry.ContentHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash). May perform the sink write.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

The new content hash, now stored as the chain tip.

### Dispose() \{#Kun_Audit_InMemoryAuditChainState_Dispose}

```csharp
public void Dispose()
```

### PeekTip(string?) \{#Kun_Audit_InMemoryAuditChainState_PeekTip_System_String\_}

Returns the current chain tip for `tenantKey`, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if the chain is empty.

```csharp
public string? PeekTip(string? tenantKey)
```

#### Parameters

`tenantKey` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)?
