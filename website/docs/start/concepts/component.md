---
title: 组件
description: 组件是分发与安装的单位：文件夹 + 清单 + 声明式表面 + 可选代码；运行时形态叫插件。
part: start
project: qian
audience: [developer, agent]
updated: 2026-10-07
---

# 组件

**组件**是一个文件夹 + 一份清单（`.qian-plugin/plugin.json`）+ 若干声明式表面 + 可选代码，是分发与安装的单位。无代码组件是常态：只带 skills、commands 这类声明式内容，装上即可用。

**插件**是组件的运行时形态：按一个条目挂进共享上下文 `ctx`，向它贡献服务、监听器与可逆副作用。谈文件时说「组件」，谈运行时说「插件」。

- 平台自带的租户、身份、鉴权、文件、配置、网关等能力都是组件，没有特权内核（[ADR-020 D20.1](/design/adr/adr-020-component-model-v2)）。
- 带代码的组件发两类包：`Qian.<X>.Abstractions`（Definition）与 Provider 包，见 [seam](/start/concepts/seam)。
- 装一个组件：`qian add <名称>`，见 [qian 命令](/cli/commands)。

## 参考

- [组件文件夹布局](/cli/layout)
- [组件清单 plugin.json](/cli/plugin-json)
