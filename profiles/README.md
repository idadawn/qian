# profile 装配基线

交付 platform、gateway、identity、standalone、minimal 五份静态配置及对应组合包。按 [S1-DEC-012](../pm/tasks/S1-DEC-012.md) 的确认结论，minimal 只包含 Runtime 与声明式表面装载器，初始 patch 为空，不默认加载业务组件。

其它 profile 中尚未交付 Provider 的业务叶节点显式标记 `x-skeleton: true`，可解析、可校验、可 dump；这不代表对应业务 Provider 已可启动。platform 的生产启动项目为 [Qian.Platform.Host](../hosts/Qian.Platform.Host/README.md)（S1-DEP-009）：按本 profile 装配平台插件树，宿主 overlay 关掉没有 Provider 的条目；六个平台组件（tenancy / authz / audit / registry / capabilities / lifecycle）以它的装配与集成测试为启动证据去标（R144 ③）。staging 首次部署（S1-DEP-011）在同一宿主上以 `Identity:InProcess=true` 并入身份分支：宿主内置的扩展 overlay 把 `identity` 条目换成 `qian.identity.server` 并加 `tenancy.signup`，这两个组件同样以该宿主的装配与集成测试为证据去标（R150 ①），identity profile 的骨架警告随之 5→3（identity profile 本身的启动证据仍归 S1-DEP-010）。minimal 无业务骨架警告；测试从实际交付文件启动真实 Runtime，并通过 overlay 挂载无代码表面。

gateway 的生产启动项目为 [Qian.Gateway.Host](../hosts/Qian.Gateway.Host/README.md)，默认组合包按 DEC-030 声明原生 Data/Audit。2026-10-23 完整生产宿主已通过严格 Linux NativeAOT 发布与真实依赖运行，默认 `build.aot: true` 已恢复，代码组件兼容声明由相应持续门维护；组件安装门与 DEP-008 的业务镜像交付仍各自验收。
