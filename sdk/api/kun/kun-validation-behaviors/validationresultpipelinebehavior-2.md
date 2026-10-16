> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ValidationResultPipelineBehavior\<TRequest, TValue>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation.Behaviors](/sdk/api/kun/kun-validation-behaviors/index.md)\
Assembly: Kun.Validation.dll

Pipeline behavior that runs every registered [IValidator\<T>](/sdk/api/kun/kun-validation/ivalidator-1.md)
against the incoming request and returns
`Result<TValue>.Fail(Error.Validation("request.invalid", …))`
instead of throwing when any report errors. Companion to the
exception-throwing [ValidationPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)
for handlers that deliberately compose via [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md)
and want validation failures to surface through the normal
railway pipeline (see `docs/architecture/ADR-011-Result-async-extensions.md`
and `docs/architecture/ADR-014-Validation-Result-behavior.md`).

```csharp
public sealed class ValidationResultPipelineBehavior<TRequest, TValue> : IPipelineBehavior<TRequest, Result<TValue>> where TRequest : IRequest<Result<TValue>>
```

#### Type Parameters

`TRequest`

The request type being validated.

`TValue`

The success value carried by the handler's [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md).

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValidationResultPipelineBehavior\<TRequest, TValue>](/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md)

#### Implements

IPipelineBehavior\<TRequest, Result\<TValue>>

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Register per request type via
`KunMediatorBuilder.AddValidationResultBehavior<TRequest, TValue>()`;
do **not** register alongside [ValidationPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)
for the same request — pick one.

## Constructors

### ValidationResultPipelineBehavior(IEnumerable\<IValidator\<TRequest>>) \{#Kun_Validation_Behaviors_ValidationResultPipelineBehavior_2__ctor_System_Collections_Generic_IEnumerable_Kun_Validation_IValidator__0\_\_\_}

Initializes a new instance of
[ValidationResultPipelineBehavior\<TRequest, TValue>](/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md).

```csharp
public ValidationResultPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
```

#### Parameters

`validators` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[IValidator](/sdk/api/kun/kun-validation/ivalidator-1.md)\<TRequest>>

All registered validators for `TRequest`.

## Methods

### Handle(TRequest, RequestHandlerDelegate\<Result\<TValue>>, CancellationToken) \{#Kun_Validation_Behaviors_ValidationResultPipelineBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate_Kun_Core_Results_Result__1___System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<Result<TValue>> Handle(TRequest request, RequestHandlerDelegate<Result<TValue>> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` RequestHandlerDelegate\<Result\<TValue>>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<Result\<TValue>>

The response from the inner pipeline.
