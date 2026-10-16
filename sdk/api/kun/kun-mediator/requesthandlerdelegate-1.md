> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Delegate RequestHandlerDelegate\<TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

Delegate passed to a pipeline behavior representing the next step in the chain.

```csharp
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>()
```

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

#### Type Parameters

`TResponse`

The response type returned by the handler.
