> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.Validation

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Validation)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Validation.Behaviors](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/index.md)

[Kun.Validation.DependencyInjection](/pr-preview/pr-50/sdk/api/kun/kun-validation-dependencyinjection/index.md)

### Classes

[ValidationError](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationerror.md)

A single validation failure on a specific property or at the object level.

[ValidationException](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationexception.md)

Thrown by [ValidationPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)
when one or more validators report errors. Carries the full error list so
upstream handlers (e.g. ASP.NET Core exception handlers) can translate it.

### Structs

[ValidationResult](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationresult.md)

Result of running a validator against an instance. [ValidationResult.IsValid](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationresult.md#Kun_Validation_ValidationResult_IsValid) is true iff
[ValidationResult.Errors](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationresult.md#Kun_Validation_ValidationResult_Errors) is empty.

### Interfaces

[IValidator\<T>](/pr-preview/pr-50/sdk/api/kun/kun-validation/ivalidator-1.md)

Validates instances of `T`. Implementations are registered per
request type via `KunMediatorBuilder.AddValidator<T, TValidator>()` (see
[KunValidationServiceCollectionExtensions](/pr-preview/pr-50/sdk/api/kun/kun-validation-dependencyinjection/kunvalidationservicecollectionextensions.md)).
