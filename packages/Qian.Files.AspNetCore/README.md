# Qian.Files.AspNetCore

`qian.files` 的 HTTP 表面（S1-FILE-001）：每个端点调用 `IFileService` 的能力门面，错误一律 ErrorDetail；另有 local 后端的直传 / 直下端点。本包不引用任何 Provider，权限点由宿主回调接到 PEP。

## 映射

```csharp
app.MapQianFiles(
    http => /* 按请求租户收窄、可注入 Files 的 qian.files 上下文 */,
    (endpoint, permission) => endpoint.RequireQianPermission(permission), // Qian.Authz.AspNetCore 的 HTTP PEP
    new FilesEndpointOptions { TransferSource = http => /* qian.files 的 global 上下文 */ });
```

| 端点 | 能力 | 权限点 | 成功 |
| --- | --- | --- | --- |
| `POST /api/files/uploads:presign` | `qian.files/presign-upload` | `qian.files:file:write` | 201 `PresignedUpload` |
| `POST /api/files/{id}:complete` | `qian.files/complete-upload` | `qian.files:file:write` | 200 `FileDescriptor` |
| `GET /api/files/{id}` | `qian.files/get` | `qian.files:file:read` | 200 `FileDescriptor` |
| `GET /api/files/{id}/download-url` | `qian.files/download-url` | `qian.files:file:read` | 200 `DownloadTicket` |
| `DELETE /api/files/{id}` | `qian.files/delete` | `qian.files:file:delete` | 200 `FileDescriptor` |
| `GET /api/files/quota` | `qian.files/quota` | `qian.files:file:read` | 200 `FileQuota` |

- 每个端点带 `PluginEndpointBinding`（条目 ID 缺省 `files`）；`RequirePlugin = true` 时改用 `RequireQianPlugin`，要求条目在请求租户 scope 已挂载（平台宿主须装 `AddQianPluginEndpoints` / `UseQianPluginEndpoints`）。
- `requirePermission` 回调必须真的接上授权管道：构建端点时核对权限元数据与授权要求都在，只加元数据的回调直接抛出，避免生产模式的缺省拒绝被「声明了却没判定」绕过。
- 申请只收 JSON（其他类型 415），请求体上限 16 KiB，未知字段 `INPUT_INVALID`。完成与删除可带 `Idempotency-Key` 头（GUID），缺省由端点生成；两者都按状态幂等。
- 响应一律 `Cache-Control: private, no-store`。

宿主须先装认证、可信租户边界（例如只对 `/api` 分支 `UseKunMultiTenancy`）、PEP（`AddQianAuthzEnforcement` + `UseQianAuthzEnforcement`，生产模式）与 ErrorDetail 中间件（`AddQianErrors` / `UseQianErrors`）。

## 直传 / 直下端点

`TransferSource` 非空时登记 `PUT {TransferPath}/{**key}` 与 `GET {TransferPath}/{**key}`（缺省 `/files/blobs`，须与组件配置 `storage.endpoint` 的路径一致）。它们签名即授权：显式 `AllowAnonymous` 并标 `PermissionEnforcedInHandler`，不读租户上下文，由 local 网关核对 HMAC 签名、过期、对象键与类型头；签名不符、过期与对象不存在一律 404，超过声明大小 413，同一上传地址写第二次 409。直下响应带签发时的 `Content-Disposition: attachment`、`X-Content-Type-Options: nosniff` 与 `Content-Security-Policy: sandbox`。这两个端点不要放在租户解析或强制认证的路径下，浏览器直传不带令牌。

## 模型体验

端点与能力一一对应，模型经通用执行入口调用能力时拿到同样的回执与 ErrorDetail；本包不额外暴露模型工具。直传 / 直下地址只在能力回执里出现，过期即失效。

## 已知限制与延期项

- 只为 local 后端提供直传网关；s3 / oss 的直传直接指向对象存储（S1-FILE-002），届时 `TransferSource` 可不设。
- 没有列表端点（也没有对应能力）；按业务对象列文件由业务组件保存文件 ID 后自行查询。
- 直传地址是持有即可用的短时凭证：上传不超过 15 分钟、下载不超过 5 分钟，泄露期间任何人都能用；需要更短时限时由后续任务调整，不在端点上另加开关。
- 分片上传、秒传协议（yue Upload 组件契约）归 S1-FILE-003；yue Upload 的端到端与 `qian add files` 端到端不在本任务。
