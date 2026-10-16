> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Security.Security

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[JwtCurrentUser](/sdk/api/kun/kun-security-security/jwtcurrentuser.md)

[ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md) implementation that reads `sub`, `name`,
and `roles` claims from the authenticated [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)
on the current HTTP request.
Also implements [ITracedCurrentUser](/sdk/api/kun/kun-core-security/itracedcurrentuser.md) to expose the `trace_id` claim
propagated from the gateway S2S JWT for OpenTelemetry trace continuity.
