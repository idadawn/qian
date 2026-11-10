# qian.lifecycle

组件清单位于 `.qian-plugin/plugin.json`；代码字段与配置 Schema 由 Provider 入口派生。完整能力边界、宿主装配、验证和安装限制统一维护在 [Qian.Lifecycle](../../packages/Qian.Lifecycle/README.md)。

清单已去掉 `x-skeleton`（R144 ③）：本组件是 `policy.required` 的平台组件，只在平台宿主里运行，安装路径是平台镜像预装，启动证据是[平台生产宿主](../../hosts/Qian.Platform.Host/README.md)的 profile 装配与集成测试；不提供 `qian add` 独立安装。
