# 能力目录 HTTP 适配器

- 主文档统一维护在 ../Qian.Capabilities/README.md。
- 只依赖 Definition；主体从可信请求服务获取，租户视图由已认证宿主提供。
- 全部入口需认证，错误交给 ErrorDetail 中间件；不得从 JSON 读取 tenant、roles 或授权标志。
- 别名必须复用同一逻辑，不能出现权限与缓存策略分叉。
