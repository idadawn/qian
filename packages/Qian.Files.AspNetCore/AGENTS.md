# Qian.Files.AspNetCore

- 本包只做 HTTP 适配：端点调用 `IFileService` 门面，不直接读写数据库或存储，不引用任何 Provider；PEP 由宿主经 `requirePermission` 回调接入（本包不依赖 `Qian.Authz.AspNetCore`）。
- 每个 `/api/files` 端点必须带 `PluginEndpointBinding` 与权限点，构建期核对授权要求确实接上；新增端点同时补能力、权限点与验收用例中的端点清单断言。
- 直传 / 直下端点签名即授权，保持 `AllowAnonymous` + `PermissionEnforcedInHandler`，所有拒绝统一 404（只有超限 413、重复写 409），不要回显签名或对象键。
- 直下响应头（attachment、nosniff、CSP sandbox、no-store）不可配置；错误一律 ErrorDetail（`QianErrorException`），不要返回裸状态码。
