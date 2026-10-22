# qian.config.apollo

组件清单位于 `.qian-plugin/plugin.json`；代码字段与配置 Schema 由 Provider 入口派生。完整能力边界、宿主装配、验证和安装限制统一维护在 [Qian.Config.Apollo](../../packages/Qian.Config.Apollo/README.md)。

清单保留 `x-skeleton`，补齐代码元数据不代表真实 `qian add` 安装验收已完成。

`x-aot: true` 依据为 gateway 实际生产宿主的严格 Linux NativeAOT 发布和真实 Apollo 2.5.2 运行：配置发行方被网关采用，后续发布写入原生审计链；环境变量与 CLI 引导的 AccessKey 签名也单独验收。持续门和复现命令见 [完整宿主 driver](../../tests/Qian.Gateway.Host.AotTests/README.md)。
