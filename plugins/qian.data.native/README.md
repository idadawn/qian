# qian.data.native

真实入口为 `Qian.Data.Native.Postgres.NativeDataComponent`，提供 `NativeData`，不替代 EF `Data`。组件配置只有受信任宿主绑定名。接口、安全装配、原生证据与安装边界以 [Provider 主文档](../../packages/Qian.Data.Native.Postgres/README.md) 为准。

正式发布与真实 `qian add qian.data.native` 尚未验收，保留骨架标记；它不表示代码入口是空实现，也不能被误读为已完成发行。

`x-aot: true` 依据为本组件的严格 Linux NativeAOT 发布与真实 PostgreSQL 原生运行专项，包含两租户隔离、事务、平台绑定和卸载。持续门为 `.github/workflows/dotnet.yml` 的 `native-data-aot`；命令见 [原生探针](../../tools/Qian.Data.Native.Probe/README.md)。此声明不代替 gateway 完整依赖组合或业务镜像验收。
