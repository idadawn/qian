---
title: 宿主与框架
description: 同一组 Qian 组件的两种用法：装进自己的 ASP.NET Core 宿主（独立模式），或放进 platform profile；前端外壳与 Kun 的关系。
part: start
project: qian
audience: [developer, operator]
updated: 2026-10-08
---

# 宿主与框架

## 两种模式，同一组包

| 模式 | 宿主 | 装什么 | 适合 |
|---|---|---|---|
| 独立模式 | 你自己的 ASP.NET Core 项目（standalone profile） | `Qian.Runtime` + 所选组件的 Definition / Provider 包；配置走标准 `IConfiguration`，租户默认单租户 | 只想用某几项能力，不部署 Qian 平台 |
| 平台模式 | `platform` profile 的宿主 | base 组合包里的全部平台插件，含注册表、生命周期、能力目录三件套 | 多租户平台本体 |

两种模式下组件行为一致，由同一套 TCK 在两种模式各跑一遍保证（[ADR-019](/design/adr/adr-019-library-first-packages)）。`Qian.Runtime` 是组件库唯一必需的 Qian 依赖，组件之间只经 `*.Abstractions` 引用（依赖图门禁见 [seam](/start/concepts/seam)）。

## 五个 profile

可部署单元是 profile。S1 随发行版交付五个：

| profile | 用途 |
|---|---|
| `platform` | 平台本体：base 组合包 + 前端外壳，可选模型网关；不承诺 AOT |
| `gateway` | 业务 API 网关边缘，可 AOT |
| `identity` | 身份服务（唯一 issuer），独立安全边界 |
| `standalone` | 消费者宿主安装的最小组合包，`qian init --template dotnet-webapi` 的默认 |
| `minimal` | 测试与 SDK 用的最小插件树：TCK 宿主、CLI 自身运行时 |

组合包清单见 [profile、组合包与 patch](/cli/profile-patch)。

## 前端

前端用 React + [yue 组件库](/components/yue/)。外壳 [qian.shell](/components/platform/qian.shell) 是 MF2 宿主：插件的界面以远程模块按需加载，挂到版本化插槽上（[ADR-013](/design/adr/adr-013-frontend-yue)）。前端外壳自身也是一棵由 `@qian/runtime` 装载的插件树，编程模型见 [前端 SDK](/sdk/frontend/)。

## 与 Kun 的关系

Qian 平台基于 Kun 框架构建，前端使用 yue 组件库（[ADR-021](/design/adr/adr-021-product-family-structure)）。Kun 是 .NET 10 云原生微服务开发框架，是 `Qian.Runtime` 与平台插件的**实现依赖**，以 NuGet + CalVer 锁版本消费；`Kun.*` 不改，它的 `AddKunXxx()` 模块在 Qian 里包装成插件（[ADR-001](/design/adr/adr-001-kun-relationship)、[ADR-020 D20.10](/design/adr/adr-020-component-model-v2)）。

Kun 的文档在 SDK 部分的 [Kun 框架](/sdk/kun/) 小节：按 `KunVersion` 锁定的版本从 kun 仓库镜像，与 Qian 实际依赖的 Kun 是同一版本。Kun 模块与平台插件、seam 的对应，Qian 选定的 canonical API 与禁用项，见[与 Qian 的关系](/sdk/kun/with-qian)。
