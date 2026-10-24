> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Auditing.Middleware

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Auditing)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuditContextMiddleware](/sdk/api/kun/kun-auditing-middleware/auditcontextmiddleware.md)

ASP.NET Core middleware that populates the request-scoped [AuditContext](/sdk/api/kun/kun-auditing/auditcontext.md)
with data captured from the incoming [`HttpContext`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext): remote IP,
User-Agent header, and the current W3C trace id.
