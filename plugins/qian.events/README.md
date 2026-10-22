# qian.events

Events Provider 的实现位于 [packages/Qian.Events](../../packages/Qian.Events/README.md)，契约在 [Qian.Events.Abstractions](../../packages/Qian.Events.Abstractions/README.md)。已提供 v1 信封写入、按租约领取的 Outbox 投递（publisher confirm）、RabbitMQ 拓扑声明与 payload Schema 导出。`runtime.nuget` 钉到私有源已发布的 `Qian.Events` 版本与摘要，S1-RT-011 的 E2E 已用 `qian add events` 从私有源安装并运行 Provider。

清单的 inject、provides 与 runtime 字段由 Qian.Plugin.Tool 从实际程序集同步，CI 检查两者不漂移。配置契约与 EventsPlugin 一致：

- `broker`：可选，AMQP 地址的 secretRef（platform 或 dev），必须是宿主 `AddQianEventsBroker` 登记过的引用，凭据不写进组件文件夹；
- `outbox.lock`：可选，`skip-locked`（默认）或 `redis`，必须与宿主选项一致。

计划进入全部 .NET profile，角色为 Events seam 的 Provider，可挂在 global 或 tenant scope。标准原生入口是 Qian.Events.EventsPlugin。库模式目前可通过 `AddQianEvents` 与 `AddQianEventsBroker` 独立使用；消费端由 S1-EVT-002 交付，事件目录接口与主题 ACL 由 S1-EVT-003 交付。

该组件已通过既有真实安装门，清单不再带 `x-skeleton`。

配置 Schema 的真相源为 Provider 的 `ComponentConfiguration.Schema`，清单的 `runtime.configSchema` 与文件由工具同步，`check` 同时检查配置声明漂移。
