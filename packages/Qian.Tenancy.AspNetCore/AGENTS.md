# 租户平台运营 HTTP 表面

- 主文档为 ../Qian.Tenancy/README.md，按 TEN-006 / TEN-002 / TEN-004 的任务边界交付。
- 只依赖 Definition 与 Runtime，平台主体从经过认证的 ICurrentUser 获取；必须经过 ISuperadminBypass 与审计后才进入运营端口。
- 不经过 TenantRequestBoundary，不接受租户主体，不从请求体指定目标租户，不能把本 API 当成普通租户端点挂载。
- 端点真实描述进入 PlatformHttpContract 元数据，错误统一 ErrorDetail，公共契约同步 PublicAPI。
