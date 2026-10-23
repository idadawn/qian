# @idadawn/yue-sdk

注册表 API、PlatformClient、请求拦截器链、i18n、mock 与运行时配置注入。

- 分层与允许依赖：[yue 专题规格 §3.1](../../../docs/prd/specs/yue.md#31-包结构与依赖方向)；依赖方向由 `packages/yue/boundaries.config.mjs` 与 CI 门禁强制。
- 实现任务：S1-YUE-009（S1-DEC-024 / R109）。

## 使用

`PlatformClient` 的 `api<paths>()` 接收从组件 OpenAPI 生成的类型，返回 openapi-fetch 客户端。`GET` / `POST` 等方法的参数、请求体与响应均由契约推导；不手写另一份接口类型。

```ts
import { PlatformClient, createMockTransport, createI18n, createRegistries } from '@idadawn/yue-sdk';
import type { paths } from './generated/http-v1';

const client = new PlatformClient({
  baseUrl: 'https://platform.example',
  tokenProvider, // 宿主提供：getAccessToken 与可选 refreshAccessToken
  getTenantId: () => currentTenant,
});
const api = client.api<paths>();
const result = await api.GET('/api/tenancy/tenants');
```

令牌仅从内存提供器读取；刷新由宿主以 HttpOnly cookie 完成并更新提供器。并发 401 合并刷新，旧请求晚到时复用新令牌，最多重试一次；失败通知 `onSessionExpired`。请求取消不取消共享刷新；刷新期间切换租户则拒绝重放旧写请求。跨源地址覆盖与重定向被拒绝。租户头只表达当前上下文，服务端仍以令牌为准。

HttpOnly Cookie 会话的宿主改用 `sessionProvider: { refreshSession(): Promise<boolean> }`（与 `tokenProvider` 并列，同一单飞队列，两者同时给出时以 `tokenProvider` 为准）：浏览器不接触令牌，并发 401 合并成一次 `refreshSession()`；返回 true 时每个请求重放一次且不带 Authorization 头，并通知一次 `onSessionRefreshed`（宿主据此重拉随身份变化的数据：菜单、权限、会话快照）；false 或抛错时只通知一次 `onSessionExpired`，下一批 401 重新探测。`@idadawn/yue-shell` 的 `createBffSessionClient` 是对接 Identity.Client / 网关 BFF 的实现。

`PlatformError.detail` 保留冻结契约的 `code`、`trace_id`、`pointer` 与 `errors`；`pointerSegments` 解码 URI 片段。非标准响应抛 `HttpResponseError`，产品用 `sdk.error.http` 显示文案，不透出响应 HTML。int64 超出安全整数范围时转为原始十进制字符串；上传等非 JSON 响应保持原样。

测试只替换 `transport: createMockTransport(routes)`，调用代码不变。处理器收到实际 Request，可检查头、查询与请求体；未匹配路径直接失败，绝不回退真实网络。`middleware` 按注册顺序进入、逆序返回。

`createI18n('zh-CN')` 内置 `zh-CN` / `zh-TW` / `en-US`，`t(key, values)` 用 key 与插值，`loadLanguagePack(locale, loader)` 由宿主提供远程加载器（可以复用 PlatformClient）；失败保留已有包。React 宿主通过 `instance` 的 `languageChanged` 事件订阅语言变化。

`createRegistries()` 为每个宿主创建独立的导航、设置、命令、快捷键、预览器、客户端动作、菜单类型和控件注册表。`register` 返回幂等释放函数；重复 id 拒绝；`toJSON()` 只导出元数据快照。`registerWidget` / `listWidgets` 共用一份目录，按端与分类过滤。默认导出也提供单宿主注册表，SSR 应使用工厂隔离。客户端动作元数据包括参数 Schema、权限与描述；服务端始终重新授权。

## 契约与模型体验

`@idadawn/yue-contracts` 导出 SSE 九类事件的 Schema 与生成类型；ErrorDetail 的发布副本从 `contracts/v0/error-detail.schema.json` 复制，生成检查拒绝漂移。运行 `pnpm --filter @idadawn/yue-contracts contracts:generate` 更新，`contracts:check` 验证。注册表 JSON 可供目录、设计器与模型读取，不含运行时函数或认证凭据。

## 验证

`pnpm exec vitest run --project sdk` 覆盖并发刷新、失败释放、取消、晚到 401、租户切换、错误指针、大整数、mock、注册冲突与多语言。整体门禁运行 `pnpm yue:gates`。

## 已知限制与延期项

- SDK 不提供登录页、OIDC 流程或 HTTP 刷新端点，分别由外壳与身份组件承接；组件消费方提供对应 OpenAPI 生成类型。
- SSE 为 S1 草案，只提供 Schema 与类型；流连接、重连与 Agent 界面归后续任务。客户端动作参数须由执行方按注册表 Schema 校验，服务端权限判定不由前端替代。
- 本包由构造参数注入配置；运行时配置（`window.__APP_CONF__`）的读取与校验在 `@idadawn/yue-shell` 的 `readAppConf`（S1-YUE-002），宿主据此构造 `PlatformClient`。版本 0.1.0 尚未发布。
