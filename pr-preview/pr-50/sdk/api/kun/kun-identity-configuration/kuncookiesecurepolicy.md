> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Enum KunCookieSecurePolicy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Secure-policy for [KunCookieAuthOptions.Secure](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Secure). Mirrors
ASP.NET Core's `CookieSecurePolicy` shape without taking a
dependency on that package from the core abstractions.

```csharp
public enum KunCookieSecurePolicy
```

## Fields

`Always = 0`

Emit the cookie only over HTTPS (default in production).

`None = 1`

Emit the cookie over either scheme (development only).

`SameAsRequest = 2`

Match the scheme of the current request.
