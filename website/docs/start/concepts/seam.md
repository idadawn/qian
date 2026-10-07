---
title: seam
description: 一项能力的缝合点：Service Definition + Provider + Consumer；换 Provider 不改调用方，组件之间只经 Abstractions 引用。
part: start
project: qian
audience: [developer]
updated: 2026-10-07
---

# seam

**seam** 是一项能力的缝合点：一个 Service Definition + 一个或多个 Provider + 一个或多个 Consumer。换 Provider 不改 Consumer。

| 角色 | 包 | 做什么 |
|---|---|---|
| Definition | `Qian.<X>.Abstractions` | 拥有服务键（如 `ctx.Files`）并定义类型 |
| Provider | `Qian.<X>`、`Qian.<X>.<实现>` | 用 `Provide` 填充服务键，如对象存储、S3 兼容、本地三种文件 Provider |
| Consumer | 任何组件或宿主 | 用 `Inject` 声明依赖，只认 Definition |

依赖规则：组件之间只经 `*.Abstractions` 引用；任何 Qian 包的传递依赖里出现注册表、生命周期、能力目录三件套或其他 Provider 包，CI 的依赖图门禁直接失败（PRD §14 C19，[测试与门禁](https://github.com/idadawn/qian/blob/main/tests/README.md)）。

各 seam 的包名与服务键见[组件模型规格](/design/prd/specs/component-model) §6。
