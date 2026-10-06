---
title: MCP
description: 组件附带的 .mcp.json 表面（S1）与每租户 MCP 能力门面（S4）；MCP 以 2026-07-28 版为目标。
part: sdk
audience: [developer, agent]
updated: 2026-10-07
---

# MCP

| 能力 | 阶段 | 说明 |
|---|---|---|
| 组件的 `.mcp.json` 表面 | S1 | 组件附带的 MCP 服务器随组件安装，进 `ctx.McpServers` 并受 scope 过滤；`headers` / `env` 里的密钥写成 `secretRef://`，明文密钥校验报错 |
| 文档检索 MCP | S1（可选） | 让编码代理检索本站内容 |
| 每租户 MCP 能力门面 | S4 | MCP 服务器按租户的逻辑门面托管在 Higress，而不是每个插件一个进程；工具定义由能力目录生成 |

- 协议以 MCP 2026-07-28 版为目标（无状态、`server/discover`、头部路由、可缓存列表），经 `server/discover` 兼容旧客户端（[ADR-016 D16.4](/design/adr/adr-016-iclaw-boundary)）。
- 网关按 `Mcp-Method` / `Mcp-Name` 头路由，敏感参数不进头部（[ADR-010](/design/adr/adr-010-gateway-layers)）。
- `.mcp.json` 的文件格式见[声明式表面的文件格式](/cli/surfaces)。
