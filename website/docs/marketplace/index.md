---
title: 市场与 ISV
description: 组件上架流程、信任等级、签名与租户私有市场的规则；市场本身在 S3 开放，S3 前只放规则。
part: marketplace
audience: [isv, developer, decision-maker]
stage: S3
tag: S3
updated: 2026-10-07
---

# 市场与 ISV

**阶段**：S3 对第三方开放；S3 之前本页只写规则（[ADR-014](/design/adr/adr-014-marketplace)、[组件模型规格](/design/prd/specs/component-model) §8、§11）。

## 应用与分发

- **应用 = 组合包 + 市场条目**。市场条目只有 `name`、`source`、`policy`、`category`、`interface`，字段见[目录市场 marketplace.json](/cli/marketplace-json)。
- 两种分发：`managed`（托管、锁定、可升级，租户在上层叠加定制，默认）与 `template`（拷贝即拥有，可改）。
- `managed` 升级做三方比较（旧包、新包、租户层），冲突项由租户选择保留、采用新版或手工合并；升级前快照，可回滚。

## 信任等级

| 等级 | 允许的代码形态 | 运行位置 |
|---|---|---|
| `system` / `official` | 全部形态 | 共享进程（profile 宿主） |
| `partner` | `package`、`function`、`ui`；`service` 在 S3 按沙箱策略另行开放 | 独立工作负载 / Wasm / 浏览器隔离 |
| `private` | `package`、`function`、`ui` | 同上 |
| `community` | 同 `private`；S1 只接受无代码组件 | 同上 |

第三方代码永不进共享进程；无代码组件对所有等级开放。S3 不开放第三方 `service` 上架。

## 签名与审核

- 按信任等级强制验签：OCI 制品 cosign 分离签名，组件文件夹 digest 清单 Ed25519 分离签名；市场文件本身也签名，公钥须命中客户端信任根。
- 未签名或未命中信任根的条目需 `--allow-unverified`，只允许无代码组件、等级最高 `community`。
- 审核分级：免费元数据模板快审，付费或带代码的走安全审查，权限扩大触发复审。

## 私有市场

项目可在 `.qian/config.yml` 里配置多个市场并设优先级，`qian add` 按优先级查找；企业或租户的私有市场与官方市场并列，额外信任的公钥写进 `trust.keys`。
