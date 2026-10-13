# yue Storybook

S1-YUE-005 的组件文档与自动化验收入口。复用 core 的真实示例、组件 manifest 和令牌；不实现产品业务。

- 本地：`pnpm --filter yue-storybook dev`，默认 http://localhost:6006。
- 静态构建：`pnpm --filter yue-storybook build`，产物在 dist。
- 浏览器测试：`pnpm --filter yue-storybook test`，a11y.test=error，违规会失败。
- CSF Factories 位于 `packages/yue/core/src/storybook`；52 个文档分组覆盖 51 个组件入口与令牌页。函数参数与不可序列化的插槽请参考自动属性表和 core 主文档。

所有测试数据与文案仅供演示。主题在每条 story 前设置并在清理时恢复；测试和 Storybook 配置不进入组件发布包。
