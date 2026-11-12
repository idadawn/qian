> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Core.Cqrs

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Interfaces

[ICommand\<TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-core-cqrs/icommand-1.md)

Command：写操作请求，带返回值。

[ICommand](/pr-preview/pr-202/sdk/api/kun/kun-core-cqrs/icommand.md)

Command：写操作请求，无返回值（仍通过 Result 标识成败）。

[IQuery\<TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-core-cqrs/iquery-1.md)

Query：只读请求。应当在读侧走 Dapper AOT 等高性能路径。

[IRequest\<TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-core-cqrs/irequest-1.md)

请求标记，包含返回类型信息。由 Kun.Mediator 调度。

[IRequestHandler\<TRequest, TResponse>](/pr-preview/pr-202/sdk/api/kun/kun-core-cqrs/irequesthandler-2.md)

Handler 通用基类：处理 `TRequest` 并返回 `TResponse`。
