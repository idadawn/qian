> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Enum RateLimitPartitionKey

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.RateLimit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.RateLimit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.RateLimit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.RateLimit.Abstractions](/sdk/api/kun/kun-ratelimit-abstractions/index.md)\
Assembly: Kun.RateLimit.dll

Identity dimension the rate limiter partitions on.

```csharp
public enum RateLimitPartitionKey
```

## Fields

`Ip = 2`

Partition by remote IP address, honouring `X-Forwarded-For` when present.

`Tenant = 0`

Partition by `tenant_id` claim of the authenticated principal; falls back to `"anonymous"`.

`User = 1`

Partition by `sub` / `NameIdentifier` claim of the authenticated principal; falls back to `"anonymous"`.
