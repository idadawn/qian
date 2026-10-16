> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Enum KunSameSiteMode

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

`SameSite` policy for [KunCookieAuthOptions.SameSite](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SameSite).
Mirrors ASP.NET Core's `SameSiteMode` shape without taking a
dependency on that package from the core abstractions.

```csharp
public enum KunSameSiteMode
```

## Fields

`Lax = 1`

`SameSite=Lax`. Cookie is sent on top-level navigation but not
on cross-site subrequests. Recommended default for OIDC code flow.

`None = 0`

`SameSite=None`. Cookie is sent on cross-site requests; must
be paired with [KunCookieSecurePolicy.Always](/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md#Kun_Identity_Configuration_KunCookieSecurePolicy_Always).

`Strict = 2`

`SameSite=Strict`. Cookie is only sent on same-site requests —
blocks even top-level redirects from the IdP callback, so usually
only appropriate for post-login navigation cookies.

`Unspecified = -1`

No `SameSite` attribute emitted (browser default).
