# qian.config

标准配置 Provider：借用宿主的 `IConfiguration`，以 `Config` 服务键提供 `IConfigService`，只能挂在 global scope，配置只接受空对象。实现是 [Qian.Config.Apollo](../../packages/Qian.Config.Apollo/README.md) 的 `ConfigurationPlugin`；Apollo 引导与一致性校验由另一个组件 `qian.config.apollo` 负责。

清单的代码字段（inject、provides、runtime.form / entry）由 Qian.Plugin.Tool 从实际程序集同步，CI 检查两者不漂移；`runtime.nuget` 钉到私有源已发布的 `Qian.Config.Apollo` 版本与摘要。S1-RT-011 的 E2E 已用 `qian add config` 从私有源安装并运行该 Provider。

本文件夹仍标记 x-skeleton（S1-RT-002 建立的配置骨架）：该任务的验收只移除 qian.data 的标记，本组件标记的去留记在 S1-RT-011 日志的建议裁决里。

`metadata/permissions.json` 声明 `qian.config:values:read`，供配置值读取接口（`GET /api/config/values/{plugin}`，见 Provider README）判定；`metadata/roles.json` 为空：该接口只对平台主体开放，不给租户角色模板授予这个权限点。本组件仍是骨架、不能成为预装绑定，这个权限点在 S1-RT-033 收口清单前不会登记进真实权限目录，接口按拒绝处理。
