# qian.shell

前端外壳源码在 [apps/qian-shell](../../apps/qian-shell/README.md)，公共前端契约在 [qian-plugin-sdk](../../packages/qian-plugin-sdk/README.md)。组件运行于 `platform` 的 shell 组合包，提供 `Shell` 与六个带版本的插槽；消费者通过真实 TS 运行时注册贡献，不反向依赖平台实现。

`runtime.form: ui`，入口 `ui/dist/index.html` 对应构建后的静态制品。`apps/qian-shell/scripts/package-static.mjs` 生成 `apps/qian-shell/dist/distribution/plugins/qian.shell/ui/dist/`，保留组件清单与配置 Schema；伴随的 `component-artifact.json` 校验组件入口存在、每份文件摘要及 Web 根映射。由 DEP-008 放入 profile 镜像并经 gateway 提供。SDK、官方租户 MF 远程、可信清单与作用域桥接已实现；`x-skeleton` 保留为完整安装/部署门，不将浏览器测试当成 `qian add` 或正式 profile 已验收。

## 模型体验

客户端动作目录导出名称、参数 Schema、权限和描述，六类插槽可静态索引。前端只作体验层裁剪，服务端鉴权不因此省略。

## 已知限制与延期项

S1 只加载可信官方 MF UI。真实认证/会话接线、权益源和静态镜像部署分别由 YUE-002、ENT-002、DEP-008 继续；当前验收已经覆盖真实审计链的受控参考适配。
