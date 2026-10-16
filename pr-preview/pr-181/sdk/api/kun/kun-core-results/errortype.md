> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Enum ErrorType

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/pr-preview/pr-181/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

错误分类，用于映射到 HTTP/gRPC 状态码。

```csharp
public enum ErrorType
```

## Fields

`Conflict = 3`

State conflict (e.g. duplicate, optimistic concurrency).

`Failure = 0`

Generic unclassified failure.

`Forbidden = 5`

Caller is authenticated but not permitted.

`NotFound = 2`

Requested resource was not found.

`Unauthorized = 4`

Caller is not authenticated.

`Unavailable = 6`

A dependency is temporarily unavailable.

`Validation = 1`

Input validation failure.
