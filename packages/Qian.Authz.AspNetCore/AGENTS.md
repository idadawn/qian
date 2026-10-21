# 授权 HTTP 适配器

- 主文档统一维护在 ../Qian.Authz/README.md。
- 只依赖 Authz Definition、Identity Definition 与 ErrorDetail 结果，不依赖 Authz Provider；主体从可信请求服务（ICurrentUser）获取，租户由已认证宿主建立。
- 适配器不得自行判定或改写授权结果，也不得从请求读取主体、租户或权限集合；列出的内容全部来自 `IAuthzService.ListEffectivePermissionsAsync`。
- 委托（act）与 Agent 主体在 S1 一律拒绝；授权组件未就绪时与不存在同为 404。
- PEP（S1-AZ-004）：端点用 `RequireQianPermission` 声明权限点，gRPC 方法用 `[RequiresPermission]`；判定只交给 `IAuthzEnforcement`，适配器不读 `IPermissionChecker`、不自行判定。按调用者可见性在处理器内判定的平台表面用 `PermissionEnforcedInHandlerAttribute(reason)` 显式标记并写明理由，不要用 `AllowAnonymous` 或去掉 `PluginEndpointBinding` 绕过缺省拒绝。
- 拒绝一律写 ErrorDetail（403 AUTHZ_DENIED / 401 UNAUTHENTICATED / 404 NOT_FOUND，`Cache-Control: private, no-store`）；gRPC 以 `PermissionDenied` + trailer `qian-error-bin` 返回；原因码只进决策日志，不回显给调用方。
