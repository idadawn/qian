> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ValidationPipelineBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Validation)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation.Behaviors](/sdk/api/kun/kun-validation-behaviors/index.md)\
Assembly: Kun.Validation.dll

Pipeline behavior that runs every registered [IValidator\<T>](/sdk/api/kun/kun-validation/ivalidator-1.md) against the
incoming request and throws [ValidationException](/sdk/api/kun/kun-validation/validationexception.md) if any report errors.

For Result-based semantics (returning `Result.Fail(Error.Validation(...))` instead
of throwing), compose this behavior inside a user-written "ValidationResultAdapter"
behavior — shipped as a follow-up.

```csharp
public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

The request type being validated.

`TResponse`

The response type returned by the handler.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValidationPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)

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

### ValidationPipelineBehavior(IEnumerable\<IValidator\<TRequest>>) \{#Kun_Validation_Behaviors_ValidationPipelineBehavior_2__ctor_System_Collections_Generic_IEnumerable_Kun_Validation_IValidator__0\_\_\_}

Initializes a new instance of [ValidationPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md).

```csharp
public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
```

#### Parameters

`validators` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[IValidator](/sdk/api/kun/kun-validation/ivalidator-1.md)\<TRequest>>

All registered validators for `TRequest`.

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Validation_Behaviors_ValidationPipelineBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

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
