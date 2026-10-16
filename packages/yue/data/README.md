# @idadawn/yue-data

S1-YUE-008：DataTable、Tree / TreeSelect / Cascader、日期时间范围、Upload 与 ImagePreview。设计范围见 yue 规格，交互与完整属性说明以 [docs/index.mdx](docs/index.mdx) 为准；共享示例位于 docs/examples/data-demo.tsx，场景登记在 docs/scenarios.json。

## 数据与状态

DataTable 基于 TanStack Table 9.3，声明式列的 JSON Schema 和生成类型在 contracts 包；render 作为宿主函数单独注入。消费方持有 query，通过 PlatformClient 获取 data 与 total，表格不会二次处理服务端当前页。提供加载、空、错误、重试、行选择、列显隐、固定列与工具栏。文案使用 SDK i18n；先调用 addDataLanguagePacks，或直接 createDataI18n。

Tree 延续既有键盘与 ARIA 实现，使用 TanStack Virtual 对超过 100 个可见节点默认虚拟化，固定行高；也支持受控展开、选择与复选，禁用节点不参与级联勾选。TreeSelect 和 Cascader 复用它，一万节点下只渲染窗口。日期、时间控件使用原生输入和 core 样式；范围无效时显示错误。ImagePreview 复用 core Dialog。

## 上传契约与模型体验

Upload 只经注入的 PlatformClient 请求四个 FILE-003 端点，SHA-256 摘要、秒传、可重入 init、确认分片和 complete 共用同一请求链。取消或失败后，再次 init 读取已确认分片，只发送剩余部分。分片序号从 1 开始，进度按确认字节统计；取消不调用 complete。

组件 mock 契约草案在 contracts/upload.openapi.json，生成类型用 `pnpm --filter @idadawn/yue-data contracts:generate`，CI 的 `contracts:check` 拒绝漂移。默认响应为 uploadId、partSize、uploadedParts 和文件 id / name / size / contentType；这是提供方接线输入，不代表真实文件端点已交付。OpenAPI 生成器隔离在 tools/openapi-codegen，满足其 TypeScript 5 peer；产品工作区仍使用 TypeScript 6。

列 Schema、上传 OpenAPI 与可寻址 data-slot / data-testid 可供工具读取。令牌由 SDK 内存提供器处理，不存储于 URL、iframe 或 localStorage；图片预览不会追加认证令牌。

## 验证与边界

`pnpm yue:gates` 覆盖类型、分层、循环依赖、发布形状、浏览器、SSR、靶场、Lighthouse 与 Rspress。靶场 `/data.html` 使用与 Rspress 相同示例。浏览器验证一万节点跨窗口键盘定位、服务端查询回调、分片请求体/摘要/进度、失败重入、取消与秒传；页面 E2E 实际选择文件并验证完成状态。

## 已知限制与延期项

真实上传及 200 MB 对象存储 E2E 属于 S1-FILE-003；参考应用列表属于 S1-QA-007。Upload 摘要当前读取整个文件，不承诺超大文件的恒定内存。树表、行内编辑、列权限与动态行高留给 S2。TanStack Virtual 会让 React Compiler 跳过该树组件的自动记忆化，保留正常 React 更新，不屏蔽 lint 规则。版本 0.1.0 尚未发布。
