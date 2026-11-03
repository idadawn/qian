> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.Validation.Behaviors

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Validation)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Classes

[ValidationPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)

Pipeline behavior that runs every registered [IValidator\<T>](/pr-preview/pr-50/sdk/api/kun/kun-validation/ivalidator-1.md) against the
incoming request and throws [ValidationException](/pr-preview/pr-50/sdk/api/kun/kun-validation/validationexception.md) if any report errors.

For Result-based semantics (returning `Result.Fail(Error.Validation(...))` instead
of throwing), compose this behavior inside a user-written "ValidationResultAdapter"
behavior — shipped as a follow-up.

[ValidationResultPipelineBehavior\<TRequest, TValue>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md)

Pipeline behavior that runs every registered [IValidator\<T>](/pr-preview/pr-50/sdk/api/kun/kun-validation/ivalidator-1.md)
against the incoming request and returns
`Result<TValue>.Fail(Error.Validation("request.invalid", …))`
instead of throwing when any report errors. Companion to the
exception-throwing [ValidationPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)
for handlers that deliberately compose via [Result\<T>](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)
and want validation failures to surface through the normal
railway pipeline (see `docs/architecture/ADR-011-Result-async-extensions.md`
and `docs/architecture/ADR-014-Validation-Result-behavior.md`).

[ValidationResultPipelineBehavior\<TRequest>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-1.md)

Non-generic variant of
[ValidationResultPipelineBehavior\<TRequest, TValue>](/pr-preview/pr-50/sdk/api/kun/kun-validation-behaviors/validationresultpipelinebehavior-2.md) for
handlers whose response is the non-generic [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)
(void commands with no return value). Returns
`Result.Fail(Error.Validation("request.invalid", …))`
instead of throwing when validators report errors. Closes ADR-014
§F1 follow-up.
