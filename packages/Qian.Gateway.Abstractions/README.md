# 网关 Definition

本包定义 `Gateway` 服务键、只读 `IGatewayService` 及静态路由配置词汇，不依赖 Provider。`GatewayDefinition.Gateway()` 从当前插件上下文解析服务；`AudienceFor(clusterId)` 只接受已登记的下游，返回该 cluster 的唯一 S2S 受众。

默认 Provider 为 `Qian.Gateway`，进入 gateway profile。组件文件夹、配置 Schema、安装边界与验证见 [Provider 主文档](../Qian.Gateway/README.md)。

## 模型体验

该 seam 不公开模型工具，不接收请求中的 issuer、audience、路由地址或凭据。消费方只能读取当前挂载状态和构建宿主时确定的下游受众。

## 已知限制与延期项

静态配置不代表已通过 NativeAOT 或私有源安装验收。动态路由、能力聚合与可见性由 GW-003 接续。
