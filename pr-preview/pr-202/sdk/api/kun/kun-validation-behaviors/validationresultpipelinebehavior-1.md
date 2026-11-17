> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class ValidationResultPipelineBehavior\<TRequest>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation.Behaviors](/pr-preview/pr-202/sdk/api/kun/kun-validation-behaviors/index.md)\
Assembly: Kun.Validation.dll

Non-generic variant of
[ValidationResultPipelineBehavior\<TRequest, TValue>](/pr-preview/pr-202/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md) for
handlers whose response is the non-generic [Result](/pr-preview/pr-202/sdk/api/kun/kun-core-results/result.md)
(void commands with no return value). Returns
`Result.Fail(Error.Validation("request.invalid", …))`
instead of throwing when validators report errors. Closes ADR-014
§F1 follow-up.

```csharp
public sealed class ValidationResultPipelineBehavior<TRequest> : IPipelineBehavior<TRequest, Result> where TRequest : IRequest<Result>
```

#### Type Parameters

`TRequest`

The request type being validated.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValidationResultPipelineBehavior\<TRequest>](/pr-preview/pr-202/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-1.md)

#### Implements

IPipelineBehavior\<TRequest, Result>

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ValidationResultPipelineBehavior(IEnumerable\<IValidator\<TRequest>>) \{#Kun_Validation_Behaviors_ValidationResultPipelineBehavior_1__ctor_System_Collections_Generic_IEnumerable_Kun_Validation_IValidator__0\_\_\_}

Initializes a new instance of
[ValidationResultPipelineBehavior\<TRequest>](/pr-preview/pr-202/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-1.md).

```csharp
public ValidationResultPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
```

#### Parameters

`validators` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[IValidator](/pr-preview/pr-202/sdk/api/kun/kun-validation/ivalidator-1.md)\<TRequest>>

All registered validators for `TRequest`.

## Methods

### Handle(TRequest, RequestHandlerDelegate\<Result>, CancellationToken) \{#Kun_Validation_Behaviors_ValidationResultPipelineBehavior_1_Handle__0_Kun_Mediator_RequestHandlerDelegate_Kun_Core_Results_Result__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<Result> Handle(TRequest request, RequestHandlerDelegate<Result> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` RequestHandlerDelegate\<Result>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<Result>

The response from the inner pipeline.
