> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 版本矩阵

> 本页由 `website/scripts/build-versions.ts` 从仓库钉版文件生成（[ADR-021](/design/adr/adr-021-product-family-structure.md) D21.2 第 3 条），不要手改；改版本去改下表列出的源文件。

「哪个 Qian 配哪个 Kun」只看这一张表：Kun 的版本在 `Directory.Packages.props` 的 `KunVersion` 写一次，代码引用的 `Kun.*` 包、[Kun 框架](/sdk/kun/index.md)小节与 [Kun API 参考](/sdk/api/kun/index.md)都按它锁定。

Kun 的同日修复版 `-hotfix.N` 是推送 nuget.org 的正式修复，但 NuGet 按预发布排序；`Kun.*` 一律以 `[$(KunVersion)]` 精确钉定，不依赖最新稳定版排序（ADR-021 D21.3 补注）。

| 文档版本      | Kun        | Qian.\*             | @yue/\*     | .NET SDK                            | Node / pnpm                     | K3s   |
| --------- | ---------- | ------------------- | ----------- | ----------------------------------- | ------------------------------- | ----- |
| main（开发版） | 2026.10.25 | 未发布，开发版 main（54 个包） | 0.1.0（8 个包） | 10.0.100（rollForward latestFeature） | ^22.12.0 \|\| >=24.0.0 / 12.9.1 | v1.35 |

多版本（docs-site.md §5）开启后每个文档版本一行；现在只有开发版 `main`。

## 来源

| 项           | 钉版文件                       | 说明                                                                         |
| ----------- | -------------------------- | -------------------------------------------------------------------------- |
| Kun         | `Directory.Packages.props` | `<KunVersion>`，CalVer；升级 Kun 只改这一处（ADR-021 D21.3）                          |
| Qian.\*     | `packages/*/*.csproj`      | 写了 `<IsPackable>true</IsPackable>` 的项目；版本随首个包族发布钉在 `Directory.Build.props` |
| @yue/\*     | `pnpm-workspace.yaml`      | 工作区里名字以 `@idadawn/yue-` 开头的包（npm 包名；文档里 `@yue/<包>` 是简称，S1-YUE-003 建立）      |
| .NET SDK    | `global.json`              | `sdk.version` 与 `rollForward`                                              |
| Node / pnpm | `package.json`             | `engines.node` 与 `packageManager`                                          |
| K3s         | `deploy/baseline.json`     | 部署基线（S1-DEP-001）；集群本身在部署仓库 `idadawn/iclaw-deploy`                          |

## Qian 包清单

- `Qian.Audit`（未发布）
- `Qian.Audit.Abstractions`（未发布）
- `Qian.Audit.Native`（未发布）
- `Qian.Authz`（未发布）
- `Qian.Authz.Abstractions`（未发布）
- `Qian.Authz.AspNetCore`（未发布）
- `Qian.Capabilities`（未发布）
- `Qian.Capabilities.Abstractions`（未发布）
- `Qian.Capabilities.AspNetCore`（未发布）
- `Qian.Config.Abstractions`（未发布）
- `Qian.Config.Apollo`（未发布）
- `Qian.Config.AspNetCore`（未发布）
- `Qian.Config.Secrets`（未发布）
- `Qian.Data`（未发布）
- `Qian.Data.Abstractions`（未发布）
- `Qian.Data.AspNetCore`（未发布）
- `Qian.Data.Dialect.Pg`（未发布）
- `Qian.Data.Native.Abstractions`（未发布）
- `Qian.Data.Native.Postgres`（未发布）
- `Qian.Docs`（未发布）
- `Qian.Docs.Abstractions`（未发布）
- `Qian.Events`（未发布）
- `Qian.Events.Abstractions`（未发布）
- `Qian.Files`（未发布）
- `Qian.Files.Abstractions`（未发布）
- `Qian.Files.AspNetCore`（未发布）
- `Qian.Gateway`（未发布）
- `Qian.Gateway.Abstractions`（未发布）
- `Qian.Identity.Abstractions`（未发布）
- `Qian.Identity.Client`（未发布）
- `Qian.Identity.Connector.Oidc`（未发布）
- `Qian.Identity.Server`（未发布）
- `Qian.Lifecycle`（未发布）
- `Qian.Lifecycle.Abstractions`（未发布）
- `Qian.Lifecycle.AspNetCore`（未发布）
- `Qian.Plugin.Sdk`（未发布）
- `Qian.Plugin.Sdk.AspNetCore`（未发布）
- `Qian.Plugin.Sdk.Generators`（未发布）
- `Qian.Profiles`（未发布）
- `Qian.Profiles.Abstractions`（未发布）
- `Qian.Registry`（未发布）
- `Qian.Registry.Abstractions`（未发布）
- `Qian.Registry.AspNetCore`（未发布）
- `Qian.Runtime`（未发布）
- `Qian.Runtime.Errors`（未发布）
- `Qian.Runtime.Errors.AspNetCore`（未发布）
- `Qian.Scheduler`（未发布）
- `Qian.Scheduler.Abstractions`（未发布）
- `Qian.Surfaces`（未发布）
- `Qian.Surfaces.Abstractions`（未发布）
- `Qian.Surfaces.Hosting`（未发布）
- `Qian.Tenancy`（未发布）
- `Qian.Tenancy.Abstractions`（未发布）
- `Qian.Tenancy.AspNetCore`（未发布）

## yue 包清单

- `@idadawn/yue-biz` 0.1.0
- `@idadawn/yue-contracts` 0.1.0
- `@idadawn/yue-core` 0.1.0
- `@idadawn/yue-data` 0.1.0
- `@idadawn/yue-form` 0.1.0
- `@idadawn/yue-sdk` 0.1.0
- `@idadawn/yue-shell` 0.1.0
- `@idadawn/yue-tokens` 0.1.0
