> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IKunJwksProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Jwks](/sdk/api/kun/kun-security-jwks/index.md)\
Assembly: Kun.Security.dll

Provides the JSON Web Key Set used for S2S token signature verification.

```csharp
public interface IKunJwksProvider
```

## Methods

### GetAsync(CancellationToken) \{#Kun_Security_Jwks_IKunJwksProvider_GetAsync_System_Threading_CancellationToken\_}

Returns the current [`JsonWebKeySet`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkeyset).

```csharp
Task<JsonWebKeySet> GetAsync(CancellationToken ct = default)
```

#### Parameters

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[JsonWebKeySet](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkeyset)>
