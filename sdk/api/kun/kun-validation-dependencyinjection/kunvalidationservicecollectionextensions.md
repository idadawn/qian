> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunValidationServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation.DependencyInjection](/sdk/api/kun/kun-validation-dependencyinjection/index.md)\
Assembly: Kun.Validation.dll

Extension methods to register Kun.Validation services into an [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
or a [KunMediatorBuilder](/sdk/api/kun/kun-mediator/kunmediatorbuilder.md).

```csharp
public static class KunValidationServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunValidationServiceCollectionExtensions](/sdk/api/kun/kun-validation-dependencyinjection/kunvalidationservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddValidationBehavior\<TRequest, TResponse>(KunMediatorBuilder) \{#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidationBehavior__2_Kun_Mediator_KunMediatorBuilder\_}

Registers [ValidationPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md) for the given
request+response pair on a [KunMediatorBuilder](/sdk/api/kun/kun-mediator/kunmediatorbuilder.md).

```csharp
public static KunMediatorBuilder AddValidationBehavior<TRequest, TResponse>(this KunMediatorBuilder builder) where TRequest : IRequest<TResponse>
```

#### Parameters

`builder` KunMediatorBuilder

The mediator builder to register the behavior on.

#### Returns

KunMediatorBuilder

The same `builder` for chaining.

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

### AddValidationResultBehavior\<TRequest, TValue>(KunMediatorBuilder) \{#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidationResultBehavior__2_Kun_Mediator_KunMediatorBuilder\_}

Registers [ValidationResultPipelineBehavior\<TRequest, TValue>](/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md)
for the given request whose response is `Result<TValue>`. On
validator failure the behaviour returns
`Result<TValue>.Fail(Error.Validation("request.invalid", ...))`
instead of throwing [ValidationException](/sdk/api/kun/kun-validation/validationexception.md). Do **not**
also call [KunValidationServiceCollectionExtensions.AddValidationBehavior](/sdk/api/kun/kun-validation-dependencyinjection/kunvalidationservicecollectionextensions.md#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidationBehavior__2_Kun_Mediator_KunMediatorBuilder_) for
the same request type — pick one. See
`docs/architecture/ADR-014-Validation-Result-behavior.md`.

```csharp
public static KunMediatorBuilder AddValidationResultBehavior<TRequest, TValue>(this KunMediatorBuilder builder) where TRequest : IRequest<Result<TValue>>
```

#### Parameters

`builder` KunMediatorBuilder

The mediator builder to register the behavior on.

#### Returns

KunMediatorBuilder

The same `builder` for chaining.

#### Type Parameters

`TRequest`

The request type.

`TValue`

The success value carried by the handler's [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md).

### AddValidationResultBehavior\<TRequest>(KunMediatorBuilder) \{#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidationResultBehavior__1_Kun_Mediator_KunMediatorBuilder\_}

Non-generic-response overload of
[KunValidationServiceCollectionExtensions.AddValidationResultBehavior](/sdk/api/kun/kun-validation-dependencyinjection/kunvalidationservicecollectionextensions.md#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidationResultBehavior__2_Kun_Mediator_KunMediatorBuilder_). Registers
[ValidationResultPipelineBehavior\<TRequest>](/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-1.md) for requests
whose response is the non-generic [Result](/sdk/api/kun/kun-core-results/result.md) (void commands).
See ADR-014 §F1.

```csharp
public static KunMediatorBuilder AddValidationResultBehavior<TRequest>(this KunMediatorBuilder builder) where TRequest : IRequest<Result>
```

#### Parameters

`builder` KunMediatorBuilder

The mediator builder to register the behavior on.

#### Returns

KunMediatorBuilder

The same `builder` for chaining.

#### Type Parameters

`TRequest`

The request type.

### AddValidator\<TRequest, TValidator>(IServiceCollection) \{#Kun_Validation_DependencyInjection_KunValidationServiceCollectionExtensions_AddValidator__2_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers `TValidator` as a transient
[IValidator\<T>](/sdk/api/kun/kun-validation/ivalidator-1.md) for `TRequest`.

```csharp
public static IServiceCollection AddValidator<TRequest, TValidator>(this IServiceCollection services) where TValidator : class, IValidator<TRequest>
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to register into.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.

#### Type Parameters

`TRequest`

The request type to validate.

`TValidator`

The validator implementation type.
