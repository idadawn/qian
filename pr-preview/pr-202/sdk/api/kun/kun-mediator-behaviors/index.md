> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Mediator.Behaviors

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[LoggingBehavior\<TRequest, TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/loggingbehavior-2.md)

Pipeline behavior that logs the request name on entry and the elapsed time on exit.
Logs at [`Debug`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel.debug) for normal completion and
[`Warning`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel.warning) when the handler throws.

[PerformanceBehavior\<TRequest, TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/performancebehavior-2.md)

Pipeline behavior that logs a warning when the handler pipeline exceeds a duration
threshold. The threshold is currently hardcoded to 500 ms; a configurable version
backed by `IOptions<PerformanceBehaviorOptions>` is a planned follow-up.

[PerformanceBehaviorOptions](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/performancebehavioroptions.md)

慢命令告警的可配置阈值。

[PermissionBehaviorExtensions](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/permissionbehaviorextensions.md)

为静态声明权限的命令装配判定行为，缺失引擎时拒绝执行。

[TracingBehavior\<TRequest, TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/tracingbehavior-2.md)

Pipeline behavior that starts an [`Activity`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity) for each dispatched request
using the `"Kun.Mediator"` [`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource). Compatible with
OpenTelemetry — attach a listener via `AddSource("Kun.Mediator")` in the
OpenTelemetry builder.
