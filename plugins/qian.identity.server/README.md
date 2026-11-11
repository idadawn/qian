# qian.identity.server

组件清单位于 `.qian-plugin/plugin.json`；代码字段与配置 Schema 由 Provider 入口派生。完整能力边界、宿主装配、验证和安装限制统一维护在 [Qian.Identity.Server](../../packages/Qian.Identity.Server/README.md)。

清单已去掉 `x-skeleton`（R150 ①）：平台镜像预装、平台宿主同进程身份分支的装配与集成测试是启动证据；这不代表 `qian add` 独立安装验收已完成。
