---
"@idadawn/yue-sdk": patch
"@idadawn/yue-shell": patch
---

PlatformClient 新增 HttpOnly Cookie 会话宿主 `sessionProvider`：并发 401 合并成一次会话刷新，刷新结论之后才到达的 401 沿用结论。新增 `onSessionRefreshed` 通知，在重放之前同步发出；宿主收到 `onSessionExpired` 之后不得再从它发起新一轮。宿主回调同步抛错只异步上报，不影响等待中的请求与过期通知。yue-shell 新增外壳认证接线：`readAppConf` 运行时配置校验、`createBffSessionClient` 网关 BFF 会话客户端、`fetchNavigation` / `fetchPermissions` / `toMenu` 能力导航与有效权限、`createBrowserRouter` / `matchMenuItem` 路由、`InboxPopover` 站内信弹窗、`createIdentityAuthProvider` 认证页族适配与 `PasswordChange` 改密页。
