> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Testing.EntityFrameworkCore.Outbox

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Testing.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Testing.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Testing.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[OutboxAssertions](/pr-preview/pr-122/sdk/api/kun/kun-testing-entityframeworkcore-outbox/outboxassertions.md)

Fluent assertion extensions for EF-persisted [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)
state. Helpers come in two overload families:
one on [`IEnumerable`](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1) of [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) for
tests that already hold a pre-queried snapshot, and one on
[`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) for the one-liner case. On mismatch, helpers
throw [KunAssertionException](/pr-preview/pr-122/sdk/api/kun/kun-testing/kunassertionexception.md) with a precise message that
includes the helper name, expected `eventTypeId`, observed count,
and the distinct event-type IDs actually present (so the failure
message tells the consumer what WAS staged, not just what wasn't).
See ADR-015 §F3.
