# Qian.Plugin.Sdk.AspNetCore

SDK 的 HTTP 入口适配，主文档见 [Qian.Plugin.Sdk](../Qian.Plugin.Sdk/README.md)。本包只引用 Runtime 与 Registry Definition；宿主提供真实注册表上下文。当前覆盖租户 Minimal API，未证明归属的 app/agent-session 不开放。

`DeprecatedAlias` 是 v1 `/api/kernel/*` 别名的共用响应头（R33，组件规格 §9.5）：`Deprecation: @1791331200` 与指向后继路径的 `Link: <…>; rel="successor-version"`，后继带宿主 PathBase 并保留原查询串。能力目录与注册表管理面的别名都调用它；须在写出响应体（含错误响应）之前调用，异常处理器会清空已写的响应头。
