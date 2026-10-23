# 前端插件 SDK

- 先读根 AGENTS.md。包名为 `@idadawn/qian-plugin-sdk`，公共逻辑导入名可通过 npm alias 保留 `@qian/plugin-sdk`。
- 不复制 yue 的 HTTP、认证刷新、i18n 或组件引擎；不让 yue 反向依赖 Qian。
- 远程清单、桥接参数都按不可信输入校验。加载前检查版本、挂载、权益、权限；任何拒绝均不得请求远程代码。
- 插槽注册走真实 Qian TS 运行时，卸载撤销桥接句柄、注册和效果。不宣称 MF 能隔离恶意的同源 JavaScript。
- 私有源验收必须安装实际发布包；本地 pack 只证明打包形状。
