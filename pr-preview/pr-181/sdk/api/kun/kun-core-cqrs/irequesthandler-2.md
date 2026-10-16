> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IRequestHandler\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Cqrs](/pr-preview/pr-181/sdk/api/kun/kun-core-cqrs/index.md)\
Assembly: Kun.Core.dll

Handler 通用基类：处理 `TRequest` 并返回 `TResponse`。

```csharp
public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

`TResponse`

## Methods

### Handle(TRequest, CancellationToken) \{#Kun_Core_Cqrs_IRequestHandler_2_Handle__0_System_Threading_CancellationToken\_}

Handle the request and return the response.

```csharp
ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>
