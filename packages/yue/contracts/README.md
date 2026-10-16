# @idadawn/yue-contracts

不依赖 UI 的 Schema 与 TypeScript 契约层。S1-YUE-005 已交付 `component-manifest.schema.json`，统一组件名称、版本、可序列化 props、slots、events、a11y、令牌与示例形状。

`componentManifestSchema` 为运行时导出，`ComponentManifest` 类型由 Schema 生成：

```bash
pnpm --filter @idadawn/yue-contracts contracts:generate
pnpm --filter @idadawn/yue-contracts contracts:check
```

不要手改 component-manifest.generated.ts。组件 props 本身必须是合法 JSON Schema，示例还需按对应 props Schema 校验；core 的目录单测同时执行这两层检查。

S1-YUE-009 已增加 `sseEventSchema` / `SseEvent`（SSE 九类事件草案）和 `errorDetailSchema` / `ErrorDetail`。ErrorDetail 的副本从冻结契约 `contracts/v0/error-detail.schema.json` 生成，不能手改；生成检查同时验证副本和类型。SSE 的 `clientAction` 只描述名称与参数，执行方须按 SDK 动作注册表做参数校验，服务端重新授权。

FormSchema/ListSchema 与控件元数据草案由 S1-YUE-007 交付；Filter AST 的前端生成类型与 Field Type 配对留给后续任务。组件 manifest 不替代低代码控件契约。
