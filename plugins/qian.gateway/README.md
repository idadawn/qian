# qian.gateway

本目录已有 `Qian.Gateway.GatewayComponent` 的真实入口、服务键与派生配置 Schema，默认进入 gateway profile。接口、安全边界、验证与延期项以 [Provider 主文档](../../packages/Qian.Gateway/README.md) 为准。

`x-aot: true` 依据是实际生产宿主 `hosts/Qian.Gateway.Host` 的严格 Linux NativeAOT 发布与真实 Apollo、受限 PostgreSQL、Redis、Identity Cookie/Bearer 换签和审计运行证据，持续门为 `gateway-host-aot`。私有源发布与真实 `qian add gateway` 尚未验收，按既有安装门保留 `x-skeleton: true`；业务镜像与生产部署仍归 DEP-008 / DEP-005。
