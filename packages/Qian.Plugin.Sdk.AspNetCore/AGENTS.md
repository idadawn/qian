# SDK HTTP 适配

- 主文档沿用 Qian.Plugin.Sdk/README.md，本包仅负责 HTTP 入口装配。
- 只引用 Runtime/Definition，不引用 Registry、Tenancy、Authz 等 Provider。
- 租户只来自已认证主身份及匹配的 ITenantContext；不能接受请求头、正文或任意 scope 参数作为归属证明。
- 读取实际 Registry scope 视图；预装清单不是运行资格。不存在、被隐藏、停用统一 404。
- 未安装或错误排序中间件时，已标注的 endpoint 必须拒绝执行。
