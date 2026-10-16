> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Security.Http

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[PropagateS2sTokenHandler](/sdk/api/kun/kun-security-http/propagates2stokenhandler.md)

A [`DelegatingHandler`](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler) that copies the `Authorization` header
from the current incoming HTTP request to all outgoing requests made by the
[`HttpClient`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) it is attached to.
This propagates the S2S JWT transparently across service boundaries.
