> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IAuditChainState

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-181/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Maintains the rolling tip of the per-tenant audit hash chain. The interface
exposes a single atomic "acquire-previous, compute-new, commit-new" operation
so implementations are free to centralise the lock, the durable read, and the
write in one transaction.

```csharp
public interface IAuditChainState
```

## Methods

### AppendAsync(string?, Func\<string?, ValueTask\<string>>, CancellationToken) \{#Kun_Audit_IAuditChainState_AppendAsync_System_String_System_Func_System_String_System_Threading_Tasks_ValueTask_System_String___System_Threading_CancellationToken\_}

Appends one entry to the tenant's chain. Within `computeNewHash`
the caller should build the [AuditEntry](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md), call
[AuditContentHasher.Compute](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditcontenthasher.md#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry_) with the supplied previous hash, and
return the freshly computed hash. The implementation guarantees the
read-compute-write sequence is serialised per `tenantKey`
so concurrent commands on the same tenant do not fork the chain.

```csharp
ValueTask<string> AppendAsync(string? tenantKey, Func<string?, ValueTask<string>> computeNewHash, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantKey` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant identifier, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the shared/global chain.

`computeNewHash` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?, [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>>

Factory that, given the previous hash (or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the chain is empty),
produces the new [AuditEntry.ContentHash](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash). May perform the sink write.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

The new content hash, now stored as the chain tip.
