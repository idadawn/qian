# qian.config

标准配置 Provider：借用宿主的 `IConfiguration`，以 `Config` 服务键提供 `IConfigService`，只能挂在 global scope，配置只接受空对象。实现是 [Qian.Config.Apollo](../../packages/Qian.Config.Apollo/README.md) 的 `ConfigurationPlugin`；Apollo 引导与一致性校验由另一个组件 `qian.config.apollo` 负责。

清单的代码字段（inject、provides、runtime.form / entry）由 Qian.Plugin.Tool 从实际程序集同步，CI 检查两者不漂移；`runtime.nuget` 钉到私有源已发布的 `Qian.Config.Apollo` 版本与摘要。S1-RT-011 的 E2E 已用 `qian add config` 从私有源安装并运行该 Provider。


该组件已通过既有真实安装门，清单不再带 `x-skeleton`。

配置 Schema 的真相源为 Provider 的 `ComponentConfiguration.Schema`，清单的 `runtime.configSchema` 与文件由工具同步，`check` 同时检查配置声明漂移。
