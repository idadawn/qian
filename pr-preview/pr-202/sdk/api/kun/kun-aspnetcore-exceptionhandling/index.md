> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.AspNetCore.ExceptionHandling

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[KunExceptionHandler](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-exceptionhandling/kunexceptionhandler.md)

Global exception handler that translates Kun-specific exceptions into
RFC 7807 ProblemDetails responses.

- [ValidationException](/pr-preview/pr-202/sdk/api/kun/kun-validation/validationexception.md) → 400 Bad Request with validation errors.
- [ResultFailureException](/pr-preview/pr-202/sdk/api/kun/kun-core-results/resultfailureexception.md) → mapped via [ErrorToProblemDetailsExtensions.ToProblemDetails](/pr-preview/pr-202/sdk/api/kun/kun-aspnetcore-mapping/errortoproblemdetailsextensions.md#Kun_AspNetCore_Mapping_ErrorToProblemDetailsExtensions_ToProblemDetails_Kun_Core_Results_Error_).
- [DomainException](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/domainexception.md) → 400 Bad Request with the domain error code.
- All other exceptions → rethrown for the default handler to produce 500.
