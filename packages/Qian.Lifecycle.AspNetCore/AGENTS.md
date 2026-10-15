# 生命周期管理面 HTTP 适配器

- 主文档统一维护在 ../Qian.Lifecycle/README.md。
- 只依赖 Lifecycle Definition 与 ErrorDetail 结果，不依赖生命周期 Provider；主体从可信请求服务获取，租户由已认证宿主建立。
- 不挂插件端点门禁：scope 合入失败会收紧本 scope 的全部注册，诊断面仍须能给出原因。
- 权限与 404 的判定全部在生命周期控制器内完成；适配器不得自行放行、改写原因或从请求读取租户与授权标志。
