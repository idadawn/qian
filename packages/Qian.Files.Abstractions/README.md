# Qian.Files.Abstractions

文件组件 `qian.files` 的 Definition 包（S1-FILE-001，PRD 7.7）：服务键、门面接口、注册表 seam、契约类型、错误码与集成事件。业务组件只引用本包，经 `Files` 服务键取用文件能力，不依赖 Provider（`Qian.Files`）或任何存储 SDK。

## 服务键与门面

- `FilesDefinition.Key`（`Files`）：由 `qian.files` Provider 提供的 `IFileService`。`[QianPlugin]` 组件在 `Apply` 里写 `ctx.Inject<IFileService>(FilesDefinition.Key)`，并把 `FilesDefinition.Key` 列进 `Inject`；`ctx.Files()` 扩展只给非生成器宿主用（QIANSDK002 不允许把 `ctx` 当参数传出）。
- `IFileService` 的六个操作各对应一个能力，经 SDK 命令管道执行：同一个可串行化事务里完成租户校验、授权判定、业务写入、Outbox 与审计。

| 操作 | 能力 ID（R23） | 副作用 | 权限点 |
| --- | --- | --- | --- |
| `PresignUploadAsync` | `qian.files/presign-upload` | write，幂等 | `qian.files:file:write` |
| `CompleteAsync` | `qian.files/complete-upload` | write，幂等 | `qian.files:file:write` |
| `GetAsync` | `qian.files/get` | read | `qian.files:file:read` |
| `GetDownloadUrlAsync` | `qian.files/download-url` | read，记审计 | `qian.files:file:read` |
| `DeleteAsync` | `qian.files/delete` | destructive，策略提示 confirm | `qian.files:file:delete` |
| `GetQuotaAsync` | `qian.files/quota` | read | `qian.files:file:read` |

常量见 `FilesCapabilities` 与 `FilesPermissions`。

## 注册表 seam（R38）

- `IFileService.Policies`（`IUploadPolicyRegistry`，`FilesDefinition.PoliciesKey`）：上传用途的策略表。`Register(policy)` 按策略里的用途登记并返回释放句柄，组件在 `Apply` 里交给 `ctx.Effect`，卸载即撤销；同名用途独占，重复登记报 `REGISTRATION_CONFLICT`（`key` 为 `Files.Policies:<用途>`）。策略是数据而不是具名服务，不进清单 `provides`。平台宿主里，组件还可以在自己文件夹的 `metadata/upload-policies.json` 声明用途策略（结构见 `schemas/v2/files-upload-policies.schema.json`，只能声明以本组件 ID 开头的用途）：Provider 从 Registry 预装快照汇总，同组件预装多个版本时取最高版本；同用途已有登记（组件配置或 `Register`）时以登记为准，不报冲突。
- `IFileService.Backends`（`IFileBackendRegistry`，`FilesDefinition.BackendsKey`）：存储后端工厂表，`local` 由 Provider 自带。登记后端的组件在入口上写 `[Contributes(typeof(FilesDefinition), nameof(FilesDefinition.BackendsKey), "<名称>", RegistryType = …)]`，生成器据此把 `Files.Backends:<名称>` 派生进 `provides`；S1-FILE-002 的 s3 / oss 以同一方式登记。

用途形如 `<组件 ID>/<名称>`（例如 `acme.crm/attachment`），组件 ID 段进入存储键。`UploadPolicy` 的 `AllowedTypes` 为精确 MIME 或 `type/*`，`MaxSize` 为单文件上限，`MaxCount` 非空时限制每个租户该用途的在用文件数。

## 契约类型

- 请求：`PresignUploadRequest`（幂等键、用途、文件名、大小、声明类型、SHA-256）、`CompleteUploadRequest`、`DeleteFileRequest`。
- 回执：`PresignedUpload`（文件 ID、直传地址、方法、必需请求头、过期时间）、`DownloadTicket`、`FileDescriptor`、`FileQuota`。
- 状态：`FileStatus`（`uploading` / `available` / `quarantined` / `deleted`）与 `FileQuarantineReason`（`size_mismatch` / `checksum_mismatch` / `type_not_allowed`）。
- `FilesContractJson`：上述类型的源生成序列化信息（camelCase、拒绝未知字段、遵守必填与可空标注），HTTP 适配层与宿主复用。

## 错误码

`FilesErrors` 由 Provider 登记到宿主的 `ErrorCatalog`，业务错误只用 409 / 422：

| 错误码 | 状态 | 场景 |
| --- | --- | --- |
| `QUOTA_EXCEEDED` | 422 | 申请大小超过租户剩余配额，`message` 带剩余字节，`pointer` 为 `#/size` |
| `FILE_TYPE_NOT_ALLOWED` | 422 | 声明类型不在策略内（`allowed` 列出允许值），或完成时嗅探出的类型不在策略内 |
| `FILE_TOO_LARGE` | 422 | 申请大小超过用途策略的 `maxSize` |
| `FILE_COUNT_EXCEEDED` | 422 | 用途策略的 `maxCount` 已满 |
| `FILE_CHECKSUM_MISMATCH` | 422 | 完成时服务端自算的大小或 SHA-256 与申请不符，文件已隔离 |
| `FILE_UPLOAD_EXPIRED` | 409 | 上传地址过期且超过完成时限，或用过期的幂等键重发申请 |
| `FILE_UPLOAD_INCOMPLETE` | 409 | 完成时还没有收到直传内容 |
| `FILE_NOT_AVAILABLE` | 409 | 对未完成或已隔离的文件签发下载地址 |

## 集成事件

`FilesEventIds.ObjectCreated`（`files.object.created.v1`）在完成上传、文件转为可用时随能力事务写入 Outbox；`files.object.deleted.v1` 在软删一个可用文件时写入。宿主在唯一的 `AddKunEventBus` 里调用 `RegisterFilesEvents()`，Provider 装载时核对登记，漏登记即拒绝挂载。

## 依赖

只依赖 `Qian.Runtime` 与 `Kun.EventBus.Abstractions`。Kun 存储类型不出现在公共 API 上：后端接口用本包自己的 `PresignedTransfer` 等记录，不把 `Kun.Storage` 泄露给业务组件。

## 模型体验

六个能力都 `AgentExpose`，工具描述由代码派生到 `plugins/qian.files/tools/*.json`。模型拿到的错误都可直接纠正：未知用途返回 `INPUT_INVALID`，`errors[0]` 指向 `#/purpose` 并列出已登记用途；类型不允许时 `allowed` 列出策略类型；超配额时 `message` 给出剩余字节。删除是 destructive，策略提示 `confirm`，Agent 调用须经确认；下载地址每次签发都记审计，模型不能静默批量导出。直传地址是带签名的短时 URL，模型只拿到地址，不接触存储凭据。

## 已知限制与延期项

- 组件声明的上传策略（`metadata/upload-policies.json`）只在有 Registry 的平台宿主汇总；独立模式没有 Registry，只能用组件配置或 `Policies.Register`。PRD 7.7 原写的清单 `contributes: qian.files.upload-policy/v1` 按 v2 规格（声明式贡献放 `metadata/`）改为此方式（S1-FILE-004 裁决）。汇总取全部预装组件的声明，不按租户实际挂载的组件过滤：未知用途报错时 `allowed` 可能列出本租户未挂载组件的用途，与授权目录同一口径。
- 门面名为 `IFileService`（与 `IAuditService`、`IAuthzService` 同一命名），不是任务背景里写的 `IQianFileService`。
- 分片上传、秒传与物理删除（S1-FILE-003）、预览（S2）不在本包契约内，届时以新增成员扩展，不改现有记录。
