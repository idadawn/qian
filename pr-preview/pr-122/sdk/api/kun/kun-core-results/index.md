> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Core.Results

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md)

强类型错误。相比抛异常，[Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md) 是显式的、可测试的失败信号，
也更符合 CQRS Handler 的函数式风格。

[ResultExtensions](/pr-preview/pr-122/sdk/api/kun/kun-core-results/resultextensions.md)

Synchronous railway-oriented extensions for [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md) and
[Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011.

[ResultFailureException](/pr-preview/pr-122/sdk/api/kun/kun-core-results/resultfailureexception.md)

由 gRPC 拦截器识别并翻译为 `RpcException`。

[TaskResultExtensions](/pr-preview/pr-122/sdk/api/kun/kun-core-results/taskresultextensions.md)

Asynchronous railway-oriented extensions for [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
receivers whose result type is [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md) or
[Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011.

[ValueTaskResultExtensions](/pr-preview/pr-122/sdk/api/kun/kun-core-results/valuetaskresultextensions.md)

Asynchronous railway-oriented extensions for
[`ValueTask`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1) receivers whose result type is
[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md) or [Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011;
mirror of [TaskResultExtensions](/pr-preview/pr-122/sdk/api/kun/kun-core-results/taskresultextensions.md) for hot paths that
avoid [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) allocation (Mediator pipeline, Saga step
executors).

### Structs

[Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)

带返回值的 Result。Handler 成功时携带 `T`，失败时携带 [Result\<T>.Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md#Kun_Core_Results_Result_1_Error)。

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

无返回值的 Result。Handler 在不需要返回数据时使用。

### Enums

[ErrorType](/pr-preview/pr-122/sdk/api/kun/kun-core-results/errortype.md)

错误分类，用于映射到 HTTP/gRPC 状态码。
