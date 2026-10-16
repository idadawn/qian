> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IValidator\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation](/sdk/api/kun/kun-validation/index.md)\
Assembly: Kun.Validation.dll

Validates instances of `T`. Implementations are registered per
request type via `KunMediatorBuilder.AddValidator<T, TValidator>()` (see
[KunValidationServiceCollectionExtensions](/sdk/api/kun/kun-validation-dependencyinjection/kunvalidationservicecollectionextensions.md)).

```csharp
public interface IValidator<in T>
```

#### Type Parameters

`T`

The type to validate.

## Methods

### ValidateAsync(T, CancellationToken) \{#Kun_Validation_IValidator_1_ValidateAsync__0_System_Threading_CancellationToken\_}

Validate the given instance.

```csharp
ValueTask<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default)
```

#### Parameters

`instance` T

The instance to validate.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)>

A [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md) indicating success or failure.
