---
"@idadawn/yue-sdk": patch
"@idadawn/yue-shell": patch
---

PlatformClient 新增 HttpOnly Cookie 会话宿主 `sessionProvider`：并发 401 合并成一次会话刷新，刷新结论之后才到达的 401 沿用结论。新增 `onSessionRefreshed` 通知，在重放之前同步发出；宿主收到 `onSessionExpired` 之后不得再从它发起新一轮。宿主回调同步抛错只异步上报，不影响等待中的请求与过期通知。yue-shell 新增外壳的通用部件：`createBrowserRouter` / `matchMenuItem` 路由、`InboxPopover` 站内信弹窗与 `PasswordChange` 改密页。Qian 专有的接线（运行时配置、网关 BFF 会话客户端、bootstrap、能力导航、认证页族适配）不在本包，见私有工作区包 `@qian/shell-kit`（S1-YUE-012）。
