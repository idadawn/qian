> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class UnitOfWorkPipelineBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Behaviors](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-behaviors/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Mediator pipeline behavior that commits the unit-of-work after a command handler returns
successfully. Place this behavior after validation but before logging so the commit
participates in the correct observability scope.

```csharp
public sealed class UnitOfWorkPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

Command type.

`TResponse`

Handler response type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[UnitOfWorkPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-behaviors/unitofworkpipelinebehavior-2.md)

#### Implements

IPipelineBehavior\<TRequest, TResponse>

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### UnitOfWorkPipelineBehavior(IUnitOfWork) \{#Kun_EntityFrameworkCore_Behaviors_UnitOfWorkPipelineBehavior_2__ctor_Kun_EntityFrameworkCore_IUnitOfWork\_}

Creates a new [UnitOfWorkPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore-behaviors/unitofworkpipelinebehavior-2.md).

```csharp
public UnitOfWorkPipelineBehavior(IUnitOfWork unitOfWork)
```

#### Parameters

`unitOfWork` [IUnitOfWork](/pr-preview/pr-122/sdk/api/kun/kun-entityframeworkcore/iunitofwork.md)

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_EntityFrameworkCore_Behaviors_UnitOfWorkPipelineBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` RequestHandlerDelegate\<TResponse>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

The response from the inner pipeline.
