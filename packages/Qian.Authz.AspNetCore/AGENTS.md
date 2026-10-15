# 授权 HTTP 适配器

- 主文档统一维护在 ../Qian.Authz/README.md。
- 只依赖 Authz Definition、Identity Definition 与 ErrorDetail 结果，不依赖 Authz Provider；主体从可信请求服务（ICurrentUser）获取，租户由已认证宿主建立。
- 适配器不得自行判定或改写授权结果，也不得从请求读取主体、租户或权限集合；列出的内容全部来自 `IAuthzService.ListEffectivePermissionsAsync`。
- 委托（act）与 Agent 主体在 S1 一律拒绝；授权组件未就绪时与不存在同为 404。
