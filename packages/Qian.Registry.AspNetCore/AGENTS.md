# 注册表管理面 HTTP 适配器

- 主文档统一维护在 ../Qian.Registry/README.md。
- 只依赖 Registry/Capabilities Definition、ErrorDetail 结果与 SDK 的别名响应头，不依赖注册表 Provider；主体从可信请求服务获取，租户由已认证宿主建立。
- 不挂插件端点门禁：scope 合入失败会收紧本 scope 的全部注册（含注册表自己的），诊断面仍须能给出原因。
- 权限、403 原因码与 404 的判定全部在注册表服务内完成；适配器不得自行放行、改写原因或读取授权标志；写入的目标租户仅映射路径，由服务核对主体和 scope，正文不含租户字段。
- 别名复用同一逻辑，并在写出响应（含错误响应）之前带上 Deprecation 与 Link，所以错误以结果返回而不是抛给异常处理器。

- UseQianRegistryAliases 放在 UseRouting 之后、身份/租户边界与授权之前，认证和绑定失败也必须保留弃用头。
