> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Testing.Results

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Testing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Testing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Testing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[ResultAssertions](/pr-preview/pr-202/sdk/api/kun/kun-testing-results/resultassertions.md)

Fluent assertion extensions for [Result](/pr-preview/pr-202/sdk/api/kun/kun-core-results/result.md) and
[Result\<T>](/pr-preview/pr-202/sdk/api/kun/kun-core-results/result-1.md). On mismatch, throws
[KunAssertionException](/pr-preview/pr-202/sdk/api/kun/kun-testing/kunassertionexception.md) with a precise message that
includes the helper name, the expected value, and the actual
observed value. See ADR-012 §Error-message contract.
