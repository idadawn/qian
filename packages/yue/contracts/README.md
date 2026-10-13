# @idadawn/yue-contracts

不依赖 UI 的 Schema 与 TypeScript 契约层。S1-YUE-005 已交付 `component-manifest.schema.json`，统一组件名称、版本、可序列化 props、slots、events、a11y、令牌与示例形状。

`componentManifestSchema` 为运行时导出，`ComponentManifest` 类型由 Schema 生成：

```bash
pnpm --filter @idadawn/yue-contracts contracts:generate
pnpm --filter @idadawn/yue-contracts contracts:check
```

不要手改 component-manifest.generated.ts。组件 props 本身必须是合法 JSON Schema，示例还需按对应 props Schema 校验；core 的目录单测同时执行这两层检查。

FormSchema/ListSchema、控件注册表、SSE、Filter AST 的前端生成类型与 Field Type 配对继续由 S1-YUE-007/009 及后续任务交付，组件 manifest 不替代低代码控件契约。
