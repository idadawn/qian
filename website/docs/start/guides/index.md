---
title: 指南
description: Agent 栈、后端模式、多 profile 组合、安全、连接器与集成、从参考平台迁移：每篇的要点与权威来源。
part: start
project: qian
audience: [developer, operator, decision-maker]
updated: 2026-10-07
---

# 指南

完整指南随相关组件交付陆续补齐；在此之前，每节给出要点与权威来源。

## Agent 栈 {#agent-stack}

Qian 是能力提供方与业务主数据权威，Agent 运行时是 iClaw，模型与 MCP 流量走共用的 Higress；Qian 不写 Agent loop、沙箱、记忆与技能引擎（[ADR-016](/design/adr/adr-016-iclaw-boundary)）。组件的 skills、tools 等 Agent 原生表面在 S1 可用；每租户 MCP 能力门面在 S4。开发入口见 [AI SDK](/sdk/ai/)。

## 后端模式 {#backend}

组件以 seam 组织：Definition、Provider、Consumer 三个角色，组件之间只经 `*.Abstractions` 引用；进程内事件按五种分发模式之一分发，跨进程的集成事件经 Outbox 可靠发布。见 [seam](/start/concepts/seam)、[事件与中间件](/sdk/dotnet/events)、[qian.events](/components/platform/qian.events)。

## 多 profile 组合 {#profiles}

一套部署通常由多个 profile 组成：`gateway` 在边缘做认证与限流，`identity` 是唯一 issuer，`platform` 承载平台插件。各 profile 独立部署、独立升级。见 [宿主与框架](/start/hosts)、[GitOps 部署](/ops/deploy)。

## 安全 {#security}

默认共享库 + `tenant_id` + PostgreSQL RLS 隔离租户；网关默认拒绝匿名；密钥只以 `secretRef://` 引用出现；第三方代码不进共享进程，按信任等级限制运行形态。见 [PRD §13 安全](/design/prd/13-security)、[密钥](/ops/secrets)。

## 连接器与集成 {#integrations}

连接器是为某个 seam 提供 Provider、且需要出网的组件，例如身份 seam 的 `oidc`、`wecom` 连接器：向注册表子服务按名称注册，并在清单 `policy.egress[]` 声明允许的出网目标。见 [组件清单 plugin.json](/cli/plugin-json)。

## 从参考平台迁移 {#migration}

迁移方案按租户 / 身份 / 权限、应用表单与列表、流程、数据集成与展示、前端五个领域组织，见 [迁移方案](/design/migration/)。
